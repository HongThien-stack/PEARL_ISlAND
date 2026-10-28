using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Shared settings screen for the opening menu and in-game pause menu.</summary>
[DefaultExecutionOrder(-1000)]
public sealed class GameSettingsUI : MonoBehaviour
{
    public static GameSettingsUI Instance { get; private set; }
    public static bool IsOpen => Instance != null && Instance.panelRoot != null && Instance.panelRoot.activeSelf;
    public static int EscapeHandledFrame { get; private set; } = -1;

    private static readonly Color Cream = new Color(1f, .9f, .68f);
    private static readonly Color Gold = new Color(.93f, .67f, .30f);
    private static readonly Color Brown = new Color(.22f, .12f, .075f);
    private static readonly Color ButtonBrown = new Color(.39f, .23f, .13f);

    private bool showLauncher = true;
    private GameObject launcher;
    private GameObject panelRoot;
    private GameObject audioTab;
    private GameObject videoTab;
    private GameObject controlsTab;
    private Slider masterSlider;
    private Slider musicSlider;
    private Slider effectsSlider;
    private Text displayModeLabel;
    private Text messageLabel;
    private float previousTimeScale = 1f;
    private bool pausedGame;
    private bool escapeHeld;

    public void ShowLauncher(bool show)
    {
        showLauncher = show;
        if (launcher != null) launcher.SetActive(show);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    private void Start() => Build();

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        bool down = keyboard != null && keyboard.escapeKey.isPressed;
        bool pressed = keyboard != null && keyboard.escapeKey.wasPressedThisFrame;
        try
        {
            down |= Input.GetKey(KeyCode.Escape);
            pressed |= Input.GetKeyDown(KeyCode.Escape);
        }
        catch (System.InvalidOperationException) { }
        pressed |= down && !escapeHeld;
        escapeHeld = down;
        if (pressed) HandleEscape();
    }

    private void OnGUI()
    {
        Event current = Event.current;
        if (current.type == EventType.KeyDown && current.keyCode == KeyCode.Escape)
            HandleEscape();
    }

    private void HandleEscape()
    {
        if (EscapeHandledFrame == Time.frameCount) return;
        if (IsOpen)
        {
            EscapeHandledFrame = Time.frameCount;
            SetOpen(false);
            return;
        }

        // Let the currently open window consume Escape first.
        if (QuestWindowUI.IsOpen || InventoryUI.IsOpen || VisualNovelDialogueUI.IsOpen) return;
        ShopUI shop = FindAnyObjectByType<ShopUI>();
        if (shop != null && shop.IsOpen) return;
        EscapeHandledFrame = Time.frameCount;
        SetOpen(true);
    }

    private void OnDestroy()
    {
        if (pausedGame)
        {
            Time.timeScale = previousTimeScale;
            MovementLock.Unlock(this);
        }
        if (Instance == this) Instance = null;
    }

    public void Toggle() => SetOpen(!IsOpen);

    public void SetOpen(bool open)
    {
        if (panelRoot == null || panelRoot.activeSelf == open) return;
        bool inGame = SceneManager.GetActiveScene().name != "MainMenu";
        if (open && inGame)
        {
            previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            MovementLock.Lock(this);
            pausedGame = true;
        }
        else if (!open && pausedGame)
        {
            Time.timeScale = previousTimeScale;
            MovementLock.Unlock(this);
            pausedGame = false;
        }
        panelRoot.SetActive(open);
        if (launcher != null) launcher.SetActive(showLauncher && !open);
        if (open) messageLabel.text = inGame ? "Game paused" : "Settings";
    }

    private void Build()
    {
        if (EventSystem.current == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

        GameSettingsVisuals visuals = Resources.Load<GameSettingsVisuals>("GameSettingsVisuals");
        GameObject canvasObject = new GameObject("Settings Canvas", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 2500;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1408, 768);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = .5f;

        Image gear = MakeImage(canvasObject.transform, "Settings Gear", visuals != null ? visuals.gear : null,
            new Vector2(72, 72), new Vector2(-52, -52), new Vector2(1, 1));
        launcher = gear.gameObject;
        if (gear.sprite == null) MakeText(gear.transform, "⚙", 38, Vector2.zero, new Vector2(72, 72));
        Button gearButton = gear.gameObject.AddComponent<Button>();
        gearButton.onClick.AddListener(Toggle);
        launcher.SetActive(showLauncher);

        Image shade = MakeImage(canvasObject.transform, "Settings Overlay", null, Vector2.zero, Vector2.zero);
        panelRoot = shade.gameObject;
        Stretch(shade.rectTransform);
        shade.color = new Color(0f, 0f, 0f, .76f);

        Image panel = MakeImage(shade.transform, "Settings Panel", null,
            new Vector2(930, 650), Vector2.zero);
        panel.color = Brown;
        Outline border = panel.gameObject.AddComponent<Outline>();
        border.effectColor = Gold;
        border.effectDistance = new Vector2(3, -3);
        if (visuals != null && visuals.panel != null)
        {
            Image ornament = MakeImage(panel.transform, "Wooden Ornament", visuals.panel,
                new Vector2(930, 650), Vector2.zero);
            ornament.color = new Color(1f, 1f, 1f, .15f);
            ornament.raycastTarget = false;
        }

        MakeText(panel.transform, "SETTINGS", 42, new Vector2(0, 274), new Vector2(650, 60));
        MakeButton(panel.transform, "X", new Vector2(55, 48), new Vector2(420, 275),
            () => SetOpen(false));
        MakeButton(panel.transform, "AUDIO", new Vector2(210, 48), new Vector2(-230, 212),
            () => SelectTab(0));
        MakeButton(panel.transform, "DISPLAY", new Vector2(210, 48), new Vector2(0, 212),
            () => SelectTab(1));
        MakeButton(panel.transform, "CONTROLS", new Vector2(210, 48), new Vector2(230, 212),
            () => SelectTab(2));

        audioTab = MakeContent(panel.transform, "Audio Tab");
        videoTab = MakeContent(panel.transform, "Display Tab");
        controlsTab = MakeContent(panel.transform, "Controls Tab");
        BuildAudioTab();
        BuildVideoTab();
        BuildControlsTab();

        MakeButton(panel.transform, "SAVE GAME", new Vector2(250, 55), new Vector2(-285, -278), SaveGame);
        MakeButton(panel.transform, "QUIT GAME", new Vector2(250, 55), new Vector2(0, -278), QuitGame);
        MakeButton(panel.transform, "CLOSE", new Vector2(250, 55), new Vector2(285, -278),
            () => SetOpen(false));
        messageLabel = MakeText(panel.transform, "", 18, new Vector2(0, -228), new Vector2(780, 28));
        SelectTab(0);
        panelRoot.SetActive(false);
    }

    private void BuildAudioTab()
    {
        masterSlider = MakeSlider(audioTab.transform, "MASTER VOLUME", 110, GameSettings.MasterVolume);
        musicSlider = MakeSlider(audioTab.transform, "GAME / MENU MUSIC", 0, GameSettings.MusicVolume);
        effectsSlider = MakeSlider(audioTab.transform, "OTHER SOUNDS", -110, GameSettings.EffectsVolume);
        masterSlider.onValueChanged.AddListener(_ => UpdateAudio());
        musicSlider.onValueChanged.AddListener(_ => UpdateAudio());
        effectsSlider.onValueChanged.AddListener(_ => UpdateAudio());
    }

    private void UpdateAudio()
    {
        GameSettings.SetVolumes(masterSlider.value, musicSlider.value, effectsSlider.value);
    }

    private void BuildVideoTab()
    {
        displayModeLabel = MakeText(videoTab.transform, "", 27, new Vector2(0, 125), new Vector2(720, 50));
        MakeButton(videoTab.transform, "FULLSCREEN", new Vector2(260, 54), new Vector2(-145, 40),
            () => ChangeDisplay(true, Screen.width, Screen.height));
        MakeButton(videoTab.transform, "WINDOWED", new Vector2(260, 54), new Vector2(145, 40),
            () => ChangeDisplay(false, 1280, 720));
        MakeText(videoTab.transform, "WINDOW SIZE", 23, new Vector2(0, -35), new Vector2(720, 36));
        MakeButton(videoTab.transform, "960 × 540", new Vector2(180, 48), new Vector2(-290, -105),
            () => ChangeDisplay(false, 960, 540));
        MakeButton(videoTab.transform, "1280 × 720", new Vector2(180, 48), new Vector2(-96, -105),
            () => ChangeDisplay(false, 1280, 720));
        MakeButton(videoTab.transform, "1600 × 900", new Vector2(180, 48), new Vector2(96, -105),
            () => ChangeDisplay(false, 1600, 900));
        MakeButton(videoTab.transform, "1920 × 1080", new Vector2(180, 48), new Vector2(290, -105),
            () => ChangeDisplay(false, 1920, 1080));
        RefreshDisplayLabel();
    }

    private void ChangeDisplay(bool fullscreen, int width, int height)
    {
        GameSettings.ApplyDisplay(fullscreen, width, height);
        RefreshDisplayLabel(fullscreen, width, height);
        messageLabel.text = fullscreen ? "Fullscreen enabled" : "Windowed mode enabled — resize the window freely";
    }

    private void RefreshDisplayLabel(bool? fullscreen = null, int width = 0, int height = 0)
    {
        bool full = fullscreen ?? Screen.fullScreenMode != FullScreenMode.Windowed;
        displayModeLabel.text = full ? "MODE: FULLSCREEN" :
            $"MODE: WINDOWED  { (width > 0 ? width : Screen.width) } × { (height > 0 ? height : Screen.height) }";
    }

    private void BuildControlsTab()
    {
        string[] labels = {
            "Move", "Sprint", "Interact / NPC", "Chop / Shop", "Sword", "Fishing rod",
            "Attack / Cast", "Inventory", "Quests", "Fishing minigame", "Settings"
        };
        string[] keys = {
            "WASD / Arrows", "Shift", "F", "E", "1", "2",
            "J / Left click", "I", "Q", "Space", "Esc"
        };
        for (int i = 0; i < labels.Length; i++)
        {
            int column = i / 6;
            int row = i % 6;
            float x = column == 0 ? -207 : 207;
            float y = 140 - row * 53;
            MakeText(controlsTab.transform, labels[i], 22, new Vector2(x - 86, y),
                new Vector2(210, 43), TextAnchor.MiddleLeft);
            MakeText(controlsTab.transform, keys[i], 21, new Vector2(x + 108, y),
                new Vector2(170, 43), TextAnchor.MiddleRight).color = Gold;
        }
    }

    private void SaveGame()
    {
        if (SceneManager.GetActiveScene().name == "MainMenu")
        {
            messageLabel.text = "Enter the game to save progress";
            return;
        }
        messageLabel.text = GameSave.SaveCurrent() ? "Game saved" : "Could not save game";
    }

    private void QuitGame()
    {
        if (pausedGame) Time.timeScale = previousTimeScale;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void SelectTab(int tab)
    {
        audioTab.SetActive(tab == 0);
        videoTab.SetActive(tab == 1);
        controlsTab.SetActive(tab == 2);
    }

    private static GameObject MakeContent(Transform parent, string name)
    {
        Image image = MakeImage(parent, name, null, new Vector2(850, 390), new Vector2(0, -17));
        image.color = new Color(.12f, .07f, .045f, .88f);
        return image.gameObject;
    }

    private static Slider MakeSlider(Transform parent, string label, float y, float value)
    {
        MakeText(parent, label, 23, new Vector2(-278, y + 27), new Vector2(300, 38), TextAnchor.MiddleLeft);
        Text percent = MakeText(parent, Mathf.RoundToInt(value * 100) + "%", 22,
            new Vector2(311, y - 10), new Vector2(72, 38));
        GameObject sliderObject = new GameObject(label + " Slider", typeof(RectTransform), typeof(Slider));
        sliderObject.transform.SetParent(parent, false);
        RectTransform rect = sliderObject.GetComponent<RectTransform>();
        Place(rect, new Vector2(320, 44), new Vector2(50, y - 10));
        Image track = MakeImage(rect, "Track", null, new Vector2(320, 16), Vector2.zero);
        track.color = ButtonBrown;
        Image fill = MakeImage(track.transform, "Fill", null, Vector2.zero, Vector2.zero);
        fill.rectTransform.anchorMin = Vector2.zero;
        fill.rectTransform.anchorMax = Vector2.one;
        fill.rectTransform.offsetMin = Vector2.zero;
        fill.rectTransform.offsetMax = Vector2.zero;
        fill.color = Gold;
        fill.raycastTarget = false;
        Slider slider = sliderObject.GetComponent<Slider>();
        slider.fillRect = fill.rectTransform;
        slider.handleRect = null;
        slider.targetGraphic = track;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = value;
        slider.onValueChanged.AddListener(v => percent.text = Mathf.RoundToInt(v * 100) + "%");
        return slider;
    }

    private static Button MakeButton(Transform parent, string label, Vector2 size, Vector2 position,
        UnityEngine.Events.UnityAction action)
    {
        Image image = MakeImage(parent, label, null, size, position);
        image.color = ButtonBrown;
        Outline outline = image.gameObject.AddComponent<Outline>();
        outline.effectColor = Gold;
        MakeText(image.transform, label, 22, Vector2.zero, size);
        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(action);
        return button;
    }

    private static Text MakeText(Transform parent, string caption, int size, Vector2 position,
        Vector2 dimensions, TextAnchor alignment = TextAnchor.MiddleCenter)
    {
        GameObject textObject = new GameObject(caption + " Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        textObject.transform.SetParent(parent, false);
        Place(textObject.GetComponent<RectTransform>(), dimensions, position);
        Text text = textObject.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = caption;
        text.fontSize = size;
        text.fontStyle = FontStyle.Bold;
        text.alignment = alignment;
        text.color = Cream;
        text.raycastTarget = false;
        return text;
    }

    private static Image MakeImage(Transform parent, string name, Sprite sprite, Vector2 size,
        Vector2 position, Vector2? anchor = null)
    {
        GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imageObject.transform.SetParent(parent, false);
        RectTransform rect = imageObject.GetComponent<RectTransform>();
        Vector2 point = anchor ?? new Vector2(.5f, .5f);
        rect.anchorMin = point;
        rect.anchorMax = point;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        Image image = imageObject.GetComponent<Image>();
        image.sprite = sprite;
        return image;
    }

    private static void Place(RectTransform rect, Vector2 size, Vector2 position)
    {
        rect.anchorMin = new Vector2(.5f, .5f);
        rect.anchorMax = rect.anchorMin;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
