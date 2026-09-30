using UnityEngine;

/// <summary>
/// Dynamic Y-Sorting component for 2D Top-Down environments.
/// Automatically calculates SpriteRenderer sorting order based on Y position.
/// Uses standard formula: sortingOrder = sortingOrderOffset + Mathf.RoundToInt(-transform.position.y * ySortingMultiplier) + extraOffset
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class DynamicYSorter : MonoBehaviour
{
    [Header("Y-Sorting Settings")]
    [Tooltip("Base sorting order offset (default: 30000 to match Player/Customer/Chef).")]
    [SerializeField] private int sortingOrderOffset = 30000;

    [Tooltip("Multiplier for Y position scaling.")]
    [SerializeField] private float ySortingMultiplier = 1.3f;

    [Tooltip("Additional manual offset (+1 to render in front, -1 to render behind).")]
    [SerializeField] private int extraOffset = 0;

    [Tooltip("If true, only updates sorting order once on Awake/Start (useful for static furniture).")]
    [SerializeField] private bool runOnceOnly = false;

    private SpriteRenderer spriteRenderer;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            spriteRenderer.spriteSortPoint = SpriteSortPoint.Pivot;
        }
        UpdateSortingOrder();
    }

    private void Start()
    {
        UpdateSortingOrder();
    }

    private void LateUpdate()
    {
        if (!runOnceOnly)
        {
            UpdateSortingOrder();
        }
    }

    public void UpdateSortingOrder()
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.sortingOrder = sortingOrderOffset + Mathf.RoundToInt(-transform.position.y * ySortingMultiplier) + extraOffset;
        }
    }

    public void SetExtraOffset(int offset)
    {
        extraOffset = offset;
        UpdateSortingOrder();
    }
}
