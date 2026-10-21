using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
public class HealthBarHudSample : MonoBehaviour
{
    [Header("Layout")]
    [SerializeField] private Vector2 panelSize = new Vector2(520f, 150f);
    [SerializeField] private Vector2 panelPosition = new Vector2(40f, -40f);
    [SerializeField] private Vector2 portraitSize = new Vector2(120f, 120f);
    [SerializeField] private Vector2 healthBarSize = new Vector2(330f, 34f);
    [SerializeField] private float spacing = 18f;

    [Header("Character Image")]
    [SerializeField] private Sprite characterSprite;
    [SerializeField] private Color portraitBackground = new Color(0.12f, 0.16f, 0.2f, 1f);
    [SerializeField] private Color panelColor = new Color(0.04f, 0.06f, 0.08f, 0.94f);

    [Header("Health Bar")]
    [SerializeField] private Health targetHealth;
    [SerializeField, Range(0f, 1f)] private float healthPercent = 1f;
    [SerializeField] private Sprite healthBarFrameSprite;
    [SerializeField] private Sprite healthBarSprite;
    [SerializeField] private Color healthBarFrameColor = Color.white;
    [SerializeField] private Color healthBarColor = Color.white;
    [SerializeField] private Color healthBarBackgroundColor = new Color(0.18f, 0.04f, 0.04f, 1f);

    private Canvas canvas;
    private bool healthEventsSubscribed;

    private void OnEnable()
    {
        EnsureHierarchy();
        ApplyVisuals();
        FindTargetHealth();
        SubscribeToHealth();
    }

    private void Start()
    {
        FindTargetHealth();
        SubscribeToHealth();
        RefreshFromHealth();
    }

    private void OnDisable()
    {
        UnsubscribeFromHealth();
    }

    private void OnValidate()
    {
        if (canvas == null)
        {
            canvas = FindPrimaryCanvas();
        }

        ApplyVisuals();
    }

    [ContextMenu("Rebuild Health HUD")]
    private void RebuildHealthHud()
    {
        EnsureHierarchy(true);
        ApplyVisuals();
    }

    [ContextMenu("Cleanup Duplicate HUD Objects")]
    private void CleanupDuplicateHudObjects()
    {
        canvas = FindPrimaryCanvas();
        if (canvas == null)
        {
            return;
        }

        RectTransform panel = FindChildRect(canvas.transform, "Health HUD Panel");
        RemoveDuplicatePanels(canvas.transform, panel);
        if (panel == null)
        {
            return;
        }

        RemoveDuplicateChildren(panel, "Character Frame");
        RemoveDuplicateChildren(panel, "Health Bar Frame");
        RemoveChildrenNamed(panel, "Health Label (Image Slot)");
        ApplyVisuals();
    }

    private void EnsureHierarchy(bool forceRebuild = false)
    {
        canvas = FindPrimaryCanvas();
        if (canvas == null)
        {
            GameObject canvasObject = new GameObject("Health HUD Canvas");
            canvasObject.transform.SetParent(transform, false);
            canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            canvasObject.AddComponent<CanvasScaler>();
            canvasObject.AddComponent<GraphicRaycaster>();
        }

        RemoveDuplicateCanvases(canvas.transform);

        RectTransform panel = FindChildRect(canvas.transform, "Health HUD Panel");
        RemoveDuplicatePanels(canvas.transform, panel);
        if (forceRebuild && panel != null)
        {
            DestroyObject(panel.gameObject);
            panel = null;
        }

        if (panel != null && panel.Find("Health Bar Frame") != null)
        {
            RemoveDuplicateChildren(panel, "Character Frame");
            RemoveDuplicateChildren(panel, "Health Bar Frame");
            RemoveDuplicateChildren(panel, "Health Label (Image Slot)");
            RemoveDuplicateChildren(panel, "Health Bar Background");
            RemoveDuplicateChildren(panel, "Health Bar");
            RemoveChildrenNamed(panel, "Health Label (Image Slot)");
            return;
        }

        if (panel != null)
        {
            DestroyObject(panel.gameObject);
        }

        panel = CreateRect("Health HUD Panel", canvas.transform, panelSize, panelPosition);
        panel.gameObject.AddComponent<Image>().color = panelColor;

        RectTransform portraitFrame = CreateRect(
            "Character Frame",
            panel,
            portraitSize,
            new Vector2(20f, 0f));
        portraitFrame.gameObject.AddComponent<Image>().color = portraitBackground;
        Image portrait = CreateImage("Character Image", portraitFrame);
        SetStretch(portrait.rectTransform, 0.08f, 0.92f);
        portrait.preserveAspect = true;

        float contentX = 20f + portraitSize.x + spacing;
        RectTransform healthFrame = CreateRect(
            "Health Bar Frame",
            panel,
            healthBarSize + new Vector2(10f, 10f),
            new Vector2(contentX, 0f));
        healthFrame.gameObject.AddComponent<Image>().color = healthBarFrameColor;

        RectTransform background = CreateRect(
            "Health Bar Background",
            healthFrame,
            healthBarSize,
            Vector2.zero);
        background.gameObject.AddComponent<Image>().color = healthBarBackgroundColor;

        RectTransform healthBar = CreateRect(
            "Health Bar",
            background,
            healthBarSize,
            Vector2.zero);
        healthBar.gameObject.AddComponent<Image>().color = healthBarColor;
    }

    private void ApplyVisuals()
    {
        if (canvas == null)
        {
            canvas = FindPrimaryCanvas();
        }

        if (canvas == null)
        {
            return;
        }

        RectTransform panel = FindChildRect(canvas.transform, "Health HUD Panel");
        if (panel == null)
        {
            return;
        }

        RectTransform portraitFrame = FindChildRect(panel, "Character Frame");
        RectTransform portrait = portraitFrame == null
            ? null
            : FindChildRect(portraitFrame, "Character Image");
        if (portraitFrame != null)
        {
            GetOrAddImage(portraitFrame);
        }

        if (portrait != null)
        {
            Image portraitImage = GetOrAddImage(portrait);
            portraitImage.sprite = characterSprite;
            portraitImage.preserveAspect = true;
        }

        RectTransform frame = FindChildRect(panel, "Health Bar Frame");
        RectTransform background = frame == null
            ? null
            : FindChildRect(frame, "Health Bar Background");
        RectTransform healthBar = background == null
            ? null
            : FindChildRect(background, "Health Bar");
        if (frame == null || background == null || healthBar == null)
        {
            return;
        }

        Image frameImage = GetOrAddImage(frame);
        frameImage.sprite = healthBarFrameSprite;
        frameImage.type = Image.Type.Sliced;

        GetOrAddImage(background);

        Image healthImage = GetOrAddImage(healthBar);
        healthImage.sprite = healthBarSprite;
        healthImage.type = Image.Type.Filled;
        healthImage.fillMethod = Image.FillMethod.Horizontal;
        healthImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        healthImage.fillAmount = Mathf.Clamp01(healthPercent);
    }

    private void FindTargetHealth()
    {
        if (targetHealth != null)
        {
            return;
        }

        GameObject player = GameObject.FindWithTag("Player");
        if (player != null)
        {
            targetHealth = player.GetComponent<Health>();
        }
    }

    private void SubscribeToHealth()
    {
        if (healthEventsSubscribed || targetHealth == null)
        {
            return;
        }

        targetHealth.OnHealthChanged += HandleHealthChanged;
        healthEventsSubscribed = true;
    }

    private void UnsubscribeFromHealth()
    {
        if (!healthEventsSubscribed || targetHealth == null)
        {
            return;
        }

        targetHealth.OnHealthChanged -= HandleHealthChanged;
        healthEventsSubscribed = false;
    }

    private void RefreshFromHealth()
    {
        if (targetHealth != null)
        {
            HandleHealthChanged(targetHealth.CurrentHealth, targetHealth.MaxHealth);
        }
    }

    private void HandleHealthChanged(float current, float max)
    {
        healthPercent = max <= 0f ? 0f : Mathf.Clamp01(current / max);
        ApplyVisuals();
    }

    private Canvas FindPrimaryCanvas()
    {
        Transform named = transform.Find("Health HUD Canvas");
        Canvas primary = named == null ? null : named.GetComponent<Canvas>();
        if (primary != null)
        {
            return primary;
        }

        for (int i = 0; i < transform.childCount; i++)
        {
            Canvas candidate = transform.GetChild(i).GetComponent<Canvas>();
            if (candidate != null)
            {
                candidate.name = "Health HUD Canvas";
                return candidate;
            }
        }

        return null;
    }

    private void RemoveDuplicatePanels(Transform canvasTransform, RectTransform primary)
        {
            for (int i = canvasTransform.childCount - 1; i >= 0; i--)
            {
                Transform child = canvasTransform.GetChild(i);
                if (child == primary || child.name != "Health HUD Panel")
                {
                    continue;
                }

                DestroyObject(child.gameObject);
            }
        }

        private void RemoveDuplicateChildren(Transform parent, string childName)
        {
            Transform first = null;
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child.name != childName)
                {
                    continue;
                }

                if (first == null)
                {
                    first = child;
                }
                else
                {
                    DestroyObject(child.gameObject);
                }
            }
        }

        private void RemoveChildrenNamed(Transform parent, string childName)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Transform child = parent.GetChild(i);
                if (child.name == childName)
                {
                    DestroyObject(child.gameObject);
                }
            }
        }

    private void RemoveDuplicateCanvases(Transform primary)
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child == primary || child.GetComponent<Canvas>() == null)
            {
                continue;
            }

            DestroyObject(child.gameObject);
        }
    }

    private static RectTransform FindChildRect(Transform parent, string childName)
    {
        Transform child = parent.Find(childName);
        return child == null ? null : child.GetComponent<RectTransform>();
    }

    private static RectTransform CreateRect(
        string objectName,
        Transform parent,
        Vector2 size,
        Vector2 position)
    {
        GameObject child = new GameObject(objectName, typeof(RectTransform));
        child.transform.SetParent(parent, false);
        RectTransform rect = child.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        return rect;
    }

    private static Image CreateImage(string objectName, Transform parent)
    {
        GameObject child = new GameObject(objectName, typeof(RectTransform));
        child.transform.SetParent(parent, false);
        return child.AddComponent<Image>();
    }

    private static Image GetOrAddImage(RectTransform rect)
    {
        Image image = rect.GetComponent<Image>();
        return image == null ? rect.gameObject.AddComponent<Image>() : image;
    }

    private static void SetStretch(RectTransform rect, float min, float max)
    {
        rect.anchorMin = new Vector2(min, min);
        rect.anchorMax = new Vector2(max, max);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void DestroyObject(GameObject target)
    {
        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
    }
}
