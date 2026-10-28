using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Quest Window UI script displaying player quests, objectives, progress, and rewards.
/// Automatically creates a responsive uGUI Canvas if none is assigned in the Inspector.
/// Hotkey: 'Q' to toggle open/close.
/// </summary>
public class QuestWindowUI : MonoBehaviour
{
    public static QuestWindowUI Instance { get; private set; }

    [Header("UI Roots")]
    [SerializeField] private GameObject windowRoot;
    [SerializeField] private Transform questListContainer;
    [SerializeField] private GameObject questItemPrefab;

    [Header("Detail Panel")]
    [SerializeField] private Text titleText;
    [SerializeField] private Text categoryText;
    [SerializeField] private Text descriptionText;
    [SerializeField] private Text objectiveText;
    [SerializeField] private Slider progressBar;
    [SerializeField] private Text progressPercentageText;
    [SerializeField] private Text rewardGoldText;
    [SerializeField] private Text rewardExpText;
    [SerializeField] private Text rewardItemText;
    [SerializeField] private Button claimRewardButton;
    [SerializeField] private Text claimButtonText;

    [Header("HUD Mini Tracker (Optional)")]
    [SerializeField] private GameObject hudTrackerPanel;
    [SerializeField] private Text hudTrackerText;

    [Header("Settings")]
    [SerializeField] private KeyCode toggleKey = KeyCode.Q;
    [SerializeField] private bool lockMovementWhenOpen = true;

    private readonly List<GameObject> spawnedItems = new List<GameObject>();
    private Quest selectedQuest;
    private int activeFilter = 0; // 0 = Tất Cả, 1 = Đang Làm, 2 = Hoàn Thành
    private Font defaultFont;

    public static bool IsOpen => Instance != null && Instance.windowRoot != null && Instance.windowRoot.activeSelf;

    public void Configure(GameObject root, Transform listContainer, Text title, Text category, Text desc, Text obj, Slider pb, Text pbPct, Text gold, Text exp, Text item, Button claimBtn, Text claimTxt, GameObject hudPanel, Text hudTxt)
    {
        windowRoot = root;
        questListContainer = listContainer;
        titleText = title;
        categoryText = category;
        descriptionText = desc;
        objectiveText = obj;
        progressBar = pb;
        progressPercentageText = pbPct;
        rewardGoldText = gold;
        rewardExpText = exp;
        rewardItemText = item;
        claimRewardButton = claimBtn;
        claimButtonText = claimTxt;
        hudTrackerPanel = hudPanel;
        hudTrackerText = hudTxt;
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        // Ensure QuestManager exists
        if (QuestManager.Instance == null)
        {
            gameObject.AddComponent<QuestManager>();
        }

        // Load runtime font
        defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (defaultFont == null)
        {
            defaultFont = Font.CreateDynamicFontFromOSFont("Arial", 14);
        }

        // Procedurally construct UI if not assigned in Inspector
        if (windowRoot == null)
        {
            BuildProceduralUI();
        }

        if (windowRoot != null)
        {
            windowRoot.SetActive(false);
        }
    }

    private void Start()
    {
        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.OnQuestListChanged += RefreshUI;
            QuestManager.Instance.OnQuestUpdated += OnQuestUpdated;
            QuestManager.Instance.OnQuestCompleted += OnQuestCompleted;
        }

        RefreshUI();
        UpdateHUDTracker();
    }

    private void OnDisable()
    {
        if (lockMovementWhenOpen)
        {
            MovementLock.Unlock(this);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;

        if (lockMovementWhenOpen)
        {
            MovementLock.Unlock(this);
        }

        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.OnQuestListChanged -= RefreshUI;
            QuestManager.Instance.OnQuestUpdated -= OnQuestUpdated;
            QuestManager.Instance.OnQuestCompleted -= OnQuestCompleted;
        }
    }

    private void Update()
    {
        bool togglePressed = false;
        bool escapePressed = false;

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            togglePressed = Keyboard.current.qKey.wasPressedThisFrame;
            escapePressed = Keyboard.current.escapeKey.wasPressedThisFrame;
        }
#endif
        if (!togglePressed)
        {
            try { togglePressed = Input.GetKeyDown(toggleKey); } catch { }
        }
        if (!escapePressed)
        {
            try { escapePressed = Input.GetKeyDown(KeyCode.Escape); } catch { }
        }

        if (togglePressed)
        {
            SetOpen(!IsOpen);
        }
        else if (escapePressed && IsOpen)
        {
            SetOpen(false);
        }
    }

    public void ToggleWindow()
    {
        SetOpen(!IsOpen);
    }

    public void SetOpen(bool open)
    {
        if (windowRoot == null) return;

        windowRoot.SetActive(open);

        if (open)
        {
            RefreshUI();
            if (lockMovementWhenOpen)
            {
                MovementLock.Lock(this);
            }
        }
        else
        {
            if (lockMovementWhenOpen)
            {
                MovementLock.Unlock(this);
            }
        }
    }

    public void SetFilter(int filterIndex)
    {
        activeFilter = filterIndex;
        RefreshUI();
    }

    private void OnQuestUpdated(Quest quest)
    {
        UpdateHUDTracker();
        if (IsOpen) RefreshUI();
    }

    private void OnQuestCompleted(Quest quest)
    {
        UpdateHUDTracker();
        if (IsOpen) RefreshUI();
    }

    public void RefreshUI()
    {
        if (QuestManager.Instance == null || questListContainer == null) return;

        // Clear existing spawned items
        for (int i = 0; i < spawnedItems.Count; i++)
        {
            if (spawnedItems[i] != null) Destroy(spawnedItems[i]);
        }
        spawnedItems.Clear();

        IReadOnlyList<Quest> allQuests = QuestManager.Instance.AllQuests;
        List<Quest> filtered = new List<Quest>();

        for (int i = 0; i < allQuests.Count; i++)
        {
            Quest q = allQuests[i];
            if (activeFilter == 0) filtered.Add(q);
            else if (activeFilter == 1 && q.status == QuestStatus.InProgress) filtered.Add(q);
            else if (activeFilter == 2 && (q.status == QuestStatus.Completed || q.status == QuestStatus.Claimed)) filtered.Add(q);
        }

        // Spawn quest list buttons
        for (int i = 0; i < filtered.Count; i++)
        {
            Quest q = filtered[i];
            GameObject itemGO = CreateQuestListItem(q);
            spawnedItems.Add(itemGO);
        }

        // Maintain selection
        if (selectedQuest == null || !filtered.Contains(selectedQuest))
        {
            selectedQuest = filtered.Count > 0 ? filtered[0] : null;
        }

        DisplayQuestDetail(selectedQuest);
        UpdateHUDTracker();
    }

    private GameObject CreateQuestListItem(Quest quest)
    {
        GameObject itemGO = new GameObject($"QuestItem_{quest.id}");
        itemGO.transform.SetParent(questListContainer, false);

        RectTransform rt = itemGO.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(280f, 64f);

        Image bg = itemGO.AddComponent<Image>();
        bool isSelected = (selectedQuest != null && selectedQuest.id == quest.id);
        bg.color = isSelected ? new Color(0.24f, 0.32f, 0.48f, 0.95f) : new Color(0.12f, 0.15f, 0.22f, 0.85f);

        Outline outline = itemGO.AddComponent<Outline>();
        outline.effectColor = isSelected ? new Color(1f, 0.84f, 0.35f, 0.9f) : new Color(0.25f, 0.30f, 0.42f, 0.6f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        Button btn = itemGO.AddComponent<Button>();
        ColorBlock cb = btn.colors;
        cb.highlightedColor = new Color(0.28f, 0.38f, 0.56f, 1f);
        cb.pressedColor = new Color(0.18f, 0.24f, 0.36f, 1f);
        btn.colors = cb;

        // Status badge icon text
        GameObject iconGO = new GameObject("StatusIcon");
        iconGO.transform.SetParent(itemGO.transform, false);
        RectTransform iconRT = iconGO.AddComponent<RectTransform>();
        iconRT.anchorMin = new Vector2(0f, 0.5f);
        iconRT.anchorMax = new Vector2(0f, 0.5f);
        iconRT.pivot = new Vector2(0f, 0.5f);
        iconRT.anchoredPosition = new Vector2(10f, 0f);
        iconRT.sizeDelta = new Vector2(30f, 30f);

        Text iconTxt = iconGO.AddComponent<Text>();
        iconTxt.font = defaultFont;
        iconTxt.fontSize = 18;
        iconTxt.alignment = TextAnchor.MiddleCenter;

        if (quest.status == QuestStatus.Claimed)
        {
            iconTxt.text = "✔";
            iconTxt.color = new Color(0.55f, 0.65f, 0.75f, 1f);
        }
        else if (quest.status == QuestStatus.Completed)
        {
            iconTxt.text = "🎁";
            iconTxt.color = new Color(1f, 0.85f, 0.2f, 1f);
        }
        else
        {
            iconTxt.text = "⏳";
            iconTxt.color = new Color(0.38f, 0.78f, 1f, 1f);
        }

        // Title and Category Text
        GameObject labelGO = new GameObject("Label");
        labelGO.transform.SetParent(itemGO.transform, false);
        RectTransform labelRT = labelGO.AddComponent<RectTransform>();
        labelRT.anchorMin = new Vector2(0f, 0f);
        labelRT.anchorMax = new Vector2(1f, 1f);
        labelRT.offsetMin = new Vector2(46f, 6f);
        labelRT.offsetMax = new Vector2(-10f, -6f);

        Text labelTxt = labelGO.AddComponent<Text>();
        labelTxt.font = defaultFont;
        labelTxt.fontSize = 13;
        labelTxt.lineSpacing = 1.1f;
        labelTxt.alignment = TextAnchor.MiddleLeft;

        string categoryPrefix = quest.category == QuestCategory.Main ? "<color=#FFD54F>[Chính]</color>" :
                                quest.category == QuestCategory.Daily ? "<color=#FF8A80>[Hàng Ngày]</color>" : "<color=#80D8FF>[Phụ]</color>";

        labelTxt.text = $"{categoryPrefix} <b>{quest.title}</b>\n<size=11><color=#B0BEC5>Tiến độ: {quest.ProgressText}</color></size>";

        btn.onClick.AddListener(() =>
        {
            selectedQuest = quest;
            RefreshUI();
        });

        return itemGO;
    }

    private void DisplayQuestDetail(Quest quest)
    {
        if (quest == null)
        {
            if (titleText != null) titleText.text = "Không có nhiệm vụ nào được chọn";
            if (categoryText != null) categoryText.text = "";
            if (descriptionText != null) descriptionText.text = "Hãy nhận thêm nhiệm vụ từ dân làng để tiếp tục hành trình trên đảo Pearl Island!";
            if (objectiveText != null) objectiveText.text = "";
            if (progressBar != null) progressBar.value = 0f;
            if (progressPercentageText != null) progressPercentageText.text = "0%";
            if (rewardGoldText != null) rewardGoldText.text = "0 Vàng";
            if (rewardExpText != null) rewardExpText.text = "0 EXP";
            if (rewardItemText != null) rewardItemText.text = "Không có";
            if (claimRewardButton != null) claimRewardButton.gameObject.SetActive(false);
            return;
        }

        if (titleText != null) titleText.text = quest.title;

        if (categoryText != null)
        {
            string cat = quest.category == QuestCategory.Main ? "Nhiệm Vụ Chính Tuyến" :
                         quest.category == QuestCategory.Daily ? "Nhiệm Vụ Hàng Ngày" : "Nhiệm Vụ Phụ Tuyến";

            string status = quest.status == QuestStatus.Claimed ? "<color=#81C784>[ĐÃ HOÀN TẤT & NHẬN THƯỞNG]</color>" :
                            quest.status == QuestStatus.Completed ? "<color=#FFD54F>[ĐÃ HOÀN THÀNH - CHỜ NHẬN THƯỞNG]</color>" :
                            "<color=#64B5F6>[ĐANG THỰC HIỆN]</color>";

            categoryText.text = $"{cat} • {status}";
        }

        if (descriptionText != null) descriptionText.text = quest.description;

        if (objectiveText != null)
        {
            objectiveText.text = $"Mục tiêu: Đạt tiến độ yêu cầu ({quest.ProgressText})";
        }

        if (progressBar != null) progressBar.value = quest.ProgressNormalized;

        if (progressPercentageText != null)
        {
            int percent = Mathf.RoundToInt(quest.ProgressNormalized * 100f);
            progressPercentageText.text = $"{quest.ProgressText} ({percent}%)";
        }

        if (rewardGoldText != null) rewardGoldText.text = $"+{quest.reward.gold} Vàng";
        if (rewardExpText != null) rewardExpText.text = $"+{quest.reward.exp} EXP";
        if (rewardItemText != null)
        {
            rewardItemText.text = !string.IsNullOrEmpty(quest.reward.itemName) ?
                $"+{quest.reward.itemAmount} {quest.reward.itemName}" : "Không có";
        }

        if (claimRewardButton != null)
        {
            claimRewardButton.gameObject.SetActive(true);

            if (quest.status == QuestStatus.Completed)
            {
                claimRewardButton.interactable = true;
                if (claimButtonText != null) claimButtonText.text = "🎁 NHẬN PHẦN THƯỞNG";
            }
            else if (quest.status == QuestStatus.Claimed)
            {
                claimRewardButton.interactable = false;
                if (claimButtonText != null) claimButtonText.text = "✔ ĐÃ NHẬN THƯỞNG";
            }
            else
            {
                claimRewardButton.interactable = false;
                if (claimButtonText != null) claimButtonText.text = "CHƯA HOÀN THÀNH";
            }

            claimRewardButton.onClick.RemoveAllListeners();
            claimRewardButton.onClick.AddListener(() =>
            {
                if (QuestManager.Instance != null && quest.status == QuestStatus.Completed)
                {
                    QuestManager.Instance.ClaimReward(quest.id);
                }
            });
        }
    }

    private void UpdateHUDTracker()
    {
        if (hudTrackerText == null || QuestManager.Instance == null) return;

        IReadOnlyList<Quest> all = QuestManager.Instance.AllQuests;
        Quest activeOne = null;
        for (int i = 0; i < all.Count; i++)
        {
            if (all[i].status == QuestStatus.InProgress || all[i].status == QuestStatus.Completed)
            {
                activeOne = all[i];
                break;
            }
        }

        if (activeOne != null)
        {
            if (hudTrackerPanel != null) hudTrackerPanel.SetActive(true);
            string statusIcon = activeOne.IsCompleted ? "🎁" : "📌";
            hudTrackerText.text = $"<b>{statusIcon} {activeOne.title}</b>\n<color=#FFD54F>Tiến độ: {activeOne.ProgressText}</color>";
        }
        else
        {
            if (hudTrackerPanel != null) hudTrackerPanel.SetActive(false);
        }
    }

    #region Procedural UI Builder
    private void BuildProceduralUI()
    {
        // 1. Root Canvas
        GameObject canvasGO = new GameObject("QuestCanvas");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 40;

        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();

        // 2. Window Root (Center Modal)
        windowRoot = new GameObject("QuestWindowModal");
        windowRoot.transform.SetParent(canvasGO.transform, false);

        RectTransform modalRT = windowRoot.AddComponent<RectTransform>();
        modalRT.anchorMin = new Vector2(0.5f, 0.5f);
        modalRT.anchorMax = new Vector2(0.5f, 0.5f);
        modalRT.pivot = new Vector2(0.5f, 0.5f);
        modalRT.sizeDelta = new Vector2(900f, 560f);

        Image modalBg = windowRoot.AddComponent<Image>();
        modalBg.color = new Color(0.08f, 0.10f, 0.16f, 0.96f); // Dark fantasy glass

        Outline modalOutline = windowRoot.AddComponent<Outline>();
        modalOutline.effectColor = new Color(0.85f, 0.72f, 0.38f, 1f); // Ornate Gold
        modalOutline.effectDistance = new Vector2(2.5f, -2.5f);

        Shadow modalShadow = windowRoot.AddComponent<Shadow>();
        modalShadow.effectColor = new Color(0f, 0f, 0f, 0.75f);
        modalShadow.effectDistance = new Vector2(5f, -5f);

        // Header
        GameObject headerGO = new GameObject("Header");
        headerGO.transform.SetParent(windowRoot.transform, false);
        RectTransform headerRT = headerGO.AddComponent<RectTransform>();
        headerRT.anchorMin = new Vector2(0f, 1f);
        headerRT.anchorMax = new Vector2(1f, 1f);
        headerRT.pivot = new Vector2(0.5f, 1f);
        headerRT.sizeDelta = new Vector2(0f, 50f);

        Image headerBg = headerGO.AddComponent<Image>();
        headerBg.color = new Color(0.14f, 0.18f, 0.28f, 1f);

        GameObject headerTitleGO = new GameObject("Title");
        headerTitleGO.transform.SetParent(headerGO.transform, false);
        RectTransform htRT = headerTitleGO.AddComponent<RectTransform>();
        htRT.anchorMin = new Vector2(0f, 0f);
        htRT.anchorMax = new Vector2(0.5f, 1f);
        htRT.offsetMin = new Vector2(20f, 0f);

        Text headerTitle = headerTitleGO.AddComponent<Text>();
        headerTitle.font = defaultFont;
        headerTitle.fontSize = 20;
        headerTitle.fontStyle = FontStyle.Bold;
        headerTitle.alignment = TextAnchor.MiddleLeft;
        headerTitle.color = new Color(1f, 0.90f, 0.55f, 1f);
        headerTitle.text = "📜 BẢNG NHIỆM VỤ / QUEST LOG";

        // Close Button [X]
        GameObject closeGO = new GameObject("CloseButton");
        closeGO.transform.SetParent(headerGO.transform, false);
        RectTransform closeRT = closeGO.AddComponent<RectTransform>();
        closeRT.anchorMin = new Vector2(1f, 0.5f);
        closeRT.anchorMax = new Vector2(1f, 0.5f);
        closeRT.pivot = new Vector2(1f, 0.5f);
        closeRT.anchoredPosition = new Vector2(-12f, 0f);
        closeRT.sizeDelta = new Vector2(36f, 36f);

        Image closeBg = closeGO.AddComponent<Image>();
        closeBg.color = new Color(0.75f, 0.22f, 0.22f, 1f);

        Button closeBtn = closeGO.AddComponent<Button>();
        closeBtn.onClick.AddListener(() => SetOpen(false));

        GameObject closeTxtGO = new GameObject("Text");
        closeTxtGO.transform.SetParent(closeGO.transform, false);
        RectTransform closeTxtRT = closeTxtGO.AddComponent<RectTransform>();
        closeTxtRT.anchorMin = Vector2.zero;
        closeTxtRT.anchorMax = Vector2.one;
        closeTxtRT.offsetMin = Vector2.zero;
        closeTxtRT.offsetMax = Vector2.zero;

        Text closeTxt = closeTxtGO.AddComponent<Text>();
        closeTxt.font = defaultFont;
        closeTxt.text = "✕";
        closeTxt.fontSize = 18;
        closeTxt.alignment = TextAnchor.MiddleCenter;
        closeTxt.color = Color.white;

        // Filter Tabs [Tất Cả] [Đang Làm] [Hoàn Thành]
        string[] tabs = new string[] { "Tất Cả", "Đang Làm", "Hoàn Thành" };
        for (int i = 0; i < tabs.Length; i++)
        {
            int tabIndex = i;
            GameObject tabGO = new GameObject($"Tab_{tabs[i]}");
            tabGO.transform.SetParent(headerGO.transform, false);
            RectTransform tabRT = tabGO.AddComponent<RectTransform>();
            tabRT.anchorMin = new Vector2(0.5f, 0.5f);
            tabRT.anchorMax = new Vector2(0.5f, 0.5f);
            tabRT.pivot = new Vector2(0.5f, 0.5f);
            tabRT.anchoredPosition = new Vector2(-30f + i * 110f, 0f);
            tabRT.sizeDelta = new Vector2(100f, 32f);

            Image tabImg = tabGO.AddComponent<Image>();
            tabImg.color = new Color(0.20f, 0.26f, 0.38f, 1f);

            Button tabBtn = tabGO.AddComponent<Button>();
            tabBtn.onClick.AddListener(() => SetFilter(tabIndex));

            GameObject tabTxtGO = new GameObject("Text");
            tabTxtGO.transform.SetParent(tabGO.transform, false);
            RectTransform tabTxtRT = tabTxtGO.AddComponent<RectTransform>();
            tabTxtRT.anchorMin = Vector2.zero;
            tabTxtRT.anchorMax = Vector2.one;
            tabTxtRT.offsetMin = Vector2.zero;
            tabTxtRT.offsetMax = Vector2.zero;

            Text tabTxt = tabTxtGO.AddComponent<Text>();
            tabTxt.font = defaultFont;
            tabTxt.text = tabs[i];
            tabTxt.fontSize = 13;
            tabTxt.alignment = TextAnchor.MiddleCenter;
            tabTxt.color = new Color(0.9f, 0.95f, 1f, 1f);
        }

        // Left Panel: Quest List Container
        GameObject leftPanelGO = new GameObject("LeftQuestListPanel");
        leftPanelGO.transform.SetParent(windowRoot.transform, false);
        RectTransform lpRT = leftPanelGO.AddComponent<RectTransform>();
        lpRT.anchorMin = new Vector2(0f, 0f);
        lpRT.anchorMax = new Vector2(0.36f, 1f);
        lpRT.offsetMin = new Vector2(14f, 14f);
        lpRT.offsetMax = new Vector2(-6f, -60f);

        Image lpBg = leftPanelGO.AddComponent<Image>();
        lpBg.color = new Color(0.05f, 0.07f, 0.11f, 0.85f);

        // ScrollRect
        ScrollRect scrollRect = leftPanelGO.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;

        GameObject contentGO = new GameObject("Content");
        contentGO.transform.SetParent(leftPanelGO.transform, false);
        RectTransform contentRT = contentGO.AddComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0f, 1f);
        contentRT.anchorMax = new Vector2(1f, 1f);
        contentRT.pivot = new Vector2(0.5f, 1f);
        contentRT.sizeDelta = new Vector2(0f, 600f);

        VerticalLayoutGroup vlg = contentGO.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 8f;
        vlg.padding = new RectOffset(6, 6, 8, 8);
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;

        ContentSizeFitter csf = contentGO.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.content = contentRT;
        questListContainer = contentGO.transform;

        // Right Panel: Quest Details
        GameObject rightPanelGO = new GameObject("RightQuestDetailPanel");
        rightPanelGO.transform.SetParent(windowRoot.transform, false);
        RectTransform rpRT = rightPanelGO.AddComponent<RectTransform>();
        rpRT.anchorMin = new Vector2(0.36f, 0f);
        rpRT.anchorMax = new Vector2(1f, 1f);
        rpRT.offsetMin = new Vector2(6f, 14f);
        rpRT.offsetMax = new Vector2(-14f, -60f);

        Image rpBg = rightPanelGO.AddComponent<Image>();
        rpBg.color = new Color(0.06f, 0.08f, 0.13f, 0.85f);

        // Right Detail Elements
        // Title
        GameObject detTitleGO = new GameObject("DetailTitle");
        detTitleGO.transform.SetParent(rightPanelGO.transform, false);
        RectTransform dtRT = detTitleGO.AddComponent<RectTransform>();
        dtRT.anchorMin = new Vector2(0f, 1f);
        dtRT.anchorMax = new Vector2(1f, 1f);
        dtRT.pivot = new Vector2(0f, 1f);
        dtRT.anchoredPosition = new Vector2(20f, -16f);
        dtRT.sizeDelta = new Vector2(-40f, 32f);

        titleText = detTitleGO.AddComponent<Text>();
        titleText.font = defaultFont;
        titleText.fontSize = 20;
        titleText.fontStyle = FontStyle.Bold;
        titleText.color = new Color(1f, 0.88f, 0.45f, 1f);
        titleText.text = "Tên Nhiệm Vụ";

        // Category & Status
        GameObject detCatGO = new GameObject("DetailCategory");
        detCatGO.transform.SetParent(rightPanelGO.transform, false);
        RectTransform dcRT = detCatGO.AddComponent<RectTransform>();
        dcRT.anchorMin = new Vector2(0f, 1f);
        dcRT.anchorMax = new Vector2(1f, 1f);
        dcRT.pivot = new Vector2(0f, 1f);
        dcRT.anchoredPosition = new Vector2(20f, -50f);
        dcRT.sizeDelta = new Vector2(-40f, 22f);

        categoryText = detCatGO.AddComponent<Text>();
        categoryText.font = defaultFont;
        categoryText.fontSize = 12;
        categoryText.color = new Color(0.68f, 0.78f, 0.90f, 1f);
        categoryText.text = "Nhiệm vụ chính tuyến";

        // Divider
        GameObject divGO = new GameObject("Divider");
        divGO.transform.SetParent(rightPanelGO.transform, false);
        RectTransform divRT = divGO.AddComponent<RectTransform>();
        divRT.anchorMin = new Vector2(0f, 1f);
        divRT.anchorMax = new Vector2(1f, 1f);
        divRT.anchoredPosition = new Vector2(0f, -76f);
        divRT.sizeDelta = new Vector2(-40f, 1.5f);
        Image divImg = divGO.AddComponent<Image>();
        divImg.color = new Color(0.25f, 0.32f, 0.45f, 0.8f);

        // Description Box
        GameObject descGO = new GameObject("Description");
        descGO.transform.SetParent(rightPanelGO.transform, false);
        RectTransform descRT = descGO.AddComponent<RectTransform>();
        descRT.anchorMin = new Vector2(0f, 1f);
        descRT.anchorMax = new Vector2(1f, 1f);
        descRT.pivot = new Vector2(0f, 1f);
        descRT.anchoredPosition = new Vector2(20f, -86f);
        descRT.sizeDelta = new Vector2(-40f, 95f);

        descriptionText = descGO.AddComponent<Text>();
        descriptionText.font = defaultFont;
        descriptionText.fontSize = 14;
        descriptionText.lineSpacing = 1.25f;
        descriptionText.color = new Color(0.88f, 0.92f, 0.98f, 1f);
        descriptionText.text = "Mô tả chi tiết nhiệm vụ...";

        // Objective Text
        GameObject objGO = new GameObject("Objective");
        objGO.transform.SetParent(rightPanelGO.transform, false);
        RectTransform objRT = objGO.AddComponent<RectTransform>();
        objRT.anchorMin = new Vector2(0f, 1f);
        objRT.anchorMax = new Vector2(1f, 1f);
        objRT.pivot = new Vector2(0f, 1f);
        objRT.anchoredPosition = new Vector2(20f, -190f);
        objRT.sizeDelta = new Vector2(-40f, 24f);

        objectiveText = objGO.AddComponent<Text>();
        objectiveText.font = defaultFont;
        objectiveText.fontSize = 14;
        objectiveText.fontStyle = FontStyle.Bold;
        objectiveText.color = new Color(0.4f, 0.82f, 1f, 1f);
        objectiveText.text = "Mục tiêu: Đạt tiến độ yêu cầu (0/5)";

        // Progress Bar
        GameObject pbGO = new GameObject("ProgressBar");
        pbGO.transform.SetParent(rightPanelGO.transform, false);
        RectTransform pbRT = pbGO.AddComponent<RectTransform>();
        pbRT.anchorMin = new Vector2(0f, 1f);
        pbRT.anchorMax = new Vector2(1f, 1f);
        pbRT.pivot = new Vector2(0f, 1f);
        pbRT.anchoredPosition = new Vector2(20f, -220f);
        pbRT.sizeDelta = new Vector2(-40f, 22f);

        Image pbBg = pbGO.AddComponent<Image>();
        pbBg.color = new Color(0.12f, 0.16f, 0.22f, 1f);

        GameObject fillArea = new GameObject("FillArea");
        fillArea.transform.SetParent(pbGO.transform, false);
        RectTransform faRT = fillArea.AddComponent<RectTransform>();
        faRT.anchorMin = Vector2.zero;
        faRT.anchorMax = Vector2.one;
        faRT.offsetMin = new Vector2(2f, 2f);
        faRT.offsetMax = new Vector2(-2f, -2f);

        GameObject fillGO = new GameObject("Fill");
        fillGO.transform.SetParent(fillArea.transform, false);
        RectTransform fillRT = fillGO.AddComponent<RectTransform>();
        fillRT.anchorMin = Vector2.zero;
        fillRT.anchorMax = new Vector2(0f, 1f);
        fillRT.sizeDelta = Vector2.zero;

        Image fillImg = fillGO.AddComponent<Image>();
        fillImg.color = new Color(0.18f, 0.76f, 0.45f, 1f); // Vibrant emerald green

        progressBar = pbGO.AddComponent<Slider>();
        progressBar.fillRect = fillRT;
        progressBar.minValue = 0f;
        progressBar.maxValue = 1f;
        progressBar.value = 0.5f;

        // Progress percentage text
        GameObject pctGO = new GameObject("PercentText");
        pctGO.transform.SetParent(pbGO.transform, false);
        RectTransform pctRT = pctGO.AddComponent<RectTransform>();
        pctRT.anchorMin = Vector2.zero;
        pctRT.anchorMax = Vector2.one;
        pctRT.offsetMin = Vector2.zero;
        pctRT.offsetMax = Vector2.zero;

        progressPercentageText = pctGO.AddComponent<Text>();
        progressPercentageText.font = defaultFont;
        progressPercentageText.fontSize = 12;
        progressPercentageText.fontStyle = FontStyle.Bold;
        progressPercentageText.alignment = TextAnchor.MiddleCenter;
        progressPercentageText.color = Color.white;
        progressPercentageText.text = "50%";

        // Reward Section Container
        GameObject rewBoxGO = new GameObject("RewardBox");
        rewBoxGO.transform.SetParent(rightPanelGO.transform, false);
        RectTransform rewRT = rewBoxGO.AddComponent<RectTransform>();
        rewRT.anchorMin = new Vector2(0f, 0f);
        rewRT.anchorMax = new Vector2(1f, 0f);
        rewRT.pivot = new Vector2(0.5f, 0f);
        rewRT.anchoredPosition = new Vector2(0f, 75f);
        rewRT.sizeDelta = new Vector2(-40f, 110f);

        Image rewBg = rewBoxGO.AddComponent<Image>();
        rewBg.color = new Color(0.10f, 0.13f, 0.20f, 0.9f);

        Outline rewOutline = rewBoxGO.AddComponent<Outline>();
        rewOutline.effectColor = new Color(0.35f, 0.45f, 0.60f, 0.6f);

        GameObject rewTitleGO = new GameObject("RewardTitle");
        rewTitleGO.transform.SetParent(rewBoxGO.transform, false);
        RectTransform rwtRT = rewTitleGO.AddComponent<RectTransform>();
        rwtRT.anchorMin = new Vector2(0f, 1f);
        rwtRT.anchorMax = new Vector2(1f, 1f);
        rwtRT.anchoredPosition = new Vector2(16f, -12f);
        rwtRT.sizeDelta = new Vector2(0f, 20f);

        Text rewTitle = rewTitleGO.AddComponent<Text>();
        rewTitle.font = defaultFont;
        rewTitle.fontSize = 13;
        rewTitle.fontStyle = FontStyle.Bold;
        rewTitle.color = new Color(1f, 0.85f, 0.3f, 1f);
        rewTitle.text = "🎁 PHẦN THƯỞNG HOÀN THÀNH:";

        // Gold reward
        GameObject goldGO = new GameObject("Gold");
        goldGO.transform.SetParent(rewBoxGO.transform, false);
        RectTransform goldRT = goldGO.AddComponent<RectTransform>();
        goldRT.anchoredPosition = new Vector2(20f, -48f);
        goldRT.sizeDelta = new Vector2(140f, 26f);
        rewardGoldText = goldGO.AddComponent<Text>();
        rewardGoldText.font = defaultFont;
        rewardGoldText.fontSize = 14;
        rewardGoldText.color = new Color(1f, 0.85f, 0.25f, 1f);
        rewardGoldText.text = "🪙 +150 Vàng";

        // Exp reward
        GameObject expGO = new GameObject("Exp");
        expGO.transform.SetParent(rewBoxGO.transform, false);
        RectTransform expRT = expGO.AddComponent<RectTransform>();
        expRT.anchoredPosition = new Vector2(170f, -48f);
        expRT.sizeDelta = new Vector2(140f, 26f);
        rewardExpText = expGO.AddComponent<Text>();
        rewardExpText.font = defaultFont;
        rewardExpText.fontSize = 14;
        rewardExpText.color = new Color(0.4f, 0.9f, 1f, 1f);
        rewardExpText.text = "⭐ +100 EXP";

        // Item reward
        GameObject itemRewGO = new GameObject("Item");
        itemRewGO.transform.SetParent(rewBoxGO.transform, false);
        RectTransform itemRewRT = itemRewGO.AddComponent<RectTransform>();
        itemRewRT.anchoredPosition = new Vector2(20f, -76f);
        itemRewRT.sizeDelta = new Vector2(280f, 26f);
        rewardItemText = itemRewGO.AddComponent<Text>();
        rewardItemText.font = defaultFont;
        rewardItemText.fontSize = 13;
        rewardItemText.color = new Color(0.85f, 0.70f, 1f, 1f);
        rewardItemText.text = "📦 +2 Gỗ Quý";

        // Claim Reward Button
        GameObject claimGO = new GameObject("ClaimRewardButton");
        claimGO.transform.SetParent(rightPanelGO.transform, false);
        RectTransform claimRT = claimGO.AddComponent<RectTransform>();
        claimRT.anchorMin = new Vector2(0.5f, 0f);
        claimRT.anchorMax = new Vector2(0.5f, 0f);
        claimRT.pivot = new Vector2(0.5f, 0f);
        claimRT.anchoredPosition = new Vector2(0f, 14f);
        claimRT.sizeDelta = new Vector2(260f, 44f);

        Image claimImg = claimGO.AddComponent<Image>();
        claimImg.color = new Color(0.95f, 0.70f, 0.18f, 1f);

        claimRewardButton = claimGO.AddComponent<Button>();
        ColorBlock ccb = claimRewardButton.colors;
        ccb.highlightedColor = new Color(1f, 0.82f, 0.35f, 1f);
        ccb.disabledColor = new Color(0.35f, 0.40f, 0.48f, 0.6f);
        claimRewardButton.colors = ccb;

        GameObject claimTxtGO = new GameObject("Text");
        claimTxtGO.transform.SetParent(claimGO.transform, false);
        RectTransform claimTxtRT = claimTxtGO.AddComponent<RectTransform>();
        claimTxtRT.anchorMin = Vector2.zero;
        claimTxtRT.anchorMax = Vector2.one;
        claimTxtRT.offsetMin = Vector2.zero;
        claimTxtRT.offsetMax = Vector2.zero;

        claimButtonText = claimTxtGO.AddComponent<Text>();
        claimButtonText.font = defaultFont;
        claimButtonText.fontSize = 15;
        claimButtonText.fontStyle = FontStyle.Bold;
        claimButtonText.alignment = TextAnchor.MiddleCenter;
        claimButtonText.color = new Color(0.12f, 0.10f, 0.05f, 1f);
        claimButtonText.text = "🎁 NHẬN PHẦN THƯỞNG";

        // 3. Floating HUD Tracker Widget (Top-Right)
        hudTrackerPanel = new GameObject("HUDQuestTracker");
        hudTrackerPanel.transform.SetParent(canvasGO.transform, false);
        RectTransform hudRT = hudTrackerPanel.AddComponent<RectTransform>();
        hudRT.anchorMin = new Vector2(1f, 1f);
        hudRT.anchorMax = new Vector2(1f, 1f);
        hudRT.pivot = new Vector2(1f, 1f);
        hudRT.anchoredPosition = new Vector2(-20f, -106f);
        hudRT.sizeDelta = new Vector2(300f, 72f);

        Image hudBg = hudTrackerPanel.AddComponent<Image>();
        hudBg.color = new Color(0.08f, 0.10f, 0.16f, 0.80f);

        Outline hudOutline = hudTrackerPanel.AddComponent<Outline>();
        hudOutline.effectColor = new Color(0.35f, 0.45f, 0.60f, 0.7f);

        GameObject hudTxtGO = new GameObject("Text");
        hudTxtGO.transform.SetParent(hudTrackerPanel.transform, false);
        RectTransform htTxtRT = hudTxtGO.AddComponent<RectTransform>();
        htTxtRT.anchorMin = Vector2.zero;
        htTxtRT.anchorMax = Vector2.one;
        htTxtRT.offsetMin = new Vector2(10f, 6f);
        htTxtRT.offsetMax = new Vector2(-10f, -6f);

        hudTrackerText = hudTxtGO.AddComponent<Text>();
        hudTrackerText.font = defaultFont;
        hudTrackerText.fontSize = 12;
        hudTrackerText.lineSpacing = 1.15f;
        hudTrackerText.alignment = TextAnchor.MiddleLeft;
        hudTrackerText.color = Color.white;
        hudTrackerText.text = "📌 <b>Nhiệm vụ:</b>\n<color=#FFD54F>Chưa có nhiệm vụ</color>";

        // Add Toggle button on HUD to easily open window with mouse
        Button hudBtn = hudTrackerPanel.AddComponent<Button>();
        hudBtn.onClick.AddListener(ToggleWindow);
    }
    #endregion
}
