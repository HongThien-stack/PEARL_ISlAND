#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class CowPenSetup
{
    private const string CowSheetPath = "Assets/Assets/Animal/House_Animal/Bò/pixellab-Make-the-cow-move-forward--1789140977812.png";
    private const string CowEatSheetPath = "Assets/Assets/Animal/House_Animal/Bò/pixellab-Make-the-cow-that-is-grazing--1789144480686.png";
    private const string CowEatClipPath = "Assets/Assets/Animal/House_Animal/Bò/Cow_Eat.anim";
    private const string CowControllerPath = "Assets/Assets/Animal/House_Animal/Bò/COW.controller";

    private static readonly Vector3[] StartingPositions =
    {
        new Vector3(-14.2f, -12.2f, 0f),
        new Vector3(-9.7f, -14.5f, 0f),
        new Vector3(-4.7f, -10.4f, 0f),
        new Vector3(-10.0f, -6.7f, 0f)
    };

    [MenuItem("Tools/Pearl Island/Setup Cow Pen")]
    public static void SetupCowPen()
    {
        GameObject farmer = GameObject.Find("Farmer");
        if (farmer == null)
        {
            Debug.LogError("[CowPenSetup] Không tìm thấy GameObject 'Farmer' trong scene đang mở.");
            return;
        }

        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(CowControllerPath);
        Sprite firstFrame = AssetDatabase.LoadAllAssetRepresentationsAtPath(CowSheetPath)
            .OfType<Sprite>()
            .OrderBy(sprite => ExtractFrameNumber(sprite.name))
            .FirstOrDefault();

        if (controller == null || firstFrame == null)
        {
            Debug.LogError("[CowPenSetup] Không tải được controller hoặc sprite bò.");
            return;
        }

        EnsureEatingAnimation(controller);

        Transform herd = farmer.transform.Find("Cow Herd");
        if (herd == null)
        {
            GameObject herdObject = new GameObject("Cow Herd");
            Undo.RegisterCreatedObjectUndo(herdObject, "Create Cow Herd");
            herd = herdObject.transform;
            herd.SetParent(farmer.transform, false);
        }

        for (int i = 0; i < StartingPositions.Length; i++)
        {
            string cowName = $"Cow {i + 1}";
            Transform existing = herd.Find(cowName);
            GameObject cow;
            if (existing == null)
            {
                cow = new GameObject(cowName);
                Undo.RegisterCreatedObjectUndo(cow, $"Create {cowName}");
                cow.transform.SetParent(herd, false);
            }
            else
            {
                cow = existing.gameObject;
                Undo.RecordObject(cow.transform, $"Update {cowName}");
            }

            cow.transform.localPosition = StartingPositions[i];
            cow.transform.localScale = new Vector3(1.6f, 1.6f, 1f);

            SpriteRenderer renderer = GetOrAdd<SpriteRenderer>(cow);
            renderer.sprite = firstFrame;
            renderer.spriteSortPoint = SpriteSortPoint.Pivot;

            Animator animator = GetOrAdd<Animator>(cow);
            animator.runtimeAnimatorController = controller;

            GetOrAdd<CowPenWanderer>(cow);
            GetOrAdd<DynamicYSorter>(cow);
        }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        Selection.activeGameObject = herd.gameObject;
        EditorGUIUtility.PingObject(herd.gameObject);
        Debug.Log("[CowPenSetup] Đã tạo 4 con bò có animation và giới hạn di chuyển trong chuồng Farmer.");
    }

    [MenuItem("Tools/Pearl Island/Update Cow Eating Animation")]
    public static void UpdateCowEatingAnimation()
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(CowControllerPath);
        if (controller == null)
        {
            Debug.LogError("[CowPenSetup] Không tải được COW.controller.");
            return;
        }

        EnsureEatingAnimation(controller);
        Debug.Log("[CowPenSetup] Đã gắn Cow_Eat vào controller của 4 con bò.");
    }

    private static void EnsureEatingAnimation(AnimatorController controller)
    {
        AnimationClip eatClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(CowEatClipPath);
        Sprite[] eatingFrames = AssetDatabase.LoadAllAssetRepresentationsAtPath(CowEatSheetPath)
            .OfType<Sprite>()
            .OrderBy(sprite => ExtractFrameNumber(sprite.name))
            .ToArray();

        if (eatClip == null || eatingFrames.Length == 0)
        {
            Debug.LogError("[CowPenSetup] Không tải được Cow_Eat.anim hoặc sprite sheet bò ăn.");
            return;
        }

        ObjectReferenceKeyframe[] keyframes = new ObjectReferenceKeyframe[eatingFrames.Length];
        for (int i = 0; i < eatingFrames.Length; i++)
        {
            keyframes[i] = new ObjectReferenceKeyframe
            {
                time = i * 0.08f,
                value = eatingFrames[i]
            };
        }

        EditorCurveBinding spriteBinding = new EditorCurveBinding
        {
            path = string.Empty,
            type = typeof(SpriteRenderer),
            propertyName = "m_Sprite"
        };
        AnimationUtility.SetObjectReferenceCurve(eatClip, spriteBinding, keyframes);

        AnimationClipSettings clipSettings = AnimationUtility.GetAnimationClipSettings(eatClip);
        clipSettings.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(eatClip, clipSettings);
        eatClip.frameRate = 12.5f;

        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        AnimatorState eatState = stateMachine.states
            .Select(child => child.state)
            .FirstOrDefault(state => state.name == "Cow_Eat");
        if (eatState == null)
        {
            eatState = stateMachine.AddState("Cow_Eat", new Vector3(430f, 80f, 0f));
        }
        eatState.motion = eatClip;

        EditorUtility.SetDirty(eatClip);
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
    }

    private static T GetOrAdd<T>(GameObject gameObject) where T : Component
    {
        T component = gameObject.GetComponent<T>();
        return component != null ? component : Undo.AddComponent<T>(gameObject);
    }

    private static int ExtractFrameNumber(string spriteName)
    {
        int underscore = spriteName.LastIndexOf('_');
        if (underscore >= 0 && int.TryParse(spriteName.Substring(underscore + 1), out int frame))
        {
            return frame;
        }

        return int.MaxValue;
    }
}
#endif
