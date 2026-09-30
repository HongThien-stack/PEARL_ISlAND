using UnityEngine;

/// <summary>
/// Manages holding, displaying, and clearing food items on the player's plate.
/// Synchronizes sorting order with the main player SpriteRenderer for dynamic Y-sorting compatibility.
/// </summary>
public class PlayerFoodCarrier : MonoBehaviour
{
    [Header("Food Renderer Reference")]
    [Tooltip("The SpriteRenderer on the child GameObject (e.g. HeldFood) attached to the plate position.")]
    [SerializeField] private SpriteRenderer foodSpriteRenderer;

    [Header("Animator Integration (Optional)")]
    [Tooltip("Reference to the player's Animator component.")]
    [SerializeField] private Animator animator;

    [Tooltip("Animator parameter name for triggering carry animations (e.g. IsCarrying). Leave empty if not using.")]
    [SerializeField] private string isCarryingParam = "IsCarrying";

    [Header("Sorting Sync")]
    [Tooltip("If true, food sorting order will stay relative to the player's current sorting order.")]
    [SerializeField] private bool syncSortingWithPlayer = true;

    [Tooltip("Offset added to the player's sorting order (+1 to render in front, -1 to render behind).")]
    [SerializeField] private int foodSortingOffset = 1;

    [Header("Current State")]
    [SerializeField] private Sprite currentFoodSprite;
    [SerializeField] private bool isCarryingFood = false;

    private SpriteRenderer playerSpriteRenderer;

    private void Awake()
    {
        playerSpriteRenderer = GetComponent<SpriteRenderer>();

        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        // Auto-find child SpriteRenderer if not manually assigned
        if (foodSpriteRenderer == null)
        {
            Transform foodChild = transform.Find("HeldFood");
            if (foodChild != null)
            {
                foodSpriteRenderer = foodChild.GetComponent<SpriteRenderer>();
            }
        }

        // Apply initial state
        if (currentFoodSprite != null)
        {
            SetFood(currentFoodSprite);
        }
        else
        {
            ClearFood();
        }
    }

    private void LateUpdate()
    {
        // Sync sorting order with player's dynamic Y-sorting
        if (syncSortingWithPlayer && isCarryingFood && foodSpriteRenderer != null && playerSpriteRenderer != null)
        {
            foodSpriteRenderer.sortingLayerID = playerSpriteRenderer.sortingLayerID;
            foodSpriteRenderer.sortingOrder = playerSpriteRenderer.sortingOrder + foodSortingOffset;
        }
    }

    /// <summary>
    /// Place a food item onto the plate and display it.
    /// </summary>
    /// <param name="foodSprite">The Sprite image of the food to deliver.</param>
    public void SetFood(Sprite foodSprite)
    {
        if (foodSprite == null)
        {
            ClearFood();
            return;
        }

        currentFoodSprite = foodSprite;
        isCarryingFood = true;

        if (foodSpriteRenderer != null)
        {
            foodSpriteRenderer.sprite = currentFoodSprite;
            foodSpriteRenderer.gameObject.SetActive(true);
        }

        UpdateAnimatorState(true);
    }

    /// <summary>
    /// Remove food item from the plate (after delivery or drop).
    /// </summary>
    public void ClearFood()
    {
        currentFoodSprite = null;
        isCarryingFood = false;

        if (foodSpriteRenderer != null)
        {
            foodSpriteRenderer.sprite = null;
            foodSpriteRenderer.gameObject.SetActive(false);
        }

        UpdateAnimatorState(false);
    }

    /// <summary>
    /// Dynamically adjust the sorting offset (e.g. when facing Up vs Down in animations).
    /// </summary>
    /// <param name="offset">+1 to render on top of MC, -1 to render behind MC.</param>
    public void SetSortingOffset(int offset)
    {
        foodSortingOffset = offset;
    }

    /// <summary>
    /// Check if the player is currently holding any food.
    /// </summary>
    public bool IsCarrying()
    {
        return isCarryingFood;
    }

    /// <summary>
    /// Get the Sprite of the food currently being carried.
    /// </summary>
    public Sprite GetCurrentFoodSprite()
    {
        return currentFoodSprite;
    }

    private void UpdateAnimatorState(bool carrying)
    {
        if (animator != null && HasParameter(animator, isCarryingParam))
        {
            animator.SetBool(isCarryingParam, carrying);
        }
    }

    private bool HasParameter(Animator anim, string paramName)
    {
        if (anim == null || string.IsNullOrEmpty(paramName)) return false;
        foreach (AnimatorControllerParameter param in anim.parameters)
        {
            if (param.name == paramName) return true;
        }
        return false;
    }
}
