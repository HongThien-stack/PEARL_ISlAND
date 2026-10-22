using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Editor utility to generate sample QuestCanvas and VisualNovelCanvas
/// directly into the Hierarchy of PixelLabMap.
/// Allows the user to select any component and easily drag and drop their custom sprites.
/// </summary>
[InitializeOnLoad]
public static class QuestAndDialogueSetup
{
    private const string TargetScenePath = "Assets/PixelLabMap/Generated/PixelLabMap.unity";

    static QuestAndDialogueSetup()
    {
        EditorApplication.delayCall += EnsureSetupOnce;
    }

    private static void EnsureSetupOnce()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != TargetScenePath) return;

        bool hasQuest = UnityEngine.Object.FindAnyObjectByType<QuestWindowUI>(FindObjectsInactive.Include) != null;
        bool hasVN = UnityEngine.Object.FindAnyObjectByType<VisualNovelDialogueUI>(FindObjectsInactive.Include) != null;
        bool hasEvent = UnityEngine.Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>(FindObjectsInactive.Include) != null;

        if (!hasQuest || !hasVN || !hasEvent)
        {
            BuildUIElements(false);
        }
    }

    [MenuItem("Tools/Pearl Island/Tạo UI Mẫu Cho Nhiệm Vụ và Khung Chat")]
    public static void ManualBuild()
    {
        BuildUIElements(true);
    }

    public static void BuildUIElements(bool logSuccess)
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != TargetScenePath)
        {
            if (logSuccess) Debug.LogWarning($"Hãy mở scene {TargetScenePath} trước khi tạo UI mẫu.");
            return;
        }

        Font defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (defaultFont == null) defaultFont = Font.CreateDynamicFontFromOSFont("Arial", 14);

        // 0. Ensure EventSystem
        UnityEngine.EventSystems.EventSystem eventSystem = UnityEngine.Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>(FindObjectsInactive.Include);
        if (eventSystem == null)
        {
            GameObject esGO = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem));
#if ENABLE_INPUT_SYSTEM
            esGO.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            esGO.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
#endif
            Undo.RegisterCreatedObjectUndo(esGO, "Create EventSystem");
        }

        // 1. Build QuestCanvas
        QuestWindowUI questUI = UnityEngine.Object.FindAnyObjectByType<QuestWindowUI>(FindObjectsInactive.Include);
        if (questUI == null)
        {
            GameObject canvasGO = new GameObject("QuestCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(QuestWindowUI));
            Undo.RegisterCreatedObjectUndo(canvasGO, "Create QuestCanvas");
            questUI = canvasGO.GetComponent<QuestWindowUI>();

            Canvas canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 40;

            CanvasScaler scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 0.5f;

            // Modal Root
            GameObject modalGO = MakePanel(canvasGO.transform, "QuestWindowModal", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760, 480), new Color(0.08f, 0.10f, 0.16f, 0.98f));
            Outline modalOutline = modalGO.AddComponent<Outline>();
            modalOutline.effectColor = new Color(0.85f, 0.72f, 0.38f, 1f);
            modalOutline.effectDistance = new Vector2(2f, -2f);

            // Header
            GameObject headerGO = MakePanel(modalGO.transform, "Header", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0, -22), new Vector2(0, 44), new Color(0.14f, 0.18f, 0.28f, 1f));
            Text headerTitle = MakeText(headerGO.transform, "HeaderTitle", "📜 BẢNG NHIỆM VỤ / QUEST LOG", 16, FontStyle.Bold, TextAnchor.MiddleLeft, new Vector2(-120, 0), new Vector2(300, 36), new Color(1f, 0.90f, 0.55f, 1f), defaultFont);

            // Close Button
            GameObject closeBtnGO = MakePanel(headerGO.transform, "CloseButton", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-22, 0), new Vector2(30, 30), new Color(0.75f, 0.22f, 0.22f, 1f));
            Button closeBtn = closeBtnGO.AddComponent<Button>();
            MakeText(closeBtnGO.transform, "Text", "✕", 14, FontStyle.Bold, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(30, 30), Color.white, defaultFont);

            // Tabs
            string[] tabs = new string[] { "Tất Cả", "Đang Làm", "Hoàn Thành" };
            for (int i = 0; i < tabs.Length; i++)
            {
                int tabIdx = i;
                GameObject tabGO = MakePanel(headerGO.transform, $"Tab_{tabs[i]}", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(30 + i * 85, 0), new Vector2(80, 26), new Color(0.20f, 0.26f, 0.38f, 1f));
                Button tabBtn = tabGO.AddComponent<Button>();
                MakeText(tabGO.transform, "Text", tabs[i], 11, FontStyle.Normal, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(80, 26), Color.white, defaultFont);
                tabBtn.onClick.AddListener(() => questUI.SetFilter(tabIdx));
            }

            // Left List Panel
            GameObject leftPanelGO = MakePanel(modalGO.transform, "LeftQuestListPanel", new Vector2(0f, 0f), new Vector2(0.38f, 1f), new Vector2(10, 10), new Vector2(-5, -50), new Color(0.05f, 0.07f, 0.11f, 0.85f), true);
            ScrollRect scrollRect = leftPanelGO.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;

            GameObject contentGO = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentGO.transform.SetParent(leftPanelGO.transform, false);
            RectTransform contentRT = contentGO.GetComponent<RectTransform>();
            contentRT.anchorMin = new Vector2(0f, 1f);
            contentRT.anchorMax = new Vector2(1f, 1f);
            contentRT.pivot = new Vector2(0.5f, 1f);
            contentRT.sizeDelta = new Vector2(0f, 400f);

            VerticalLayoutGroup vlg = contentGO.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = 6f;
            vlg.padding = new RectOffset(4, 4, 6, 6);
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;

            ContentSizeFitter csf = contentGO.GetComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scrollRect.content = contentRT;

            // Right Detail Panel
            GameObject rightPanelGO = MakePanel(modalGO.transform, "RightQuestDetailPanel", new Vector2(0.38f, 0f), new Vector2(1f, 1f), new Vector2(5, 10), new Vector2(-10, -50), new Color(0.06f, 0.08f, 0.13f, 0.85f), true);

            Text detTitle = MakeText(rightPanelGO.transform, "DetailTitle", "Tên Nhiệm Vụ Mẫu", 16, FontStyle.Bold, TextAnchor.UpperLeft, new Vector2(14, -14), new Vector2(-28, 26), new Color(1f, 0.88f, 0.45f, 1f), defaultFont, true);
            Text detCat = MakeText(rightPanelGO.transform, "DetailCategory", "Nhiệm Vụ Chính Tuyến • [Đang Thực Hiện]", 11, FontStyle.Normal, TextAnchor.UpperLeft, new Vector2(14, -38), new Vector2(-28, 20), new Color(0.68f, 0.78f, 0.90f, 1f), defaultFont, true);
            GameObject divider = MakePanel(rightPanelGO.transform, "Divider", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(14, -60), new Vector2(-28, 1.5f), new Color(0.25f, 0.32f, 0.45f, 0.8f), true);

            Text detDesc = MakeText(rightPanelGO.transform, "Description", "Mô tả nhiệm vụ hiển thị tại đây. Người chơi có thể đọc hướng dẫn và yêu cầu chi tiết.", 12, FontStyle.Normal, TextAnchor.UpperLeft, new Vector2(14, -68), new Vector2(-28, 70), new Color(0.88f, 0.92f, 0.98f, 1f), defaultFont, true);
            Text detObj = MakeText(rightPanelGO.transform, "Objective", "Mục tiêu: Thu thập 5 khúc gỗ (0/5)", 12, FontStyle.Bold, TextAnchor.UpperLeft, new Vector2(14, -145), new Vector2(-28, 20), new Color(0.4f, 0.82f, 1f, 1f), defaultFont, true);

            // Progress Bar
            GameObject pbGO = MakePanel(rightPanelGO.transform, "ProgressBar", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(14, -170), new Vector2(-28, 18), new Color(0.12f, 0.16f, 0.22f, 1f), true);
            GameObject fillArea = new GameObject("FillArea", typeof(RectTransform));
            fillArea.transform.SetParent(pbGO.transform, false);
            RectTransform faRT = fillArea.GetComponent<RectTransform>();
            faRT.anchorMin = Vector2.zero;
            faRT.anchorMax = Vector2.one;
            faRT.offsetMin = new Vector2(2, 2);
            faRT.offsetMax = new Vector2(-2, -2);

            GameObject fillGO = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            fillGO.transform.SetParent(fillArea.transform, false);
            RectTransform fillRT = fillGO.GetComponent<RectTransform>();
            fillRT.anchorMin = Vector2.zero;
            fillRT.anchorMax = new Vector2(0f, 1f);
            fillRT.sizeDelta = Vector2.zero;
            Image fillImg = fillGO.GetComponent<Image>();
            fillImg.color = new Color(0.18f, 0.76f, 0.45f, 1f);

            Slider slider = pbGO.AddComponent<Slider>();
            slider.fillRect = fillRT;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 0.5f;

            Text pbPct = MakeText(pbGO.transform, "PercentText", "0/5 (0%)", 10, FontStyle.Bold, TextAnchor.MiddleCenter, Vector2.zero, Vector2.zero, Color.white, defaultFont, true);

            // Reward Box
            GameObject rewBoxGO = MakePanel(rightPanelGO.transform, "RewardBox", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(14, 55), new Vector2(-28, 85), new Color(0.10f, 0.13f, 0.20f, 0.9f), true);
            MakeText(rewBoxGO.transform, "RewardTitle", "🎁 PHẦN THƯỞNG HOÀN THÀNH:", 11, FontStyle.Bold, TextAnchor.UpperLeft, new Vector2(10, -8), new Vector2(-20, 18), new Color(1f, 0.85f, 0.3f, 1f), defaultFont, true);

            Text rewGold = MakeText(rewBoxGO.transform, "RewardGold", "🪙 +150 Vàng", 11, FontStyle.Normal, TextAnchor.MiddleLeft, new Vector2(12, -34), new Vector2(110, 20), new Color(1f, 0.85f, 0.25f, 1f), defaultFont);
            Text rewExp = MakeText(rewBoxGO.transform, "RewardExp", "⭐ +100 EXP", 11, FontStyle.Normal, TextAnchor.MiddleLeft, new Vector2(125, -34), new Vector2(110, 20), new Color(0.4f, 0.9f, 1f, 1f), defaultFont);
            Text rewItem = MakeText(rewBoxGO.transform, "RewardItem", "📦 +2 Gỗ Quý", 11, FontStyle.Normal, TextAnchor.MiddleLeft, new Vector2(12, -56), new Vector2(200, 20), new Color(0.85f, 0.70f, 1f, 1f), defaultFont);

            // Claim Button
            GameObject claimBtnGO = MakePanel(rightPanelGO.transform, "ClaimRewardButton", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 14), new Vector2(220, 34), new Color(0.95f, 0.70f, 0.18f, 1f));
            Button claimBtn = claimBtnGO.AddComponent<Button>();
            Text claimTxt = MakeText(claimBtnGO.transform, "Text", "🎁 NHẬN PHẦN THƯỞNG", 12, FontStyle.Bold, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(220, 34), new Color(0.12f, 0.10f, 0.05f, 1f), defaultFont);

            // HUD Mini Tracker
            GameObject hudTrackerGO = MakePanel(canvasGO.transform, "HUDQuestTracker", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-15, -60), new Vector2(210, 50), new Color(0.08f, 0.10f, 0.16f, 0.80f));
            Button hudBtn = hudTrackerGO.AddComponent<Button>();
            Text hudTxt = MakeText(hudTrackerGO.transform, "Text", "📌 <b>Nhiệm vụ:</b>\n<color=#FFD54F>Chưa có nhiệm vụ</color>", 10, FontStyle.Normal, TextAnchor.MiddleLeft, Vector2.zero, new Vector2(190, 42), Color.white, defaultFont);

            closeBtn.onClick.AddListener(() => questUI.SetOpen(false));
            hudBtn.onClick.AddListener(() => questUI.ToggleWindow());

            questUI.Configure(modalGO, contentGO.transform, detTitle, detCat, detDesc, detObj, slider, pbPct, rewGold, rewExp, rewItem, claimBtn, claimTxt, hudTrackerGO, hudTxt);
            modalGO.SetActive(false);
            EditorUtility.SetDirty(questUI);
        }

        // 2. Build VisualNovelCanvas
        VisualNovelDialogueUI vnUI = UnityEngine.Object.FindAnyObjectByType<VisualNovelDialogueUI>(FindObjectsInactive.Include);
        if (vnUI == null)
        {
            GameObject vnCanvasGO = new GameObject("VisualNovelCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(VisualNovelDialogueUI));
            Undo.RegisterCreatedObjectUndo(vnCanvasGO, "Create VisualNovelCanvas");
            vnUI = vnCanvasGO.GetComponent<VisualNovelDialogueUI>();

            Canvas canvas = vnCanvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;

            CanvasScaler scaler = vnCanvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 0.5f;

            // Dialogue Root
            GameObject dlgRoot = MakePanel(vnCanvasGO.transform, "DialogueRoot", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.2f), true);

            // Character Portrait Frame (Khung Chứa Hình Ảnh Nhân Vật)
            GameObject portFrame = MakePanel(dlgRoot.transform, "CharacterPortraitFrame", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-280, 150), new Vector2(210, 270), new Color(0.08f, 0.12f, 0.18f, 0.95f));
            Outline pfOutline = portFrame.AddComponent<Outline>();
            pfOutline.effectColor = new Color(0.90f, 0.78f, 0.40f, 1f);
            pfOutline.effectDistance = new Vector2(2f, -2f);

            // Portrait Image inside Frame
            GameObject portImgGO = MakePanel(portFrame.transform, "PortraitImage", Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-10, -10), Color.white, true);
            Image portImg = portImgGO.GetComponent<Image>();
            portImg.preserveAspect = true;

            // Dialogue Box (Khung Nhân Vật Nói Chuyện)
            GameObject dlgBox = MakePanel(dlgRoot.transform, "DialogueBox", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 20), new Vector2(800, 160), new Color(0.06f, 0.08f, 0.13f, 0.95f));
            Outline dbOutline = dlgBox.AddComponent<Outline>();
            dbOutline.effectColor = new Color(0.85f, 0.72f, 0.38f, 1f);
            dbOutline.effectDistance = new Vector2(2f, -2f);

            // Speaker Name Plate
            GameObject namePlate = MakePanel(dlgBox.transform, "SpeakerNamePlate", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20, -10), new Vector2(160, 30), new Color(0.14f, 0.20f, 0.32f, 1f));
            Text speakerText = MakeText(namePlate.transform, "NameText", "Tên Nhân Vật", 13, FontStyle.Bold, TextAnchor.MiddleLeft, Vector2.zero, new Vector2(140, 26), new Color(1f, 0.88f, 0.45f, 1f), defaultFont);

            // Dialogue Content Text
            Text contentText = MakeText(dlgBox.transform, "DialogueContentText", "Lời thoại nhân vật sẽ hiển thị tại đây theo kiểu Visual Novel đánh máy từng chữ...", 14, FontStyle.Normal, TextAnchor.UpperLeft, new Vector2(25, -28), new Vector2(-50, -45), new Color(0.94f, 0.96f, 1f, 1f), defaultFont, true);

            // Continue Indicator (Mũi tên nhấp nháy ▼)
            GameObject contGO = MakePanel(dlgBox.transform, "ContinueIndicator", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-18, 12), new Vector2(24, 24), Color.clear);
            Text contTxt = MakeText(contGO.transform, "Text", "▼", 14, FontStyle.Bold, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(24, 24), new Color(1f, 0.85f, 0.3f, 1f), defaultFont);

            // Choices Container
            GameObject choicesGO = new GameObject("ChoicesContainer", typeof(RectTransform), typeof(VerticalLayoutGroup));
            choicesGO.transform.SetParent(dlgRoot.transform, false);
            RectTransform choicesRT = choicesGO.GetComponent<RectTransform>();
            choicesRT.anchorMin = new Vector2(0.5f, 0.5f);
            choicesRT.anchorMax = new Vector2(0.5f, 0.5f);
            choicesRT.anchoredPosition = new Vector2(0, 15);
            choicesRT.sizeDelta = new Vector2(360, 160);
            VerticalLayoutGroup cvlg = choicesGO.GetComponent<VerticalLayoutGroup>();
            cvlg.spacing = 8f;
            cvlg.childAlignment = TextAnchor.MiddleCenter;
            cvlg.childControlWidth = false;
            cvlg.childControlHeight = false;

            vnUI.Configure(dlgRoot, portFrame, portImg, dlgBox, speakerText, contentText, contGO, choicesGO.transform);
            dlgRoot.SetActive(false);
            EditorUtility.SetDirty(vnUI);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        if (logSuccess)
        {
            Debug.Log("[QuestAndDialogueSetup] ✨ Đã tạo hoàn tất mẫu QuestCanvas và VisualNovelCanvas trong Hierarchy của PixelLabMap!");
        }
    }

    private static GameObject MakePanel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size, Color color, bool stretch = false)
    {
        Transform existing = parent.Find(name);
        GameObject go = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        if (existing == null) go.transform.SetParent(parent, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;

        if (stretch)
        {
            rt.offsetMin = new Vector2(pos.x, pos.y);
            rt.offsetMax = new Vector2(pos.x + size.x, pos.y + size.y);
        }
        else
        {
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        Image img = go.GetComponent<Image>();
        img.color = color;
        return go;
    }

    private static Text MakeText(Transform parent, string name, string text, int fontSize, FontStyle fontStyle, TextAnchor alignment, Vector2 pos, Vector2 size, Color color, Font font, bool stretch = false)
    {
        Transform existing = parent.Find(name);
        GameObject go = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        if (existing == null) go.transform.SetParent(parent, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        if (stretch)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(pos.x, pos.y);
            rt.offsetMax = new Vector2(pos.x + size.x, pos.y + size.y);
        }
        else
        {
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        Text t = go.GetComponent<Text>();
        t.font = font;
        t.fontSize = fontSize;
        t.fontStyle = fontStyle;
        t.alignment = alignment;
        t.color = color;
        t.text = text;
        return t;
    }
}
