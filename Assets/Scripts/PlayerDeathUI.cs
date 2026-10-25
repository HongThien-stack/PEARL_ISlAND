using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>Shows the death screen and revives the player at the MC_Home entrance.</summary>
[RequireComponent(typeof(Health))]
public sealed class PlayerDeathUI : MonoBehaviour
{
    [SerializeField] private Sprite youDiedSprite;
    [SerializeField] private Sprite replaySprite;
    [SerializeField] private Transform homeSpawnPoint;
    [SerializeField] private Vector3 homeSpawnOffset = new Vector3(0f, 2.04f, 0f);

    private Health health;
    private Rigidbody2D body;
    private GameObject canvasObject;
    private GameObject deathScreen;
    private float previousTimeScale = 1f;

    private void Awake()
    {
        health = GetComponent<Health>();
        body = GetComponent<Rigidbody2D>();
    }

    public void Configure(Sprite died, Sprite replay, Transform spawnPoint, Vector3 spawnOffset)
    {
        youDiedSprite = died;
        replaySprite = replay;
        homeSpawnPoint = spawnPoint;
        homeSpawnOffset = spawnOffset;
        if (deathScreen == null) BuildDeathScreen();
    }

    private void OnEnable()
    {
        health.OnDeath += ShowDeathScreen;
    }

    private void OnDisable()
    {
        health.OnDeath -= ShowDeathScreen;
    }

    private void OnDestroy()
    {
        if (deathScreen != null && deathScreen.activeSelf)
            Time.timeScale = previousTimeScale;
        if (canvasObject != null) Destroy(canvasObject);
    }

    private void BuildDeathScreen()
    {
        canvasObject = new GameObject("Player Death Canvas", typeof(RectTransform),
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 2000;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1408, 768);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        deathScreen = new GameObject("Death Screen", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        deathScreen.transform.SetParent(canvasObject.transform, false);
        Image shade = deathScreen.GetComponent<Image>();
        shade.color = new Color(0f, 0f, 0f, 0.65f);
        RectTransform shadeRect = shade.rectTransform;
        shadeRect.anchorMin = Vector2.zero;
        shadeRect.anchorMax = Vector2.one;
        shadeRect.offsetMin = Vector2.zero;
        shadeRect.offsetMax = Vector2.zero;

        Image banner = MakeSprite("You Died", youDiedSprite);
        banner.transform.SetParent(deathScreen.transform, false);
        Place(banner.rectTransform, new Vector2(0, 85), new Vector2(556, 268));
        banner.raycastTarget = false;

        Image replay = MakeSprite("Replay", replaySprite);
        replay.transform.SetParent(deathScreen.transform, false);
        Place(replay.rectTransform, new Vector2(0, -130), new Vector2(308, 95));
        Button replayButton = replay.gameObject.AddComponent<Button>();
        replayButton.targetGraphic = replay;
        replayButton.onClick.AddListener(Replay);

        deathScreen.SetActive(false);
    }

    private static Image MakeSprite(string name, Sprite sprite)
    {
        GameObject item = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        Image image = item.GetComponent<Image>();
        image.sprite = sprite;
        return image;
    }

    private static void Place(RectTransform rect, Vector2 offset, Vector2 size)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = offset;
        rect.sizeDelta = size;
    }

    private void ShowDeathScreen()
    {
        if (deathScreen == null)
        {
            Debug.LogError("[PlayerDeathUI] Death screen has not been configured.");
            return;
        }
        if (deathScreen.activeSelf) return;
        if (EventSystem.current == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

        if (body != null) body.linearVelocity = Vector2.zero;
        previousTimeScale = Time.timeScale;
        deathScreen.SetActive(true);
        Time.timeScale = 0f;
    }

    private void Replay()
    {
        if (homeSpawnPoint == null)
        {
            Debug.LogError("[PlayerDeathUI] Home spawn point is not assigned.");
            return;
        }

        Vector3 destination = homeSpawnPoint.position + homeSpawnOffset;
        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
            body.position = destination;
        }
        transform.position = destination;
        health.Revive();
        CameraFollow cameraFollow = FindAnyObjectByType<CameraFollow>();
        if (cameraFollow != null)
        {
            cameraFollow.SetFollowPlayer(transform);
            cameraFollow.SnapToTarget();
        }

        deathScreen.SetActive(false);
        Time.timeScale = previousTimeScale;
    }

#if UNITY_EDITOR
    [ContextMenu("Test Player Death")]
    private void TestPlayerDeath()
    {
        if (Application.isPlaying) health.TakeDamage(health.MaxHealth);
    }
#endif
}
