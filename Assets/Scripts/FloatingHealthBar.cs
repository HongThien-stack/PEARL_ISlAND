using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// World Space floating health bar displayed overhead for enemies and monsters.
/// Updates dynamically when Health changes and automatically auto-creates its Canvas UI if missing.
/// </summary>
public class FloatingHealthBar : MonoBehaviour
{
    [Header("Target & Positioning")]
    [SerializeField] private Health targetHealth;
    [SerializeField] private Vector3 offset = new Vector3(0f, 1.4f, 0f);
    [SerializeField] private Vector2 barSize = new Vector2(0.85f, 0.12f);
    [SerializeField] private bool hideWhenFull = true;

    [Header("UI Elements")]
    [SerializeField] private Slider healthSlider;
    [SerializeField] private Image fillImage;
    [SerializeField] private Canvas canvas;

    private Transform targetTransform;

    private void Awake()
    {
        if (targetHealth == null) targetHealth = GetComponentInParent<Health>();
        if (targetHealth != null) targetTransform = targetHealth.transform;

        EnsureCanvasAndSlider();
    }

    private void OnEnable()
    {
        if (targetHealth != null)
        {
            targetHealth.OnHealthChanged += UpdateHealthBar;
            targetHealth.OnDeath += HideOnDeath;
        }
    }

    private void OnDisable()
    {
        if (targetHealth != null)
        {
            targetHealth.OnHealthChanged -= UpdateHealthBar;
            targetHealth.OnDeath -= HideOnDeath;
        }
    }

    private void LateUpdate()
    {
        if (targetTransform != null && canvas != null)
        {
            transform.position = targetTransform.position + offset;
            transform.rotation = Quaternion.identity; // Keep upright facing camera

            // Normalize scale so parent monster scale (e.g. 3x) doesn't blow up the health bar
            if (transform.parent != null)
            {
                Vector3 pScale = transform.parent.lossyScale;
                float invX = Mathf.Abs(pScale.x) > 0.001f ? 1f / Mathf.Abs(pScale.x) : 1f;
                float invY = Mathf.Abs(pScale.y) > 0.001f ? 1f / Mathf.Abs(pScale.y) : 1f;
                transform.localScale = new Vector3(invX, invY, 1f);
            }
        }
    }

    public void UpdateHealthBar(float current, float max)
    {
        if (healthSlider != null)
        {
            healthSlider.maxValue = max;
            healthSlider.value = current;
        }

        if (canvas != null)
        {
            if (hideWhenFull && Mathf.Approximately(current, max))
            {
                canvas.enabled = false;
            }
            else
            {
                canvas.enabled = true;
            }
        }
    }

    private void HideOnDeath()
    {
        if (canvas != null) canvas.enabled = false;
    }

    private void EnsureCanvasAndSlider()
    {
        if (canvas != null && healthSlider != null)
        {
            RectTransform existingRT = canvas.GetComponent<RectTransform>();
            if (existingRT != null) existingRT.sizeDelta = barSize;
            return;
        }

        // Auto-generate WorldSpace UI Canvas & Slider if not assigned
        GameObject canvasGO = new GameObject("OverheadCanvas");
        canvasGO.transform.SetParent(transform, false);

        canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 999;

        RectTransform canvasRT = canvas.GetComponent<RectTransform>();
        canvasRT.sizeDelta = barSize;
        canvasGO.transform.localScale = Vector3.one;

        GameObject sliderGO = new GameObject("HealthSlider");
        sliderGO.transform.SetParent(canvasGO.transform, false);

        healthSlider = sliderGO.AddComponent<Slider>();
        RectTransform sliderRT = sliderGO.GetComponent<RectTransform>();
        sliderRT.anchorMin = Vector2.zero;
        sliderRT.anchorMax = Vector2.one;
        sliderRT.sizeDelta = Vector2.zero;

        // Background
        GameObject bgGO = new GameObject("Background");
        bgGO.transform.SetParent(sliderGO.transform, false);
        Image bgImg = bgGO.AddComponent<Image>();
        bgImg.color = new Color(0.12f, 0.12f, 0.12f, 0.85f);
        RectTransform bgRT = bgGO.GetComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero;
        bgRT.anchorMax = Vector2.one;
        bgRT.sizeDelta = Vector2.zero;

        // Fill Area & Fill Image
        GameObject fillArea = new GameObject("Fill Area");
        fillArea.transform.SetParent(sliderGO.transform, false);
        RectTransform fillAreaRT = fillArea.AddComponent<RectTransform>();
        fillAreaRT.anchorMin = Vector2.zero;
        fillAreaRT.anchorMax = Vector2.one;
        fillAreaRT.sizeDelta = Vector2.zero;

        GameObject fillGO = new GameObject("Fill");
        fillGO.transform.SetParent(fillArea.transform, false);
        fillImage = fillGO.AddComponent<Image>();
        fillImage.color = new Color(0.9f, 0.2f, 0.2f, 1f); // Vibrant Red
        RectTransform fillRT = fillGO.GetComponent<RectTransform>();
        fillRT.anchorMin = Vector2.zero;
        fillRT.anchorMax = Vector2.one;
        fillRT.sizeDelta = Vector2.zero;

        healthSlider.fillRect = fillRT;
        healthSlider.targetGraphic = fillImage;

        if (hideWhenFull && targetHealth != null && Mathf.Approximately(targetHealth.CurrentHealth, targetHealth.MaxHealth))
        {
            canvas.enabled = false;
        }
    }
}
