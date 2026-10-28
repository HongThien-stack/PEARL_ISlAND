using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Builds the opening menu from the sprites assigned in the menu scene.</summary>
public sealed class MainMenuUI : MonoBehaviour
{
    [SerializeField] private Sprite background;
    [SerializeField] private Sprite newGameButton;
    [SerializeField] private Sprite resumeButton;
    [SerializeField] private Sprite settingsButton;
    [SerializeField] private Sprite quitButton;
    [SerializeField] private Sprite gearButton;
    [SerializeField] private Sprite settingsPanel;
    [SerializeField] private AudioClip menuMusic;
    [SerializeField] private AudioClip gameMusic;

    private GameObject settingsRoot;

    private void Awake()
    {
        BackgroundMusic.GetOrCreate().PlayMenu(menuMusic, gameMusic);

        if (EventSystem.current == null)
        {
            GameObject eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            eventSystem.transform.SetParent(transform, false);
        }

        GameObject canvasObject = new GameObject("Main Menu Canvas", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1408, 768);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        Image backdrop = MakeImage("Background", canvasObject.transform, background);
        RectTransform backdropRect = backdrop.rectTransform;
        Place(backdropRect, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        AspectRatioFitter backgroundFit = backdrop.gameObject.AddComponent<AspectRatioFitter>();
        backgroundFit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        backgroundFit.aspectRatio = background.rect.width / background.rect.height;
        backdrop.raycastTarget = false;

        Text gameTitle = MakeTitle("Game Title", canvasObject.transform, "PEARL ISLAND", 62);
        Place(gameTitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 177), new Vector2(680, 82));
        Text subtitle = MakeTitle("Adventure Subtitle", canvasObject.transform, "ADVENTURE", 43);
        Place(subtitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 111), new Vector2(440, 64));

        bool hasSave = GameSave.HasSave;
        MakeButton("New Game", canvasObject.transform, newGameButton, new Vector2(0.5f, 0.5f),
            new Vector2(0, hasSave ? -3 : -15), new Vector2(258, 81), NewGame);
        if (hasSave)
            MakeButton("Resume", canvasObject.transform, resumeButton, new Vector2(0.5f, 0.5f),
                new Vector2(0, -95), new Vector2(258, 81), ResumeGame);
        MakeButton("Settings", canvasObject.transform, settingsButton, new Vector2(0.5f, 0.5f),
            new Vector2(0, hasSave ? -187 : -112), new Vector2(258, 81), ToggleSettings);
        MakeButton("Quit Game", canvasObject.transform, quitButton, new Vector2(0.5f, 0.5f),
            new Vector2(0, hasSave ? -279 : -209), new Vector2(258, 81), QuitGame);
        MakeButton("Settings Gear", canvasObject.transform, gearButton, Vector2.one,
            new Vector2(-54, -54), new Vector2(72, 72), ToggleSettings);

        gameObject.AddComponent<GameSettingsUI>().ShowLauncher(false);
    }

    private void CreateSettingsPanel(Transform parent)
    {
        settingsRoot = new GameObject("Settings Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        settingsRoot.transform.SetParent(parent, false);
        Image dim = settingsRoot.GetComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.55f);
        RectTransform dimRect = dim.rectTransform;
        dimRect.anchorMin = Vector2.zero;
        dimRect.anchorMax = Vector2.one;
        dimRect.offsetMin = Vector2.zero;
        dimRect.offsetMax = Vector2.zero;

        Image panel = MakeImage("Wooden Panel", settingsRoot.transform, settingsPanel);
        Place(panel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(452, 468));
        panel.raycastTarget = true;

        Image heading = MakeImage("Settings Title", panel.transform, settingsButton);
        Place(heading.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 153), new Vector2(258, 81));
        heading.raycastTarget = false;

        Text label = MakeText("Volume", panel.transform, "VOLUME");
        Place(label.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 55), new Vector2(280, 48));

        GameObject sliderObject = new GameObject("Volume Slider", typeof(RectTransform), typeof(Slider));
        sliderObject.transform.SetParent(panel.transform, false);
        RectTransform sliderRect = sliderObject.GetComponent<RectTransform>();
        Place(sliderRect, new Vector2(0.5f, 0.5f), new Vector2(0, -12), new Vector2(280, 32));
        Image track = MakeImage("Track", sliderObject.transform, null);
        track.color = new Color(0.24f, 0.12f, 0.07f, 1f);
        Stretch(track.rectTransform, new Vector2(0, 11));
        Image fill = MakeImage("Fill", track.transform, null);
        fill.color = new Color(0.86f, 0.61f, 0.31f, 1f);
        Stretch(fill.rectTransform, new Vector2(4, 3));
        Image handle = MakeImage("Handle", sliderObject.transform, null);
        handle.color = new Color(1f, 0.84f, 0.54f, 1f);
        handle.rectTransform.sizeDelta = new Vector2(24, 40);
        Slider slider = sliderObject.GetComponent<Slider>();
        slider.fillRect = fill.rectTransform;
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = handle;
        slider.minValue = 0;
        slider.maxValue = 1;
        slider.value = AudioListener.volume;
        slider.onValueChanged.AddListener(value => AudioListener.volume = value);

        MakeButton("Close Settings", panel.transform, gearButton, new Vector2(0.5f, 0.5f),
            new Vector2(0, -155), new Vector2(72, 72), ToggleSettings);
        settingsRoot.SetActive(false);
    }

    private static Image MakeImage(string name, Transform parent, Sprite sprite)
    {
        GameObject item = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        item.transform.SetParent(parent, false);
        Image image = item.GetComponent<Image>();
        image.sprite = sprite;
        return image;
    }

    private static void MakeButton(string name, Transform parent, Sprite sprite, Vector2 anchor,
        Vector2 offset, Vector2 size, UnityEngine.Events.UnityAction action)
    {
        Image image = MakeImage(name, parent, sprite);
        Place(image.rectTransform, anchor, offset, size);
        image.type = Image.Type.Simple;
        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(action);
    }

    private static Text MakeText(string name, Transform parent, string caption)
    {
        GameObject item = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        item.transform.SetParent(parent, false);
        Text label = item.GetComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.text = caption;
        label.fontSize = 30;
        label.fontStyle = FontStyle.Bold;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = new Color(1f, 0.86f, 0.59f, 1f);
        label.raycastTarget = false;
        return label;
    }

    private static Text MakeTitle(string name, Transform parent, string caption, int fontSize)
    {
        Text label = MakeText(name, parent, caption);
        label.fontSize = fontSize;
        label.color = new Color(1f, 0.88f, 0.56f, 1f);
        Outline outline = label.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.20f, 0.09f, 0.04f, 1f);
        outline.effectDistance = new Vector2(3, -3);
        Shadow shadow = label.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0.13f, 0.06f, 0.03f, 0.8f);
        shadow.effectDistance = new Vector2(0, -5);
        return label;
    }

    private static void Place(RectTransform rect, Vector2 anchor, Vector2 offset, Vector2 size)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.anchoredPosition = offset;
        rect.sizeDelta = size;
    }

    private static void Stretch(RectTransform rect, Vector2 margin)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = margin;
        rect.offsetMax = -margin;
    }

    private void ToggleSettings() => GameSettingsUI.Instance?.Toggle();

    private static void NewGame()
    {
        if (!GameSave.BeginNewGame()) return;
        EnterGame();
    }

    private static void ResumeGame()
    {
        GameSave.RequestLoad();
        EnterGame();
    }

    private static void EnterGame()
    {
        BackgroundMusic.GetOrCreate().StartGameWhenLoaded();
        SceneManager.LoadScene("PixelLabMap");
    }

    private static void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
