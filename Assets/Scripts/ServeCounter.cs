using UnityEngine;

/// <summary>
/// Represents the Serve Table where Chef places prepared food and MC picks it up to serve customers.
/// Respects the exact position of the FoodDisplay child GameObject as placed in the Unity Scene view.
/// </summary>
public class ServeCounter : MonoBehaviour, IInteractable
{
    [Header("Counter Display Settings")]
    [Tooltip("SpriteRenderer on a CHILD GameObject used to display food. Position it wherever you want on the table in Scene View!")]
    [SerializeField] private SpriteRenderer foodDisplayRenderer;

    [Tooltip("If true, overrides FoodDisplay position with code offset below. Leave FALSE to use your Scene View drag position!")]
    [SerializeField] private bool overridePositionWithCode = false;

    [Tooltip("Only used if 'overridePositionWithCode' is checked.")]
    [SerializeField] private Vector3 foodLocalOffset = new Vector3(0f, 0.2f, 0f);

    [Tooltip("Offset added to table's Order in Layer (e.g. 1) or absolute layer order.")]
    [SerializeField] private int foodSortingOrderOffset = 1;

    [Header("Current State")]
    [SerializeField] private Sprite currentFoodOnCounter;
    [SerializeField] private bool hasFood = false;

    private SpriteRenderer tableRenderer;

    private void Awake()
    {
        tableRenderer = GetComponent<SpriteRenderer>();
        EnsureFoodDisplayRenderer();
        UpdateDisplay();
    }

    private void OnValidate()
    {
        EnsureFoodDisplayRenderer();
        UpdateDisplay();
    }

    private void EnsureFoodDisplayRenderer()
    {
        // 1. Try finding existing child
        if (foodDisplayRenderer == null)
        {
            Transform childDisplay = transform.Find("FoodDisplay");
            if (childDisplay == null) childDisplay = transform.Find("food display");
            if (childDisplay == null) childDisplay = transform.Find("HeldFood");

            if (childDisplay != null)
            {
                foodDisplayRenderer = childDisplay.GetComponent<SpriteRenderer>();
            }
        }

        // 2. If still missing or erroneously set to table's own renderer, auto-create a child GameObject
        if (foodDisplayRenderer == null || foodDisplayRenderer.gameObject == gameObject)
        {
            Transform existingChild = transform.Find("FoodDisplay");
            GameObject foodDisplayGO;

            if (existingChild == null)
            {
                foodDisplayGO = new GameObject("FoodDisplay");
                foodDisplayGO.transform.SetParent(transform);
                foodDisplayGO.transform.localPosition = new Vector3(0f, 0.3f, 0f);
            }
            else
            {
                foodDisplayGO = existingChild.gameObject;
            }

            foodDisplayRenderer = foodDisplayGO.GetComponent<SpriteRenderer>();
            if (foodDisplayRenderer == null)
            {
                foodDisplayRenderer = foodDisplayGO.AddComponent<SpriteRenderer>();
            }
        }

        ApplySortingOrder();
    }

    private void ApplySortingOrder()
    {
        if (foodDisplayRenderer == null || foodDisplayRenderer.gameObject == gameObject) return;

        if (tableRenderer == null) tableRenderer = GetComponent<SpriteRenderer>();

        if (tableRenderer != null)
        {
            foodDisplayRenderer.sortingLayerID = tableRenderer.sortingLayerID;

            if (foodSortingOrderOffset > 1000)
            {
                foodDisplayRenderer.sortingOrder = foodSortingOrderOffset;
            }
            else
            {
                foodDisplayRenderer.sortingOrder = tableRenderer.sortingOrder + foodSortingOrderOffset;
            }
        }
    }

    /// <summary>
    /// Chef places food onto this counter.
    /// </summary>
    public void PlaceFood(Sprite foodSprite)
    {
        currentFoodOnCounter = foodSprite;
        hasFood = true;

        EnsureFoodDisplayRenderer();
        UpdateDisplay();

        if (foodSprite == null)
        {
            Debug.LogWarning("[ServeCounter] ⚠️ Chef vừa đặt đồ ăn nhưng Sprite món ăn bị NULL!");
        }
        else
        {
            Debug.Log("[ServeCounter] 🍲 Đồ ăn đã xuất hiện trên bàn giao món!");
        }
    }

    public bool HasFood()
    {
        return hasFood;
    }

    public bool CanInteract(GameObject player)
    {
        if (!hasFood) return false;

        PlayerFoodCarrier carrier = player.GetComponent<PlayerFoodCarrier>();
        return carrier != null && !carrier.IsCarrying();
    }

    public void Interact(GameObject player)
    {
        if (!CanInteract(player)) return;

        PlayerFoodCarrier carrier = player.GetComponent<PlayerFoodCarrier>();
        if (carrier != null)
        {
            // Transfer food from counter to player's plate
            carrier.SetFood(currentFoodOnCounter);

            // Clear food from counter
            currentFoodOnCounter = null;
            hasFood = false;
            UpdateDisplay();
            Debug.Log("[ServeCounter] 🍽️ MC đã lấy đồ ăn từ quầy lên dĩa!");
        }
    }

    private void UpdateDisplay()
    {
        if (foodDisplayRenderer != null && foodDisplayRenderer.gameObject != gameObject)
        {
            if (overridePositionWithCode)
            {
                foodDisplayRenderer.transform.localPosition = foodLocalOffset;
            }

            foodDisplayRenderer.sprite = currentFoodOnCounter;
            foodDisplayRenderer.enabled = hasFood && currentFoodOnCounter != null;
            ApplySortingOrder();
        }
    }
}
