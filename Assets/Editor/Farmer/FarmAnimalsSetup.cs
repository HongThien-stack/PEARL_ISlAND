#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Creates all chicken/pig animation assets and installs the animals in the farm scene.</summary>
public static class FarmAnimalsSetup
{
    private const string ScenePath = "Assets/PixelLabMap/Generated/PixelLabMap.unity";
    private const string AnimalRoot = "Assets/Assets/Animal/House_Animal";
    private const string ChickWalkSheet = AnimalRoot + "/Gà con/pixellab-Add-an-animation-of-the-chicke-1790172593435.png";
    private const string ChickEatSheet = AnimalRoot + "/Gà con/pixellab-Add-an-animation-of-the-chicke-1790172943774.png";
    private const string HenWalkSheet = AnimalRoot + "/Gà mái/pixellab-Please-create-an-animation-of--1790262924340.png";
    private const string HenEatSheet = AnimalRoot + "/Gà mái/pixellab-Please-create-an-animation-of--1790265381975.png";
    private const string RoosterWalkSheet = AnimalRoot + "/Gà trống/pixellab-Please-create-an-animation-of--1789302445077.png";
    private const string RoosterEatSheet = AnimalRoot + "/Gà trống/pixellab-Please-create-an-animation-of--1789302165527.png";
    private const string PigWalkSheet = AnimalRoot + "/Heo/pixellab-Make-the-pig-move-forward--1789139615453.png";
    private const string PigEatSheet = AnimalRoot + "/Heo/pixellab-A-pig-eating-food-from-the-fee-1789139004700.png";

    // Bounds are expressed in the local space of each pen root.
    private static readonly Vector2 ChickenMinimum = new Vector2(-9.8f, 10.35f);
    private static readonly Vector2 ChickenMaximum = new Vector2(1f, 16.55f);
    private static readonly Vector2 PigMinimum = new Vector2(-11.2f, 37.05f);
    private static readonly Vector2 PigMaximum = new Vector2(3f, 44.15f);

    private static readonly Vector3[] HenPositions =
    {
        new Vector3(-8.4f, 11.6f, 0f),
        new Vector3(-5.9f, 14.0f, 0f),
        new Vector3(-2.2f, 11.8f, 0f),
        new Vector3(-8.0f, 15.7f, 0f),
        new Vector3(0.0f, 15.5f, 0f)
    };

    private static readonly Vector3[] RoosterPositions =
    {
        new Vector3(-3.2f, 15.1f, 0f),
        new Vector3(0.0f, 12.2f, 0f)
    };

    private static readonly Vector3[] PigPositions =
    {
        new Vector3(-10.2f, 38.2f, 0f),
        new Vector3(-7.4f, 41.4f, 0f),
        new Vector3(-3.8f, 38.8f, 0f),
        new Vector3(0.3f, 41.5f, 0f),
        new Vector3(2.1f, 38.3f, 0f)
    };

    [MenuItem("Tools/Pearl Island/Setup Chickens And Pigs")]
    public static void SetupFromMenu()
    {
        SetupFarmAnimals();
    }

    [InitializeOnLoadMethod]
    private static void ScheduleSetupForOpenFarmScene()
    {
        EditorApplication.update -= TrySetupWhenSceneIsReady;
        EditorApplication.update += TrySetupWhenSceneIsReady;
    }

    private static void TrySetupWhenSceneIsReady()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.isLoaded || scene.path != ScenePath) return;
        if (FindInScene(scene, "Farm_Chicken") == null || FindInScene(scene, "Farm_Pig") == null) return;

        EditorApplication.update -= TrySetupWhenSceneIsReady;
        GameObject farmChicken = FindInScene(scene, "Farm_Chicken");
        GameObject farmPig = FindInScene(scene, "Farm_Pig");
        if (farmChicken.transform.Find("Animals") != null && farmPig.transform.Find("Pig Herd") != null) return;
        SetupFarmAnimals();
    }

    // Public entry point used by command-line validation/setup.
    public static void SetupFarmAnimals()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath)
        {
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        // Walk and eat sheets must use the same PPU; otherwise the pig visibly
        // grows when switching to the eating animation.
        ConfigurePixelsPerUnit(PigWalkSheet, 32f);
        ConfigurePixelsPerUnit(PigEatSheet, 32f);

        AnimatorController chickController = CreateController(
            AnimalRoot + "/Gà con/CHICK.controller", "Chick", ChickWalkSheet, ChickEatSheet, false);
        AnimatorController henController = CreateController(
            AnimalRoot + "/Gà mái/HEN.controller", "Hen", HenWalkSheet, HenEatSheet, false);
        AnimatorController roosterController = CreateController(
            AnimalRoot + "/Gà trống/ROOSTER.controller", "Rooster", RoosterWalkSheet, RoosterEatSheet, false);
        AnimatorController pigController = CreateController(
            AnimalRoot + "/Heo/PIG.controller", "Pig", PigWalkSheet, PigEatSheet, false);

        Sprite chickSprite = FirstFrame(ChickWalkSheet);
        Sprite henSprite = FirstFrame(HenWalkSheet);
        Sprite roosterSprite = FirstFrame(RoosterWalkSheet);
        Sprite pigSprite = FirstFrame(PigWalkSheet);

        if (chickController == null || henController == null || roosterController == null || pigController == null ||
            chickSprite == null || henSprite == null || roosterSprite == null || pigSprite == null)
        {
            Debug.LogError("[FarmAnimalsSetup] Thiếu controller hoặc sprite; không thay đổi scene.");
            return;
        }

        GameObject farmChickenObject = FindInScene(scene, "Farm_Chicken");
        GameObject pigFarm = FindInScene(scene, "Farm_Pig");
        if (farmChickenObject == null || pigFarm == null)
        {
            Debug.LogError("[FarmAnimalsSetup] Không tìm thấy Farm_Chicken hoặc Farm_Pig.");
            return;
        }

        Transform oldChickenAnimals = farmChickenObject.transform.Find("Animals");
        if (oldChickenAnimals != null) Object.DestroyImmediate(oldChickenAnimals.gameObject);
        GameObject oldPigHerd = FindInScene(scene, "Pig Herd");
        if (oldPigHerd != null) Object.DestroyImmediate(oldPigHerd);

        Transform chickenAnimals = CreateGroup("Animals", farmChickenObject.transform);
        Transform hens = CreateGroup("Hens", chickenAnimals);
        Transform chicks = CreateGroup("Chicks", chickenAnimals);
        Transform roosters = CreateGroup("Roosters", chickenAnimals);

        Transform[] createdHens = new Transform[5];
        for (int i = 0; i < createdHens.Length; i++)
        {
            GameObject hen = CreateAnimal($"Hen {i + 1}", hens, HenPositions[i], Vector3.one * 0.95f,
                henSprite, henController);
            createdHens[i] = hen.transform;
            FarmAnimalWanderer wanderer = hen.AddComponent<FarmAnimalWanderer>();
            wanderer.Configure(ChickenMinimum, ChickenMaximum, 1.05f, 0.85f,
                "Hen_Walk_Left", "Hen_Walk_Right", "Hen_Eat");
        }

        // Only two of the five hens have chicks: four follow Hen 1 and three follow Hen 2.
        CreateChickFamily(chicks, createdHens[0], 1, 4, chickSprite, chickController);
        CreateChickFamily(chicks, createdHens[1], 2, 3, chickSprite, chickController);

        for (int i = 0; i < RoosterPositions.Length; i++)
        {
            GameObject rooster = CreateAnimal($"Rooster {i + 1}", roosters, RoosterPositions[i], Vector3.one * 1.18f,
                roosterSprite, roosterController);
            FarmAnimalWanderer wanderer = rooster.AddComponent<FarmAnimalWanderer>();
            wanderer.Configure(ChickenMinimum, ChickenMaximum, 1.15f, 1.0f,
                "Rooster_Walk_Left", "Rooster_Walk_Right", "Rooster_Eat");
        }

        Transform pigHerd = CreateGroup("Pig Herd", pigFarm.transform);
        for (int i = 0; i < PigPositions.Length; i++)
        {
            GameObject pig = CreateAnimal($"Pig {i + 1}", pigHerd, PigPositions[i], new Vector3(1.4f, 1.4f, 1f),
                pigSprite, pigController);
            FarmAnimalWanderer wanderer = pig.AddComponent<FarmAnimalWanderer>();
            wanderer.Configure(PigMinimum, PigMaximum, 1.15f, 1.1f,
                "Pig_Walk_Left", "Pig_Walk_Right", "Pig_Eat");
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = chickenAnimals.gameObject;
        Debug.Log("[FarmAnimalsSetup] Hoàn tất: 5 gà mái di chuyển, 7 gà con, 2 gà trống và 5 heo trong Farm_Pig.");
    }

    private static void CreateChickFamily(
        Transform chickGroup,
        Transform mother,
        int familyNumber,
        int count,
        Sprite sprite,
        RuntimeAnimatorController controller)
    {
        Vector2[] offsets =
        {
            new Vector2(-0.75f, -0.65f),
            new Vector2(0.05f, -0.9f),
            new Vector2(0.8f, -0.55f),
            new Vector2(-0.2f, -1.35f)
        };

        for (int i = 0; i < count; i++)
        {
            Vector2 motherLocal = chickGroup.InverseTransformPoint(mother.position);
            Vector3 start = motherLocal + offsets[i];
            GameObject chick = CreateAnimal($"Chick {familyNumber}-{i + 1}", chickGroup, start,
                Vector3.one * 0.55f, sprite, controller);
            ChickFollower follower = chick.AddComponent<ChickFollower>();
            follower.Configure(mother, offsets[i], ChickenMinimum, ChickenMaximum);
        }
    }

    private static GameObject CreateAnimal(
        string name,
        Transform parent,
        Vector3 localPosition,
        Vector3 localScale,
        Sprite sprite,
        RuntimeAnimatorController controller)
    {
        GameObject animal = CreateSpriteObject(name, parent, localPosition, localScale, sprite);
        Animator animator = animal.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        return animal;
    }

    private static GameObject CreateSpriteObject(
        string name,
        Transform parent,
        Vector3 localPosition,
        Vector3 localScale,
        Sprite sprite)
    {
        GameObject result = new GameObject(name);
        result.transform.SetParent(parent, false);
        result.transform.localPosition = localPosition;
        result.transform.localScale = localScale;
        SpriteRenderer renderer = result.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.spriteSortPoint = SpriteSortPoint.Pivot;
        result.AddComponent<DynamicYSorter>();
        return result;
    }

    private static Transform CreateGroup(string name, Transform parent)
    {
        GameObject group = new GameObject(name);
        group.transform.SetParent(parent, false);
        return group.transform;
    }

    private static GameObject FindInScene(Scene scene, string objectName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform match = root.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(item => item.name == objectName);
            if (match != null) return match.gameObject;
        }

        return null;
    }

    private static AnimatorController CreateController(
        string controllerPath,
        string prefix,
        string walkSheet,
        string eatSheet,
        bool createSitState)
    {
        Sprite[] walkFrames = Frames(walkSheet);
        Sprite[] eatFrames = Frames(eatSheet);
        if (walkFrames.Length == 0 || eatFrames.Length == 0)
        {
            Debug.LogError($"[FarmAnimalsSetup] Sprite sheet của {prefix} chưa được slice.");
            return null;
        }

        string folder = controllerPath.Substring(0, controllerPath.LastIndexOf('/'));
        AnimationClip left = CreateOrUpdateClip(folder + $"/{prefix}_Walk_Left.anim", walkFrames, 0.1f, true);
        AnimationClip right = CreateOrUpdateClip(folder + $"/{prefix}_Walk_Right.anim", walkFrames, 0.1f, true);
        AnimationClip eat = CreateOrUpdateClip(folder + $"/{prefix}_Eat.anim", eatFrames, 0.1f, true);
        AnimationClip sit = createSitState
            ? CreateOrUpdateClip(folder + $"/{prefix}_Sit.anim", new[] { walkFrames[0] }, 1f, true)
            : null;

        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);

        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        AnimatorState rightState = EnsureState(stateMachine, prefix + "_Walk_Right", right, new Vector3(220f, 0f));
        EnsureState(stateMachine, prefix + "_Walk_Left", left, new Vector3(220f, 70f));
        EnsureState(stateMachine, prefix + "_Eat", eat, new Vector3(450f, 0f));
        if (sit != null)
        {
            EnsureState(stateMachine, prefix + "_Sit", sit, new Vector3(450f, 90f));
        }
        else
        {
            AnimatorState staleSitState = stateMachine.states.Select(item => item.state)
                .FirstOrDefault(item => item.name == prefix + "_Sit");
            if (staleSitState != null) stateMachine.RemoveState(staleSitState);

            string staleSitPath = folder + $"/{prefix}_Sit.anim";
            if (AssetDatabase.LoadAssetAtPath<AnimationClip>(staleSitPath) != null)
                AssetDatabase.DeleteAsset(staleSitPath);
        }
        stateMachine.defaultState = rightState;
        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static AnimationClip CreateOrUpdateClip(string path, Sprite[] frames, float frameTime, bool loop)
    {
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null)
        {
            clip = new AnimationClip();
            AssetDatabase.CreateAsset(clip, path);
        }

        ObjectReferenceKeyframe[] keyframes = new ObjectReferenceKeyframe[frames.Length];
        for (int i = 0; i < frames.Length; i++)
        {
            keyframes[i] = new ObjectReferenceKeyframe { time = i * frameTime, value = frames[i] };
        }

        AnimationUtility.SetObjectReferenceCurve(clip, new EditorCurveBinding
        {
            path = string.Empty,
            type = typeof(SpriteRenderer),
            propertyName = "m_Sprite"
        }, keyframes);

        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        clip.frameRate = 1f / frameTime;
        EditorUtility.SetDirty(clip);
        return clip;
    }

    private static AnimatorState EnsureState(
        AnimatorStateMachine stateMachine,
        string stateName,
        Motion motion,
        Vector3 position)
    {
        AnimatorState state = stateMachine.states.Select(item => item.state)
            .FirstOrDefault(item => item.name == stateName);
        if (state == null) state = stateMachine.AddState(stateName, position);
        state.motion = motion;
        return state;
    }

    private static Sprite[] Frames(string path)
    {
        return AssetDatabase.LoadAllAssetRepresentationsAtPath(path)
            .OfType<Sprite>()
            .OrderBy(sprite => FrameNumber(sprite.name))
            .ToArray();
    }

    private static Sprite FirstFrame(string path)
    {
        return Frames(path).FirstOrDefault();
    }

    private static int FrameNumber(string spriteName)
    {
        int underscore = spriteName.LastIndexOf('_');
        return underscore >= 0 && int.TryParse(spriteName.Substring(underscore + 1), out int frame)
            ? frame
            : int.MaxValue;
    }

    private static void ConfigurePixelsPerUnit(string texturePath, float pixelsPerUnit)
    {
        TextureImporter importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
        if (importer == null || Mathf.Approximately(importer.spritePixelsPerUnit, pixelsPerUnit)) return;

        importer.spritePixelsPerUnit = pixelsPerUnit;
        importer.SaveAndReimport();
    }

}
#endif
