using UnityEngine;

/// <summary>
/// Handles the visual Fishing Bobber (phao câu) and pixel Fishing Line in the world.
/// Features:
/// - Smooth arc toss animation when cast
/// - Gentle water bobbing wave motion
/// - LineRenderer pixel fishing line connecting the rod tip to the bobber
/// - Rapid tug/dip vibration and Exclamation mark popup when fish bites
/// - Clean retraction / reel in
/// </summary>
public class FishingBobber : MonoBehaviour
{
    [Header("Visual References")]
    [SerializeField] private SpriteRenderer bobberRenderer;
    [SerializeField] private Sprite bobberSprite;
    [SerializeField] private LineRenderer lineRenderer;
    [SerializeField] private GameObject exclamationPopup;

    [Header("Line Settings")]
    [SerializeField] private Color lineColor = Color.black;
    [SerializeField] private float lineWidth = 0.025f;

    private Transform rodTip;
    private Vector3 landingPos;
    private bool isBiting = false;
    private bool isLanded = false;
    private float tossTimer = 0f;
    private const float tossDuration = 0.45f;
    private Vector3 tossStartPos;

    public bool IsBiting => isBiting;
    public bool IsLanded => isLanded;

    private void Awake()
    {
        if (bobberRenderer == null) bobberRenderer = GetComponent<SpriteRenderer>();
        if (bobberRenderer == null) bobberRenderer = gameObject.AddComponent<SpriteRenderer>();

        // Setup Bobber Sprite if empty
        if (bobberRenderer != null && bobberRenderer.sprite == null)
        {
            bobberRenderer.sprite = bobberSprite != null ? bobberSprite : LoadBobberSprite();
            bobberRenderer.sortingLayerName = "Default";
            bobberRenderer.sortingOrder = 32000; // Above water and ground
        }

        // Setup LineRenderer
        if (lineRenderer == null)
        {
            lineRenderer = gameObject.AddComponent<LineRenderer>();
            lineRenderer.startWidth = lineWidth;
            lineRenderer.endWidth = lineWidth;
            lineRenderer.positionCount = 3; // Tip, Mid sag, Bobber
            lineRenderer.useWorldSpace = true;
            lineRenderer.sortingLayerName = "Default";
            lineRenderer.sortingOrder = 31999;

            Material lineMat = new Material(Shader.Find("Sprites/Default"));
            lineMat.color = lineColor;
            lineRenderer.material = lineMat;
            lineRenderer.startColor = lineColor;
            lineRenderer.endColor = lineColor;
        }

        // Create Exclamation Mark indicator if not assigned
        if (exclamationPopup == null)
        {
            CreateExclamationPopup();
        }
    }

    /// <summary>
    /// Initializes and tosses the bobber from rod tip towards landing position.
    /// </summary>
    public void Initialize(Transform tipTransform, Vector3 targetLanding, Sprite spriteOverride = null)
    {
        rodTip = tipTransform;
        landingPos = targetLanding;
        if (spriteOverride != null && bobberRenderer != null)
        {
            bobberRenderer.sprite = spriteOverride;
        }
        tossStartPos = tipTransform != null ? tipTransform.position : transform.position;
        transform.position = tossStartPos;
        tossTimer = 0f;
        isLanded = false;
        isBiting = false;

        if (exclamationPopup != null) exclamationPopup.SetActive(false);
    }

    private void Update()
    {
        // 1. Arc toss animation on spawn
        if (!isLanded)
        {
            tossTimer += Time.deltaTime;
            float t = Mathf.Clamp01(tossTimer / tossDuration);

            // Parabolic arc
            Vector3 currentPos = Vector3.Lerp(tossStartPos, landingPos, t);
            float arcHeight = Mathf.Sin(t * Mathf.PI) * 0.8f;
            currentPos.y += arcHeight;
            transform.position = currentPos;

            if (t >= 1f)
            {
                isLanded = true;
                transform.position = landingPos;
            }
        }
        else
        {
            // 2. Water floating or bite struggle motion
            if (isBiting)
            {
                // Rapid splash and jerk
                float shake = Mathf.Sin(Time.time * 24f) * 0.08f;
                float dip = Mathf.PingPong(Time.time * 6f, 0.14f) - 0.07f;
                transform.position = landingPos + new Vector3(shake, dip, 0f);
            }
            else
            {
                // Gentle calm bobbing
                float bob = Mathf.Sin(Time.time * 3.5f) * 0.04f;
                transform.position = landingPos + new Vector3(0f, bob, 0f);
            }
        }

        // 3. Update Fishing Line connecting Rod Tip to Bobber
        UpdateFishingLine();
    }

    private void UpdateFishingLine()
    {
        if (lineRenderer == null) return;

        Vector3 start = rodTip != null ? rodTip.position : transform.position;
        Vector3 end = transform.position;

        // Mid-point with natural gravity sag
        float sag = isLanded ? 0.12f : 0.04f;
        Vector3 mid = (start + end) * 0.5f + Vector3.down * sag;

        lineRenderer.SetPosition(0, start);
        lineRenderer.SetPosition(1, mid);
        lineRenderer.SetPosition(2, end);
    }

    /// <summary>
    /// Triggers the bite reaction: Bobber shakes and "!" exclamation pops up.
    /// </summary>
    public void OnBite()
    {
        isBiting = true;
        if (exclamationPopup != null)
        {
            exclamationPopup.SetActive(true);
            exclamationPopup.transform.position = transform.position + Vector3.up * 0.65f;
        }
        Debug.Log("[FishingBobber] 🐟 Cá đã cắn câu! Nhấp chuột để giật cá!");
    }

    /// <summary>
    /// Retracts and destroys the bobber and fishing line.
    /// </summary>
    public void ReelIn()
    {
        Destroy(gameObject);
    }

    private void CreateExclamationPopup()
    {
        GameObject popGO = new GameObject("ExclamationPopup");
        popGO.transform.SetParent(transform, false);
        popGO.transform.localPosition = new Vector3(0f, 0.65f, 0f);

        TextMesh tm = popGO.AddComponent<TextMesh>();
        tm.text = "!";
        tm.fontSize = 32;
        tm.fontStyle = FontStyle.Bold;
        tm.color = new Color(1f, 0.2f, 0.2f, 1f); // Vibrant red exclamation
        tm.alignment = TextAlignment.Center;
        tm.anchor = TextAnchor.MiddleCenter;

        MeshRenderer mr = popGO.GetComponent<MeshRenderer>();
        if (mr != null)
        {
            mr.sortingLayerName = "Default";
            mr.sortingOrder = 32500;
        }

        exclamationPopup = popGO;
        exclamationPopup.SetActive(false);
    }

    private Sprite LoadBobberSprite()
    {
#if UNITY_EDITOR
        string path = "Assets/Characters/Object/Item/fish_bobber-standard.png";
        var assets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path);
        foreach (var a in assets)
        {
            if (a is Sprite s) return s;
        }
#endif
        return null;
    }
}
