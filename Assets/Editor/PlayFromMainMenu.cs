using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>Starts Editor Play mode at the opening menu, regardless of the scene being edited.</summary>
[InitializeOnLoad]
internal static class PlayFromMainMenu
{
    private const string MenuPath = "Assets/Scenes/MainMenu.unity";

    static PlayFromMainMenu()
    {
        EditorApplication.delayCall += SetStartScene;
    }

    private static void SetStartScene()
    {
        SceneAsset menu = AssetDatabase.LoadAssetAtPath<SceneAsset>(MenuPath);
        if (menu != null)
            EditorSceneManager.playModeStartScene = menu;
    }
}
