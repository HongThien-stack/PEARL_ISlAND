using UnityEngine;

/// <summary>
/// Automatically bootstraps essential UI systems (Quest Window, Visual Novel Dialogue)
/// and collision fixes upon scene load, so no manual scene setup is required.
/// </summary>
public static class UIBootstrapper
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InitializeSystems()
    {
        // 0. Ensure EventSystem exists for mouse clicks on UI
        if (Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>(FindObjectsInactive.Include) == null)
        {
            GameObject esGO = new GameObject("EventSystem");
            esGO.AddComponent<UnityEngine.EventSystems.EventSystem>();
#if ENABLE_INPUT_SYSTEM
            esGO.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            esGO.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
#endif
            Object.DontDestroyOnLoad(esGO);
            Debug.Log("[UIBootstrapper] 🖱️ Đã tạo EventSystem cho chuột tương tác UI!");
        }

        // 1. Ensure Quest Manager & Quest Window UI exist (skip if user already added to scene)
        if (QuestManager.Instance == null && Object.FindAnyObjectByType<QuestManager>(FindObjectsInactive.Include) == null)
        {
            GameObject questMgrGO = new GameObject("QuestManager");
            questMgrGO.AddComponent<QuestManager>();
            Object.DontDestroyOnLoad(questMgrGO);
        }

        if (QuestWindowUI.Instance == null && Object.FindAnyObjectByType<QuestWindowUI>(FindObjectsInactive.Include) == null)
        {
            GameObject questUIGO = new GameObject("QuestWindowUI");
            questUIGO.AddComponent<QuestWindowUI>();
            Object.DontDestroyOnLoad(questUIGO);
        }

        // 2. Ensure Visual Novel Dialogue System exists (skip if user already added to scene)
        if (VisualNovelDialogueUI.Instance == null && Object.FindAnyObjectByType<VisualNovelDialogueUI>(FindObjectsInactive.Include) == null)
        {
            GameObject vnUIGO = new GameObject("VisualNovelDialogueUI");
            vnUIGO.AddComponent<VisualNovelDialogueUI>();
            Object.DontDestroyOnLoad(vnUIGO);
        }

        // 3. Ensure CollisionSetupFixer runs
        if (Object.FindAnyObjectByType<CollisionSetupFixer>(FindObjectsInactive.Include) == null)
        {
            GameObject fixerGO = new GameObject("CollisionSetupFixer");
            fixerGO.AddComponent<CollisionSetupFixer>();
            Object.DontDestroyOnLoad(fixerGO);
        }

        Debug.Log("[UIBootstrapper] 🚀 Đã khởi tạo hoàn tất: Cửa sổ Nhiệm Vụ (Phím Q), Khung Chat Visual Novel, và Collision Setup!");
    }
}
