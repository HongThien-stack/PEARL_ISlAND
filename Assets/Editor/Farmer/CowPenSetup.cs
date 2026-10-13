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
    private const string CowWalkLeftClipPath = "Assets/Assets/Animal/House_Animal/Bò/Cow_Walk_Left.anim";
    private const string CowWalkRightClipPath = "Assets/Assets/Animal/House_Animal/Bò/Cow_Walk_Right.anim";
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

        EnsureWalkingAnimations(controller);
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

    [MenuItem("Tools/Pearl Island/Update Cow Walking Animations")]
    public static void UpdateCowWalkingAnimations()
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(CowControllerPath);
        if (controller == null)
        {
            Debug.LogError("[CowPenSetup] Không tải được COW.controller.");
            return;
        }

        EnsureWalkingAnimations(controller);
        Debug.Log("[CowPenSetup] Đã gắn Cow_Walk_Left và Cow_Walk_Right vào controller của 4 con bò.");
    }

    private static void EnsureWalkingAnimations(AnimatorController controller)
    {
        AnimationClip leftClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(CowWalkLeftClipPath);
        AnimationClip rightClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(CowWalkRightClipPath);
        Sprite[] walkingFrames = AssetDatabase.LoadAllAssetRepresentationsAtPath(CowSheetPath)
            .OfType<Sprite>()
            .OrderBy(sprite => ExtractFrameNumber(sprite.name))
            .ToArray();

        if (leftClip == null || rightClip == null || walkingFrames.Length == 0)
        {
            Debug.LogError("[CowPenSetup] Không tải được animation trái/phải hoặc sprite sheet bò đi.");
            return;
        }

        SetSpriteFrames(leftClip, walkingFrames);
        SetSpriteFrames(rightClip, walkingFrames);
        SetFacingRotation(leftClip, 0f);
        SetFacingRotation(rightClip, 180f);

        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        EnsureState(stateMachine, "Cow_Walk_Left", leftClip, new Vector3(200f, -50f, 0f));
        AnimatorState rightState = EnsureState(stateMachine, "Cow_Walk_Right", rightClip, new Vector3(200f, 20f, 0f));
        stateMachine.defaultState = rightState;

        EditorUtility.SetDirty(leftClip);
        EditorUtility.SetDirty(rightClip);
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
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

        SetSpriteFrames(eatClip, eatingFrames);

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

    private static void SetSpriteFrames(AnimationClip clip, Sprite[] frames)
    {
        ObjectReferenceKeyframe[] keyframes = new ObjectReferenceKeyframe[frames.Length];
        for (int i = 0; i < frames.Length; i++)
        {
            keyframes[i] = new ObjectReferenceKeyframe
            {
                time = i * 0.08f,
                value = frames[i]
            };
        }

        EditorCurveBinding spriteBinding = new EditorCurveBinding
        {
            path = string.Empty,
            type = typeof(SpriteRenderer),
            propertyName = "m_Sprite"
        };
        AnimationUtility.SetObjectReferenceCurve(clip, spriteBinding, keyframes);

        AnimationClipSettings clipSettings = AnimationUtility.GetAnimationClipSettings(clip);
        clipSettings.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(clip, clipSettings);
        clip.frameRate = 12.5f;
    }

    private static void SetFacingRotation(AnimationClip clip, float yRotation)
    {
        EditorCurveBinding rotationBinding = new EditorCurveBinding
        {
            path = string.Empty,
            type = typeof(Transform),
            propertyName = "localEulerAnglesRaw.y"
        };

        AnimationCurve rotationCurve = AnimationCurve.Constant(0f, clip.length, yRotation);
        AnimationUtility.SetEditorCurve(clip, rotationBinding, rotationCurve);
    }

    private static AnimatorState EnsureState(
        AnimatorStateMachine stateMachine,
        string stateName,
        AnimationClip clip,
        Vector3 position)
    {
        AnimatorState state = stateMachine.states
            .Select(child => child.state)
            .FirstOrDefault(candidate => candidate.name == stateName);
        if (state == null)
        {
            state = stateMachine.AddState(stateName, position);
        }

        state.motion = clip;
        return state;
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
