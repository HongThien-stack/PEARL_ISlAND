using System;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Horizontal Fishing Minigame UI (Stardew Valley style).
/// Mechanics:
/// - Player holds Left Mouse Button: Capture Canvas / Bar accelerates RIGHT.
/// - Player releases Left Mouse Button: Capture Canvas / Bar drifts back LEFT (gravity).
/// - Goal: Keep the Fish Icon inside the Capture Canvas / Bar.
/// - Progress Bar fills while the fish is inside, drains while outside.
/// - 100% Progress = Fish Caught (Success).
/// - 0% Progress (after timer starts) = Fish Escaped (Fail).
/// </summary>
public class FishMinigameUI : MonoBehaviour
{
    public static FishMinigameUI Instance { get; private set; }

    [Header("Minigame Settings")]
    [Tooltip("Track width in UI pixels.")]
    [SerializeField] private float trackWidth = 320f;
    [SerializeField] private float trackHeight = 44f;

    [Tooltip("Capture bar width in UI pixels.")]
    [SerializeField] private float captureWidth = 72f;

    [Tooltip("Speed multiplier when holding Left Mouse (accelerates right).")]
    [SerializeField] private float pushSpeed = 4.2f;

    [Tooltip("Gravity speed pulling the capture bar left when released.")]
    [SerializeField] private float gravitySpeed = 3.6f;

    [Tooltip("Damping to prevent excessive sliding.")]
    [SerializeField] private float damping = 0.92f;

    [Tooltip("Progress gained per second when fish is inside capture bar.")]
    [SerializeField] private float progressGainRate = 0.32f;

    [Tooltip("Progress lost per second when fish is outside capture bar.")]
    [SerializeField] private float progressLossRate = 0.18f;

    [Tooltip("Starting progress (0.0 to 1.0).")]
    [SerializeField] private float initialProgress = 0.35f;

    [Header("UI Element References")]
    [SerializeField] private GameObject rootPanel;
    [SerializeField] private RectTransform trackRect;
    [SerializeField] private RectTransform captureBarRect;
    [SerializeField] private Image captureBarImage;
    [SerializeField] private RectTransform fishIconRect;
    [SerializeField] private Image fishIconImage;
    [SerializeField] private Image progressBarFill;
    [SerializeField] private Text progressText;
    [SerializeField] private Text statusHintText;

    private bool isPlaying = false;
    private float currentProgress = 0.35f;
    private float capturePosNorm = 0.1f;  // 0.0 (left) to 1.0 (right)
    private float captureVelocity = 0f;
    private float fishPosNorm = 0.5f;     // 0.0 (left) to 1.0 (right)
    private float fishVelocity = 0f;
    private float targetFishPosNorm = 0.5f;
    private float fishWanderTimer = 0f;
    private float sessionTimer = 0f;

    private Action onCatchSuccess;
    private Action onCatchFail;

    public bool IsPlaying => isPlaying;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        // Build UI procedurally if not linked in Inspector
        if (rootPanel == null)
        {
            BuildProceduralUI();
        }

        if (rootPanel != null)
        {
            rootPanel.SetActive(false);
        }
    }

    /// <summary>
    /// Starts the fishing minigame session.
    /// </summary>
    public void StartMinigame(Action onSuccess, Action onFail)
    {
        onCatchSuccess = onSuccess;
        onCatchFail = onFail;

        isPlaying = true;
        currentProgress = initialProgress;
        capturePosNorm = 0.15f;
        captureVelocity = 0f;
        fishPosNorm = 0.5f;
        targetFishPosNorm = UnityEngine.Random.Range(0.2f, 0.8f);
        fishWanderTimer = 0.5f;
        sessionTimer = 0f;

        if (rootPanel != null)
        {
            rootPanel.SetActive(true);
        }

        UpdateUI();
    }

    /// <summary>
    /// Force cancels / closes the minigame.
    /// </summary>
    public void CloseMinigame()
    {
        isPlaying = false;
        if (rootPanel != null)
        {
            rootPanel.SetActive(false);
        }
    }

    private void Update()
    {
        if (!isPlaying) return;

        float dt = Time.deltaTime;
        sessionTimer += dt;

        // 1. Player Input: Hold Mouse Button to push right, Release to drift left
        bool isHolding = ReadPlayerHoldInput();

        if (isHolding)
        {
            captureVelocity += pushSpeed * dt;
        }
        else
        {
            captureVelocity -= gravitySpeed * dt;
        }

        captureVelocity *= Mathf.Pow(damping, dt * 60f);
        capturePosNorm += captureVelocity * dt;

        // Clamp capture bar within 0.0 to 1.0
        float halfCaptureNorm = (captureWidth / trackWidth) * 0.5f;
        float minCap = halfCaptureNorm;
        float maxCap = 1f - halfCaptureNorm;

        if (capturePosNorm <= minCap)
        {
            capturePosNorm = minCap;
            if (captureVelocity < 0f) captureVelocity = -captureVelocity * 0.25f; // Slight bounce
        }
        else if (capturePosNorm >= maxCap)
        {
            capturePosNorm = maxCap;
            if (captureVelocity > 0f) captureVelocity = -captureVelocity * 0.25f; // Slight bounce
        }

        // 2. Fish AI: Wanders smoothly along track with darting behaviors
        UpdateFishAI(dt);

        // 3. Catch Evaluation: Check if Fish Icon is inside Capture Bar
        float dist = Mathf.Abs(fishPosNorm - capturePosNorm);
        bool isInside = dist <= (halfCaptureNorm + 0.04f); // With generous catch threshold

        if (isInside)
        {
            currentProgress += progressGainRate * dt;
            if (captureBarImage != null)
            {
                captureBarImage.color = new Color(0.22f, 0.95f, 0.45f, 0.75f); // Bright emerald glow
            }
        }
        else
        {
            currentProgress -= progressLossRate * dt;
            if (captureBarImage != null)
            {
                captureBarImage.color = new Color(0.95f, 0.45f, 0.25f, 0.65f); // Warning orange
            }
        }

        currentProgress = Mathf.Clamp01(currentProgress);

        // 4. Update UI displays
        UpdateUI();

        // 5. Win / Lose Check
        if (currentProgress >= 1f)
        {
            // SUCCESS: Fish Caught!
            isPlaying = false;
            if (statusHintText != null) statusHintText.text = "🎉 BẮT ĐƯỢC CÁ!";
            Invoke(nameof(HandleSuccess), 0.5f);
        }
        else if (currentProgress <= 0f && sessionTimer > 1.2f)
        {
            // FAIL: Fish Escaped!
            isPlaying = false;
            if (statusHintText != null) statusHintText.text = "💨 CÁ ĐÃ TRỐN THOÁT!";
            Invoke(nameof(HandleFail), 0.6f);
        }
    }

    private void UpdateFishAI(float dt)
    {
        fishWanderTimer -= dt;
        if (fishWanderTimer <= 0f)
        {
            // Pick a new target position
            targetFishPosNorm = UnityEngine.Random.Range(0.12f, 0.88f);

            // Sometimes the fish darts quickly, sometimes it stays calm
            float behavior = UnityEngine.Random.value;
            if (behavior < 0.35f)
            {
                // Quick dart
                fishWanderTimer = UnityEngine.Random.Range(0.4f, 0.8f);
            }
            else
            {
                // Smooth swim
                fishWanderTimer = UnityEngine.Random.Range(1.0f, 2.2f);
            }
        }

        // Smoothly guide fish to target
        fishPosNorm = Mathf.SmoothDamp(fishPosNorm, targetFishPosNorm, ref fishVelocity, 0.38f, 1.8f, dt);
        fishPosNorm = Mathf.Clamp(fishPosNorm, 0.06f, 0.94f);
    }

    private void UpdateUI()
    {
        // Position Capture Bar along Track
        if (captureBarRect != null)
        {
            float usableWidth = trackWidth;
            float posX = (capturePosNorm - 0.5f) * usableWidth;
            captureBarRect.anchoredPosition = new Vector2(posX, 0f);
        }

        // Position Fish Icon along Track
        if (fishIconRect != null)
        {
            float usableWidth = trackWidth;
            float posX = (fishPosNorm - 0.5f) * usableWidth;
            fishIconRect.anchoredPosition = new Vector2(posX, Mathf.Sin(Time.time * 6f) * 2.5f); // Subtle aquatic bob
        }

        // Update Progress Bar
        if (progressBarFill != null)
        {
            progressBarFill.fillAmount = currentProgress;
            progressBarFill.color = Color.Lerp(new Color(0.95f, 0.3f, 0.2f), new Color(0.2f, 0.85f, 0.4f), currentProgress);
        }

        if (progressText != null)
        {
            progressText.text = $"{Mathf.RoundToInt(currentProgress * 100f)}%";
        }
    }

    private void HandleSuccess()
    {
        CloseMinigame();
        onCatchSuccess?.Invoke();
    }

    private void HandleFail()
    {
        CloseMinigame();
        onCatchFail?.Invoke();
    }

    private bool ReadPlayerHoldInput()
    {
        // Space controls the capture bar during the fishing minigame.
        try
        {
            if (Keyboard.current != null && Keyboard.current.spaceKey.isPressed) return true;
        }
        catch { }

        // Legacy Input fallback
        try
        {
            return Input.GetKey(KeyCode.Space);
        }
        catch { return false; }
    }

    /// <summary>
    /// Procedurally constructs a clean pixel-art themed Fishing Minigame UI.
    /// </summary>
    public void BuildProceduralUI()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            // Find existing canvas or create one
            canvas = FindAnyObjectByType<Canvas>();
            if (canvas == null)
            {
                GameObject canvasGO = new GameObject("FishMinigameCanvas");
                canvas = canvasGO.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 900;
                CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1280, 720);
                canvasGO.AddComponent<GraphicRaycaster>();
            }
            transform.SetParent(canvas.transform, false);
        }

        // Root Dialog Frame
        GameObject panelGO = new GameObject("FishMinigamePanel");
        panelGO.transform.SetParent(transform, false);
        rootPanel = panelGO;

        RectTransform panelRT = panelGO.AddComponent<RectTransform>();
        panelRT.anchorMin = new Vector2(0.5f, 0.5f);
        panelRT.anchorMax = new Vector2(0.5f, 0.5f);
        panelRT.pivot = new Vector2(0.5f, 0.5f);
        panelRT.sizeDelta = new Vector2(390f, 155f);
        panelRT.anchoredPosition = new Vector2(0f, 60f); // Centered, slightly above MC

        // Panel Background (Cozy Slate / Dark Wood card)
        Image panelBg = panelGO.AddComponent<Image>();
        panelBg.color = new Color(0.12f, 0.16f, 0.22f, 0.94f); // Rich dark pixel slate

        // Panel Border Outline
        Outline panelOutline = panelGO.AddComponent<Outline>();
        panelOutline.effectColor = new Color(0.65f, 0.48f, 0.28f, 1f); // Warm wood border
        panelOutline.effectDistance = new Vector2(3f, -3f);

        // Header Title Text
        GameObject titleGO = new GameObject("TitleText");
        titleGO.transform.SetParent(panelGO.transform, false);
        RectTransform titleRT = titleGO.AddComponent<RectTransform>();
        titleRT.anchorMin = new Vector2(0f, 1f);
        titleRT.anchorMax = new Vector2(1f, 1f);
        titleRT.pivot = new Vector2(0.5f, 1f);
        titleRT.anchoredPosition = new Vector2(0f, -8f);
        titleRT.sizeDelta = new Vector2(0f, 26f);

        Text titleText = titleGO.AddComponent<Text>();
        titleText.text = "🎣 MINIGAME CÂU CÁ";
        titleText.fontSize = 17;
        titleText.fontStyle = FontStyle.Bold;
        titleText.alignment = TextAnchor.MiddleCenter;
        titleText.color = new Color(1f, 0.92f, 0.65f, 1f);
        titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // 1. Minigame Track Container (Border + Aquatic Blue Background)
        GameObject trackGO = new GameObject("TrackBorder");
        trackGO.transform.SetParent(panelGO.transform, false);
        trackRect = trackGO.AddComponent<RectTransform>();
        trackRect.anchorMin = new Vector2(0.5f, 0.5f);
        trackRect.anchorMax = new Vector2(0.5f, 0.5f);
        trackRect.pivot = new Vector2(0.5f, 0.5f);
        trackRect.sizeDelta = new Vector2(trackWidth, trackHeight);
        trackRect.anchoredPosition = new Vector2(0f, 6f);

        Image trackBg = trackGO.AddComponent<Image>();
        trackBg.color = new Color(0.08f, 0.14f, 0.20f, 1f); // Aquatic deep water

        Outline trackOutline = trackGO.AddComponent<Outline>();
        trackOutline.effectColor = new Color(0.25f, 0.38f, 0.52f, 1f);
        trackOutline.effectDistance = new Vector2(2f, -2f);

        // Track Mask to clip elements inside
        trackGO.AddComponent<RectMask2D>();

        // 2. Capture Fish Canva / Bar (Vùng bắt cá)
        GameObject captureGO = new GameObject("CaptureFishCanva");
        captureGO.transform.SetParent(trackGO.transform, false);
        captureBarRect = captureGO.AddComponent<RectTransform>();
        captureBarRect.anchorMin = new Vector2(0.5f, 0.5f);
        captureBarRect.anchorMax = new Vector2(0.5f, 0.5f);
        captureBarRect.pivot = new Vector2(0.5f, 0.5f);
        captureBarRect.sizeDelta = new Vector2(captureWidth, trackHeight - 4f);
        captureBarRect.anchoredPosition = Vector2.zero;

        captureBarImage = captureGO.AddComponent<Image>();
        captureBarImage.color = new Color(0.2f, 0.85f, 0.45f, 0.65f); // Translucent green capture zone

        Outline capOutline = captureGO.AddComponent<Outline>();
        capOutline.effectColor = new Color(0.3f, 1.0f, 0.55f, 0.9f);
        capOutline.effectDistance = new Vector2(2f, 2f);

        // 3. Fish Icon
        GameObject fishGO = new GameObject("FishIcon");
        fishGO.transform.SetParent(trackGO.transform, false);
        fishIconRect = fishGO.AddComponent<RectTransform>();
        fishIconRect.anchorMin = new Vector2(0.5f, 0.5f);
        fishIconRect.anchorMax = new Vector2(0.5f, 0.5f);
        fishIconRect.pivot = new Vector2(0.5f, 0.5f);
        fishIconRect.sizeDelta = new Vector2(30f, 30f);
        fishIconRect.anchoredPosition = Vector2.zero;

        fishIconImage = fishGO.AddComponent<Image>();
        // Load fish sprite if available
        Sprite fishSprite = LoadFishSprite();
        if (fishSprite != null)
        {
            fishIconImage.sprite = fishSprite;
            fishIconImage.preserveAspect = true;
        }
        else
        {
            fishIconImage.color = new Color(1f, 0.75f, 0.2f, 1f); // Gold fish fallback
        }

        // 4. Progress Bar & Border
        GameObject progressBorderGO = new GameObject("ProgressBorder");
        progressBorderGO.transform.SetParent(panelGO.transform, false);
        RectTransform progBorderRT = progressBorderGO.AddComponent<RectTransform>();
        progBorderRT.anchorMin = new Vector2(0.5f, 0.5f);
        progBorderRT.anchorMax = new Vector2(0.5f, 0.5f);
        progBorderRT.pivot = new Vector2(0.5f, 0.5f);
        progBorderRT.sizeDelta = new Vector2(trackWidth, 16f);
        progBorderRT.anchoredPosition = new Vector2(0f, -32f);

        Image progBorderBg = progressBorderGO.AddComponent<Image>();
        progBorderBg.color = new Color(0.06f, 0.08f, 0.12f, 1f);

        Outline progOutline = progressBorderGO.AddComponent<Outline>();
        progOutline.effectColor = new Color(0.4f, 0.4f, 0.45f, 0.8f);
        progOutline.effectDistance = new Vector2(1f, -1f);

        // Progress Fill
        GameObject progFillGO = new GameObject("ProgressBarFill");
        progFillGO.transform.SetParent(progressBorderGO.transform, false);
        RectTransform progFillRT = progFillGO.AddComponent<RectTransform>();
        progFillRT.anchorMin = Vector2.zero;
        progFillRT.anchorMax = Vector2.one;
        progFillRT.sizeDelta = Vector2.zero;

        progressBarFill = progFillGO.AddComponent<Image>();
        progressBarFill.type = Image.Type.Filled;
        progressBarFill.fillMethod = Image.FillMethod.Horizontal;
        progressBarFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        progressBarFill.fillAmount = initialProgress;
        progressBarFill.color = new Color(0.2f, 0.85f, 0.4f, 1f);

        // Progress Text
        GameObject progTextGO = new GameObject("ProgressText");
        progTextGO.transform.SetParent(progressBorderGO.transform, false);
        RectTransform progTextRT = progTextGO.AddComponent<RectTransform>();
        progTextRT.anchorMin = Vector2.zero;
        progTextRT.anchorMax = Vector2.one;
        progTextRT.sizeDelta = Vector2.zero;

        progressText = progTextGO.AddComponent<Text>();
        progressText.text = "35%";
        progressText.fontSize = 11;
        progressText.fontStyle = FontStyle.Bold;
        progressText.alignment = TextAnchor.MiddleCenter;
        progressText.color = Color.white;
        progressText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // 5. Instruction Hint Text
        GameObject hintGO = new GameObject("StatusHintText");
        hintGO.transform.SetParent(panelGO.transform, false);
        RectTransform hintRT = hintGO.AddComponent<RectTransform>();
        hintRT.anchorMin = new Vector2(0f, 0f);
        hintRT.anchorMax = new Vector2(1f, 0f);
        hintRT.pivot = new Vector2(0.5f, 0f);
        hintRT.sizeDelta = new Vector2(0f, 20f);
        hintRT.anchoredPosition = new Vector2(0f, 4f);

        statusHintText = hintGO.AddComponent<Text>();
        statusHintText.text = "Giữ Chuột Trái để di chuyển sang Phải • Thả ra để trôi về Trái";
        statusHintText.fontSize = 11;
        statusHintText.alignment = TextAnchor.MiddleCenter;
        statusHintText.color = new Color(0.85f, 0.88f, 0.92f, 0.85f);
        statusHintText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    private Sprite LoadFishSprite()
    {
#if UNITY_EDITOR
        string path = "Assets/Assets/Food/More_food/Crispy_Fried_Gourami_as_a_collectible_and_carryabl.png";
        var assets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path);
        foreach (var a in assets)
        {
            if (a is Sprite s) return s;
        }
#endif
        return null;
    }
}
