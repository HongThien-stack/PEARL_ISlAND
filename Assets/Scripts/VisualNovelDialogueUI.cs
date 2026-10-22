using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Model for an individual line of dialogue in a Visual Novel conversation.
/// </summary>
[Serializable]
public class DialogueLine
{
    public string speakerName = "Nhân vật";
    public Color nameColor = new Color(1f, 0.85f, 0.4f, 1f);
    [TextArea(2, 5)]
    public string text = "";
    public Sprite portrait;
    public AudioClip voiceClip;
    public DialogueChoice[] choices;

    public DialogueLine() { }

    public DialogueLine(string speakerName, string text, Sprite portrait = null)
    {
        this.speakerName = speakerName;
        this.text = text;
        this.portrait = portrait;
        this.nameColor = new Color(1f, 0.85f, 0.4f, 1f);
    }
}

/// <summary>
/// Choice button option shown during dialogue.
/// </summary>
[Serializable]
public class DialogueChoice
{
    public string choiceText = "Tiếp tục";
    public Action onSelected;

    public DialogueChoice(string choiceText, Action onSelected = null)
    {
        this.choiceText = choiceText;
        this.onSelected = onSelected;
    }
}

/// <summary>
/// Visual Novel style dialogue system featuring:
/// - A dedicated Character Portrait Frame (Khung chứa hình ảnh nhân vật)
/// - A Dialogue Box with speaker nameplate and typewriter effect (Khung nhân vật nói chuyện)
/// - Branching choices, audio blip effects, and movement locking.
/// Automatically constructs a responsive uGUI Canvas if none is set in Inspector.
/// </summary>
public class VisualNovelDialogueUI : MonoBehaviour
{
    public static VisualNovelDialogueUI Instance { get; private set; }

    [Header("UI Canvas & Panels")]
    [SerializeField] private GameObject dialogueRoot;
    [SerializeField] private GameObject portraitFrame;
    [SerializeField] private Image portraitImage;
    [SerializeField] private GameObject dialogueBox;
    [SerializeField] private Text speakerNameText;
    [SerializeField] private Text dialogueContentText;
    [SerializeField] private GameObject continueIndicator;
    [SerializeField] private Transform choicesContainer;

    [Header("Audio (Optional)")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip typeSound;

    [Header("Typewriter Settings")]
    [SerializeField] private float typingSpeed = 0.03f;
    [SerializeField] private float punctuationPause = 0.15f;

    [Header("Default Assets")]
    [SerializeField] private Sprite defaultPortrait;

    private readonly List<DialogueLine> currentLines = new List<DialogueLine>();
    private int currentLineIndex = 0;
    private bool isTyping = false;
    private bool canAdvance = false;
    private Coroutine typewriterRoutine;
    private string fullTargetText = "";
    private Action onConversationComplete;
    private Font defaultFont;
    private readonly List<GameObject> choiceButtons = new List<GameObject>();

    public static bool IsOpen => Instance != null && Instance.dialogueRoot != null && Instance.dialogueRoot.activeSelf;

    public void Configure(GameObject root, GameObject portFrame, Image portImg, GameObject box, Text speaker, Text content, GameObject indicator, Transform choices)
    {
        dialogueRoot = root;
        portraitFrame = portFrame;
        portraitImage = portImg;
        dialogueBox = box;
        speakerNameText = speaker;
        dialogueContentText = content;
        continueIndicator = indicator;
        choicesContainer = choices;
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

        defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (defaultFont == null)
        {
            defaultFont = Font.CreateDynamicFontFromOSFont("Arial", 16);
        }

        if (dialogueRoot == null)
        {
            BuildProceduralUI();
        }

        if (dialogueRoot != null)
        {
            dialogueRoot.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (!IsOpen)
        {
            bool testPressed = false;
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.tKey.wasPressedThisFrame)
            {
                testPressed = true;
            }
#endif
            if (!testPressed)
            {
                try { testPressed = Input.GetKeyDown(KeyCode.T); } catch { }
            }

            if (testPressed)
            {
                PlayDemoDialogue();
            }
            return;
        }

        bool advancePressed = false;
        bool escapePressed = false;

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            if (Keyboard.current.spaceKey.wasPressedThisFrame ||
                Keyboard.current.enterKey.wasPressedThisFrame ||
                Keyboard.current.eKey.wasPressedThisFrame ||
                Keyboard.current.fKey.wasPressedThisFrame)
            {
                advancePressed = true;
            }
            if (Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                escapePressed = true;
            }
        }
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            advancePressed = true;
        }
#endif
        if (!advancePressed)
        {
            try
            {
                advancePressed = Input.GetKeyDown(KeyCode.Space) ||
                                 Input.GetKeyDown(KeyCode.Return) ||
                                 Input.GetKeyDown(KeyCode.E) ||
                                 Input.GetKeyDown(KeyCode.F) ||
                                 Input.GetMouseButtonDown(0);
            }
            catch { }
        }

        if (!escapePressed)
        {
            try { escapePressed = Input.GetKeyDown(KeyCode.Escape); } catch { }
        }

        if (escapePressed)
        {
            EndDialogue();
            return;
        }

        if (advancePressed)
        {
            OnPlayerAdvanceInput();
        }

        // Animate prompt indicator
        if (continueIndicator != null && continueIndicator.activeSelf)
        {
            float bounce = Mathf.Sin(Time.time * 6f) * 4f;
            continueIndicator.transform.localPosition = new Vector3(
                continueIndicator.transform.localPosition.x,
                bounce - 20f,
                0f
            );
        }
    }

    public void OnPlayerAdvanceInput()
    {
        if (isTyping)
        {
            // First click skips typewriter and immediately shows full line
            SkipTypewriter();
        }
        else if (canAdvance)
        {
            // Second click goes to next line
            currentLineIndex++;
            if (currentLineIndex < currentLines.Count)
            {
                DisplayCurrentLine();
            }
            else
            {
                EndDialogue();
            }
        }
    }

    /// <summary>
    /// Displays a simple dialogue with a single speaker and multiple lines.
    /// </summary>
    public void ShowDialogue(string speakerName, Sprite portrait, string[] lines, Action onComplete = null)
    {
        if (lines == null || lines.Length == 0) return;

        List<DialogueLine> dialogueLines = new List<DialogueLine>();
        for (int i = 0; i < lines.Length; i++)
        {
            dialogueLines.Add(new DialogueLine(speakerName, lines[i], portrait));
        }

        ShowDialogue(dialogueLines, onComplete);
    }

    /// <summary>
    /// Starts a dialogue sequence with customizable speakers, expressions, and choices per line.
    /// </summary>
    public void ShowDialogue(List<DialogueLine> lines, Action onComplete = null)
    {
        if (lines == null || lines.Count == 0) return;

        currentLines.Clear();
        currentLines.AddRange(lines);
        currentLineIndex = 0;
        onConversationComplete = onComplete;

        MovementLock.Lock(this);

        if (dialogueRoot != null)
        {
            dialogueRoot.SetActive(true);
        }

        DisplayCurrentLine();
    }

    private void DisplayCurrentLine()
    {
        ClearChoices();

        if (currentLineIndex < 0 || currentLineIndex >= currentLines.Count)
        {
            EndDialogue();
            return;
        }

        DialogueLine line = currentLines[currentLineIndex];

        // 1. Setup Speaker Name
        if (speakerNameText != null)
        {
            speakerNameText.text = line.speakerName;
            speakerNameText.color = line.nameColor;
        }

        // 2. Setup Character Portrait Frame
        Sprite portraitToShow = line.portrait;
        if (portraitToShow == null)
        {
            if (portraitImage != null && portraitImage.sprite != null)
            {
                portraitToShow = portraitImage.sprite;
            }
            else
            {
                portraitToShow = defaultPortrait;
            }
        }

        if (portraitImage != null)
        {
            if (portraitToShow != null)
            {
                portraitImage.sprite = portraitToShow;
                portraitImage.gameObject.SetActive(true);
                if (portraitFrame != null) portraitFrame.SetActive(true);

                // Subtle punch scale animation on new line
                StartCoroutine(PunchScaleRoutine(portraitFrame.transform));
            }
            else if (portraitImage.sprite != null)
            {
                portraitImage.gameObject.SetActive(true);
                if (portraitFrame != null) portraitFrame.SetActive(true);
            }
            else
            {
                portraitImage.gameObject.SetActive(false);
                if (portraitFrame != null) portraitFrame.SetActive(false);
            }
        }

        // 3. Play voice clip if available
        if (audioSource != null && line.voiceClip != null)
        {
            audioSource.PlayOneShot(line.voiceClip);
        }

        // 4. Start Typewriter Effect
        fullTargetText = line.text;
        if (typewriterRoutine != null)
        {
            StopCoroutine(typewriterRoutine);
        }
        typewriterRoutine = StartCoroutine(TypewriterRoutine(fullTargetText, line.choices));
    }

    private IEnumerator TypewriterRoutine(string targetText, DialogueChoice[] choices)
    {
        isTyping = true;
        canAdvance = false;

        if (continueIndicator != null)
        {
            continueIndicator.SetActive(false);
        }

        if (dialogueContentText != null)
        {
            dialogueContentText.text = "";
        }

        int charIndex = 0;
        while (charIndex < targetText.Length)
        {
            char c = targetText[charIndex];

            // Handle rich text tags like <color=...> or <b> without breaking
            if (c == '<')
            {
                int closeIndex = targetText.IndexOf('>', charIndex);
                if (closeIndex > 0)
                {
                    charIndex = closeIndex + 1;
                    if (dialogueContentText != null)
                    {
                        dialogueContentText.text = targetText.Substring(0, charIndex);
                    }
                    continue;
                }
            }

            charIndex++;
            if (dialogueContentText != null)
            {
                dialogueContentText.text = targetText.Substring(0, charIndex);
            }

            // Typing audio tick
            if (audioSource != null && typeSound != null && (charIndex % 2 == 0))
            {
                audioSource.PlayOneShot(typeSound, 0.4f);
            }

            // Slight pause on punctuation
            if (c == '.' || c == '!' || c == '?' || c == ',')
            {
                yield return new WaitForSeconds(punctuationPause);
            }
            else
            {
                yield return new WaitForSeconds(typingSpeed);
            }
        }

        isTyping = false;
        typewriterRoutine = null;

        // Check if there are choices to show
        if (choices != null && choices.Length > 0)
        {
            canAdvance = false;
            SpawnChoices(choices);
        }
        else
        {
            canAdvance = true;
            if (continueIndicator != null)
            {
                continueIndicator.SetActive(true);
            }
        }
    }

    private void SkipTypewriter()
    {
        if (typewriterRoutine != null)
        {
            StopCoroutine(typewriterRoutine);
            typewriterRoutine = null;
        }

        isTyping = false;
        if (dialogueContentText != null)
        {
            dialogueContentText.text = fullTargetText;
        }

        DialogueLine line = currentLines[currentLineIndex];
        if (line.choices != null && line.choices.Length > 0)
        {
            canAdvance = false;
            SpawnChoices(line.choices);
        }
        else
        {
            canAdvance = true;
            if (continueIndicator != null)
            {
                continueIndicator.SetActive(true);
            }
        }
    }

    private void SpawnChoices(DialogueChoice[] choices)
    {
        ClearChoices();
        if (choicesContainer == null || choices == null) return;

        for (int i = 0; i < choices.Length; i++)
        {
            DialogueChoice choice = choices[i];
            GameObject btnGO = new GameObject($"ChoiceBtn_{i}");
            btnGO.transform.SetParent(choicesContainer, false);

            RectTransform rt = btnGO.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(400f, 40f);

            Image bg = btnGO.AddComponent<Image>();
            bg.color = new Color(0.18f, 0.24f, 0.36f, 0.95f);

            Outline outline = btnGO.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 0.85f, 0.4f, 0.8f);

            Button btn = btnGO.AddComponent<Button>();
            ColorBlock cb = btn.colors;
            cb.highlightedColor = new Color(0.28f, 0.38f, 0.58f, 1f);
            cb.pressedColor = new Color(0.12f, 0.16f, 0.25f, 1f);
            btn.colors = cb;

            GameObject txtGO = new GameObject("Text");
            txtGO.transform.SetParent(btnGO.transform, false);
            RectTransform txtRT = txtGO.AddComponent<RectTransform>();
            txtRT.anchorMin = Vector2.zero;
            txtRT.anchorMax = Vector2.one;
            txtRT.offsetMin = new Vector2(10f, 0f);
            txtRT.offsetMax = new Vector2(-10f, 0f);

            Text txt = txtGO.AddComponent<Text>();
            txt.font = defaultFont;
            txt.fontSize = 14;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;
            txt.text = choice.choiceText;

            btn.onClick.AddListener(() =>
            {
                choice.onSelected?.Invoke();
                ClearChoices();
                currentLineIndex++;
                if (currentLineIndex < currentLines.Count)
                {
                    DisplayCurrentLine();
                }
                else
                {
                    EndDialogue();
                }
            });

            choiceButtons.Add(btnGO);
        }
    }

    private void ClearChoices()
    {
        for (int i = 0; i < choiceButtons.Count; i++)
        {
            if (choiceButtons[i] != null) Destroy(choiceButtons[i]);
        }
        choiceButtons.Clear();
    }

    public void EndDialogue()
    {
        if (typewriterRoutine != null)
        {
            StopCoroutine(typewriterRoutine);
            typewriterRoutine = null;
        }

        isTyping = false;
        canAdvance = false;
        ClearChoices();

        if (dialogueRoot != null)
        {
            dialogueRoot.SetActive(false);
        }

        MovementLock.Unlock(this);

        Action callback = onConversationComplete;
        onConversationComplete = null;
        callback?.Invoke();
    }

    private IEnumerator PunchScaleRoutine(Transform target)
    {
        if (target == null) yield break;
        Vector3 baseScale = Vector3.one;
        Vector3 punchScale = new Vector3(1.05f, 1.05f, 1f);

        float duration = 0.15f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            target.localScale = Vector3.Lerp(punchScale, baseScale, elapsed / duration);
            yield return null;
        }
        target.localScale = baseScale;
    }

    /// <summary>
    /// Plays a sample Visual Novel dialogue sequence for testing (Press 'T').
    /// </summary>
    public void PlayDemoDialogue()
    {
        Sprite portrait = (portraitImage != null && portraitImage.sprite != null) ? portraitImage.sprite : defaultPortrait;

        List<DialogueLine> sample = new List<DialogueLine>
        {
            new DialogueLine("Trưởng Làng Elden", "Chào mừng cậu đã đến với Pearl Island! Đây là nơi bình yên nhưng cũng đầy bí ẩn.", portrait),
            new DialogueLine("Trưởng Làng Elden", "Cậu có thể bấm phím <color=#FFD54F>[Q]</color> để mở <b>Bảng Nhiệm Vụ</b> và xem các việc cần làm trên đảo.", portrait),
            new DialogueLine("Trưởng Làng Elden", "Hãy thử dùng rìu chặt cây, trang bị cần câu cá, hoặc tiêu diệt bọn Goblin để bảo vệ dân làng nhé!", portrait)
            {
                choices = new DialogueChoice[]
                {
                    new DialogueChoice("Tôi sẽ giúp dân làng ngay!", () => Debug.Log("[VN Demo] Người chơi chọn: Giúp đỡ dân làng!")),
                    new DialogueChoice("Để tôi đi khám phá hòn đảo trước.", () => Debug.Log("[VN Demo] Người chơi chọn: Khám phá trước!"))
                }
            }
        };

        ShowDialogue(sample);
    }

    #region Procedural UI Builder
    private void BuildProceduralUI()
    {
        // 1. Screen Space Overlay Canvas
        GameObject canvasGO = new GameObject("VisualNovelCanvas");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50; // Above most game HUDs

        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();

        dialogueRoot = new GameObject("DialogueRoot");
        dialogueRoot.transform.SetParent(canvasGO.transform, false);
        RectTransform rootRT = dialogueRoot.AddComponent<RectTransform>();
        rootRT.anchorMin = Vector2.zero;
        rootRT.anchorMax = Vector2.one;
        rootRT.offsetMin = Vector2.zero;
        rootRT.offsetMax = Vector2.zero;

        // Transparent full-screen click detector
        Button fullScreenClick = dialogueRoot.AddComponent<Button>();
        fullScreenClick.onClick.AddListener(OnPlayerAdvanceInput);
        Image transparentBg = dialogueRoot.AddComponent<Image>();
        transparentBg.color = new Color(0f, 0f, 0f, 0.25f); // Soft cinematics vignette

        // 2. Character Portrait Frame (Khung Chứa Hình Ảnh Nhân Vật)
        portraitFrame = new GameObject("CharacterPortraitFrame");
        portraitFrame.transform.SetParent(dialogueRoot.transform, false);

        RectTransform pfRT = portraitFrame.AddComponent<RectTransform>();
        pfRT.anchorMin = new Vector2(0.5f, 0f);
        pfRT.anchorMax = new Vector2(0.5f, 0f);
        pfRT.pivot = new Vector2(0.5f, 0f);
        pfRT.anchoredPosition = new Vector2(-360f, 195f); // Positioned nicely to the left above dialogue
        pfRT.sizeDelta = new Vector2(250f, 320f);

        Image pfBg = portraitFrame.AddComponent<Image>();
        pfBg.color = new Color(0.08f, 0.12f, 0.18f, 0.95f); // Deep oceanic slate

        Outline pfOutline = portraitFrame.AddComponent<Outline>();
        pfOutline.effectColor = new Color(0.90f, 0.78f, 0.40f, 1f); // Fantasy Gold
        pfOutline.effectDistance = new Vector2(2.5f, -2.5f);

        Shadow pfShadow = portraitFrame.AddComponent<Shadow>();
        pfShadow.effectColor = new Color(0f, 0f, 0f, 0.7f);
        pfShadow.effectDistance = new Vector2(4f, -4f);

        // Portrait Image component
        GameObject portImgGO = new GameObject("PortraitImage");
        portImgGO.transform.SetParent(portraitFrame.transform, false);
        RectTransform piRT = portImgGO.AddComponent<RectTransform>();
        piRT.anchorMin = Vector2.zero;
        piRT.anchorMax = Vector2.one;
        piRT.offsetMin = new Vector2(6f, 6f);
        piRT.offsetMax = new Vector2(-6f, -6f);

        portraitImage = portImgGO.AddComponent<Image>();
        portraitImage.preserveAspect = true;

        // Load default MC portrait if found
        Sprite mcPortrait = Resources.Load<Sprite>("portrait");
        if (mcPortrait != null) defaultPortrait = mcPortrait;

        // 3. Dialogue Box (Khung Nhân Vật Nói Chuyện)
        dialogueBox = new GameObject("DialogueBox");
        dialogueBox.transform.SetParent(dialogueRoot.transform, false);

        RectTransform dbRT = dialogueBox.AddComponent<RectTransform>();
        dbRT.anchorMin = new Vector2(0.5f, 0f);
        dbRT.anchorMax = new Vector2(0.5f, 0f);
        dbRT.pivot = new Vector2(0.5f, 0f);
        dbRT.anchoredPosition = new Vector2(0f, 25f);
        dbRT.sizeDelta = new Vector2(1020f, 200f);

        Image dbBg = dialogueBox.AddComponent<Image>();
        dbBg.color = new Color(0.06f, 0.08f, 0.13f, 0.95f); // Visual Novel dark glass

        Outline dbOutline = dialogueBox.AddComponent<Outline>();
        dbOutline.effectColor = new Color(0.85f, 0.72f, 0.38f, 1f);
        dbOutline.effectDistance = new Vector2(2f, -2f);

        Shadow dbShadow = dialogueBox.AddComponent<Shadow>();
        dbShadow.effectColor = new Color(0f, 0f, 0f, 0.8f);
        dbShadow.effectDistance = new Vector2(4f, -4f);

        // 4. Speaker Name Tag (Bảng tên nhân vật nổi trên khung chat)
        GameObject namePlate = new GameObject("SpeakerNamePlate");
        namePlate.transform.SetParent(dialogueBox.transform, false);

        RectTransform npRT = namePlate.AddComponent<RectTransform>();
        npRT.anchorMin = new Vector2(0f, 1f);
        npRT.anchorMax = new Vector2(0f, 1f);
        npRT.pivot = new Vector2(0f, 0f);
        npRT.anchoredPosition = new Vector2(24f, -14f);
        npRT.sizeDelta = new Vector2(200f, 38f);

        Image npBg = namePlate.AddComponent<Image>();
        npBg.color = new Color(0.14f, 0.20f, 0.32f, 1f);

        Outline npOutline = namePlate.AddComponent<Outline>();
        npOutline.effectColor = new Color(1f, 0.85f, 0.4f, 0.9f);
        npOutline.effectDistance = new Vector2(1.5f, -1.5f);

        GameObject nameTxtGO = new GameObject("NameText");
        nameTxtGO.transform.SetParent(namePlate.transform, false);
        RectTransform ntRT = nameTxtGO.AddComponent<RectTransform>();
        ntRT.anchorMin = Vector2.zero;
        ntRT.anchorMax = Vector2.one;
        ntRT.offsetMin = new Vector2(12f, 0f);
        ntRT.offsetMax = new Vector2(-12f, 0f);

        speakerNameText = nameTxtGO.AddComponent<Text>();
        speakerNameText.font = defaultFont;
        speakerNameText.fontSize = 17;
        speakerNameText.fontStyle = FontStyle.Bold;
        speakerNameText.alignment = TextAnchor.MiddleLeft;
        speakerNameText.color = new Color(1f, 0.88f, 0.45f, 1f);
        speakerNameText.text = "Nhân vật";

        // 5. Dialogue Content Text (Nội dung lời thoại Typewriter)
        GameObject contentTxtGO = new GameObject("DialogueContentText");
        contentTxtGO.transform.SetParent(dialogueBox.transform, false);

        RectTransform ctRT = contentTxtGO.AddComponent<RectTransform>();
        ctRT.anchorMin = Vector2.zero;
        ctRT.anchorMax = Vector2.one;
        ctRT.offsetMin = new Vector2(36f, 24f);
        ctRT.offsetMax = new Vector2(-46f, -44f);

        dialogueContentText = contentTxtGO.AddComponent<Text>();
        dialogueContentText.font = defaultFont;
        dialogueContentText.fontSize = 17;
        dialogueContentText.lineSpacing = 1.35f;
        dialogueContentText.alignment = TextAnchor.UpperLeft;
        dialogueContentText.color = new Color(0.94f, 0.96f, 1f, 1f);
        dialogueContentText.text = "Nội dung cuộc hội thoại sẽ hiển thị tại đây với hiệu ứng chữ gõ Visual Novel...";

        // 6. Continue Indicator (Mũi tên nhấp nháy báo bấm để đọc tiếp)
        continueIndicator = new GameObject("ContinueIndicator");
        continueIndicator.transform.SetParent(dialogueBox.transform, false);

        RectTransform ciRT = continueIndicator.AddComponent<RectTransform>();
        ciRT.anchorMin = new Vector2(1f, 0f);
        ciRT.anchorMax = new Vector2(1f, 0f);
        ciRT.pivot = new Vector2(1f, 0f);
        ciRT.anchoredPosition = new Vector2(-24f, 16f);
        ciRT.sizeDelta = new Vector2(30f, 30f);

        Text ciText = continueIndicator.AddComponent<Text>();
        ciText.font = defaultFont;
        ciText.fontSize = 18;
        ciText.alignment = TextAnchor.MiddleCenter;
        ciText.color = new Color(1f, 0.85f, 0.3f, 1f);
        ciText.text = "▼";

        continueIndicator.SetActive(false);

        // 7. Choices Container (Khung chứa các lựa chọn phân nhánh)
        GameObject choicesGO = new GameObject("ChoicesContainer");
        choicesGO.transform.SetParent(dialogueRoot.transform, false);

        RectTransform ccRT = choicesGO.AddComponent<RectTransform>();
        ccRT.anchorMin = new Vector2(0.5f, 0.5f);
        ccRT.anchorMax = new Vector2(0.5f, 0.5f);
        ccRT.pivot = new Vector2(0.5f, 0.5f);
        ccRT.anchoredPosition = new Vector2(0f, 20f);
        ccRT.sizeDelta = new Vector2(450f, 200f);

        VerticalLayoutGroup cvlg = choicesGO.AddComponent<VerticalLayoutGroup>();
        cvlg.spacing = 10f;
        cvlg.childAlignment = TextAnchor.MiddleCenter;
        cvlg.childForceExpandWidth = false;
        cvlg.childForceExpandHeight = false;
        cvlg.childControlWidth = false;
        cvlg.childControlHeight = false;

        choicesContainer = choicesGO.transform;
    }
    #endregion
}
