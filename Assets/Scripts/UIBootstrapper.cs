using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Automatically bootstraps essential UI systems (Quest Window, Visual Novel Dialogue)
/// and collision fixes upon scene load, so no manual scene setup is required.
/// </summary>
public static class UIBootstrapper
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InitializeSystems()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "MainMenu") return;

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

        GameObject player = GameObject.FindWithTag("Player");
        if (Object.FindAnyObjectByType<CoinHud>() == null)
        {
            ShopCatalog coinCatalog = Resources.Load<ShopCatalog>("ShopCatalog");
            GameObject coinHud = new GameObject("Coin HUD");
            SceneManager.MoveGameObjectToScene(coinHud, scene);
            coinHud.AddComponent<CoinHud>().Configure(coinCatalog != null ? coinCatalog.coinIcon : null);
        }
        if (scene.name == "PixelLabMap" && Object.FindAnyObjectByType<ShopUI>() == null)
        {
            ShopCatalog catalog = Resources.Load<ShopCatalog>("ShopCatalog");
            SpriteRenderer houseSprite = null;
            foreach (SpriteRenderer candidate in Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (candidate.gameObject.scene == scene && candidate.name == "af323ed7-1897-4a35-b575-f101c414f08e_0")
                {
                    houseSprite = candidate;
                    break;
                }
            if (houseSprite != null && catalog != null)
            {
                GameObject shopObject = new GameObject("Village Shop");
                SceneManager.MoveGameObjectToScene(shopObject, scene);
                shopObject.AddComponent<ShopUI>().Configure(player != null ? player.transform : null, houseSprite, catalog);
            }
            else Debug.LogError("[Shop] Shop house sprite or ShopCatalog is missing.");
        }
        GameObject homeExit = GameObject.Find("Home_Exit");
        if (player != null && homeExit != null && player.GetComponent<PlayerDeathUI>() == null)
        {
            PlayerDeathUIConfig deathConfig = Resources.Load<PlayerDeathUIConfig>("PlayerDeathUIConfig");
            if (deathConfig != null && deathConfig.youDiedSprite != null && deathConfig.replaySprite != null)
            {
                PlayerDeathUI deathUI = player.AddComponent<PlayerDeathUI>();
                deathUI.Configure(deathConfig.youDiedSprite, deathConfig.replaySprite,
                    homeExit.transform, deathConfig.homeSpawnOffset);
            }
            else
            {
                Debug.LogError("[UIBootstrapper] PlayerDeathUIConfig or its sprites are missing.");
            }
        }

        Debug.Log("[UIBootstrapper] 🚀 Đã khởi tạo hoàn tất: Cửa sổ Nhiệm Vụ (Phím Q), Khung Chat Visual Novel, và Collision Setup!");
    }
}
