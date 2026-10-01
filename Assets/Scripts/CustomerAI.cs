using System.Collections;
using UnityEngine;

/// <summary>
/// Manages Customer states: Spawning -> Walking to Table -> Seated Waiting Order -> Waiting for Food -> Eating -> Leaving.
/// Safely plays animation states or parameters without 'Invalid Layer Index' errors or moonwalking.
/// Supports Rigidbody2D physics movement and Waypoint navigation to avoid wall/table clipping.
/// Implements IInteractable for MC taking order and delivering food using 'F' key.
/// </summary>
public class CustomerAI : MonoBehaviour, IInteractable
{
    public enum CustomerState
    {
        None,
        WalkingToSeat,
        WaitingToOrder,
        WaitingForFood,
        Eating,
        Leaving
    }

    [Header("Customer State")]
    [SerializeField] private CustomerState currentState = CustomerState.None;

    [Header("Order Info")]
    [Tooltip("The food item sprite this customer wants to order.")]
    [SerializeField] private Sprite desiredFoodSprite;

    [Header("Settings")]
    [SerializeField] private float moveSpeed = 2.5f;
    [SerializeField] private float eatingDuration = 4f;

    [Header("Navigation & Pathing")]
    [Tooltip("Optional waypoints from Door to Seat to avoid tables and obstacles.")]
    [SerializeField] private Transform[] waypointsToSeat;

    [Tooltip("Move along X then Y axis if no waypoints are assigned.")]
    [SerializeField] private bool useAxisAlignedMovement = true;

    [Header("Y-Sorting Settings")]
    [Tooltip("Enable dynamic sorting order calculation based on Y-position.")]
    [SerializeField] private bool dynamicYSorting = true;
    [SerializeField] private int sortingOrderOffset = 30000;
    [SerializeField] private float ySortingMultiplier = 1.3f;
    [Tooltip("Additional sorting order offset when customer is seated (+1 to draw in front of chair, -1 to draw behind).")]
    [SerializeField] private int seatedSortingOffset = 0;

    [Header("Table Food Display")]
    [Tooltip("SpriteRenderer on child object displaying food on the table while eating.")]
    [SerializeField] private SpriteRenderer tableFoodRenderer;
    [SerializeField] private Vector3 foodDisplayLocalOffset = new Vector3(0.4f, 0.1f, 0f);
    [SerializeField] private Vector3 foodDisplayScale = new Vector3(0.5f, 0.5f, 1f);
    [SerializeField] private int foodSortingOffset = 2;

    [Header("Direct Animation State Names (Inspector Configurable)")]
    [SerializeField] private string idleDownState = "customer-idle-down";
    [SerializeField] private string idleUpState = "customer-idle-up";
    [SerializeField] private string idleLeftState = "customer-idle-left";
    [SerializeField] private string idleRightState = "customer-idle-right";
    [SerializeField] private string walkDownState = "customer-walk-down";
    [SerializeField] private string walkUpState = "customer-walk-up";
    [SerializeField] private string walkLeftState = "customer-walk-left";
    [SerializeField] private string walkRightState = "customer-walk-right";
    [SerializeField] private string sitState = "customer-sit-right";
    [SerializeField] private string eatingState = "customer-eat-right";

    [Header("Animator Parameter Names (Fallback for Blend Trees)")]
    [SerializeField] private string isMovingParam = "IsMoving";
    [SerializeField] private string isWalkingParam = "IsWalking";
    [SerializeField] private string moveXParam = "MoveX";
    [SerializeField] private string moveYParam = "MoveY";

    private Vector2 primaryDirection = Vector2.down;
    private Vector2 lastDirection = Vector2.down;

    [Header("UI Speech Bubbles / Indicators (Optional)")]
    [Tooltip("Icon or GameObject shown when customer wants to order.")]
    [SerializeField] private GameObject orderBubbleUI;

    [Tooltip("Icon or GameObject shown when customer is waiting for food.")]
    [SerializeField] private GameObject waitingFoodBubbleUI;

    [Tooltip("Icon or GameObject shown when customer is eating.")]
    [SerializeField] private GameObject eatingBubbleUI;

    private Vector3 doorPosition;
    private Vector3 seatPosition;
    private Animator animator;
    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private string currentStateName = "";
    private System.Action onCustomerLeftCallback;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        if (spriteRenderer != null)
        {
            spriteRenderer.spriteSortPoint = SpriteSortPoint.Pivot;
        }

        if (tableFoodRenderer == null)
        {
            Transform foodChild = transform.Find("TableFood");
            if (foodChild != null)
            {
                tableFoodRenderer = foodChild.GetComponent<SpriteRenderer>();
            }
            else
            {
                GameObject newFoodChild = new GameObject("TableFood");
                newFoodChild.transform.SetParent(transform, false);
                newFoodChild.transform.localPosition = foodDisplayLocalOffset;
                newFoodChild.transform.localScale = foodDisplayScale;
                tableFoodRenderer = newFoodChild.AddComponent<SpriteRenderer>();
                newFoodChild.SetActive(false);
            }
        }

        if (animator != null && animator.runtimeAnimatorController != null && animator.layerCount == 0)
        {
            animator.Rebind();
        }

        if (rb != null)
        {
            rb.gravityScale = 0f;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        }

        if (GetComponent<Collider2D>() == null)
        {
            BoxCollider2D col = gameObject.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(1f, 1f);
        }

        UpdateBubbles();
    }

    private void Update()
    {
        if (currentState == CustomerState.WaitingToOrder || currentState == CustomerState.WaitingForFood)
        {
            PlayAnimationState(sitState);
        }
        else if (currentState == CustomerState.Eating)
        {
            PlayAnimationState(eatingState);
        }
    }

    private void LateUpdate()
    {
        if (dynamicYSorting && spriteRenderer != null)
        {
            int baseOrder = sortingOrderOffset + Mathf.RoundToInt(-transform.position.y * ySortingMultiplier);
            bool isSeated = (currentState == CustomerState.WaitingToOrder || currentState == CustomerState.WaitingForFood || currentState == CustomerState.Eating);
            spriteRenderer.sortingOrder = baseOrder + (isSeated ? seatedSortingOffset : 0);

            if (tableFoodRenderer != null && tableFoodRenderer.gameObject.activeSelf)
            {
                tableFoodRenderer.sortingLayerID = spriteRenderer.sortingLayerID;
                tableFoodRenderer.sortingOrder = spriteRenderer.sortingOrder + foodSortingOffset;
            }
        }
    }

    /// <summary>
    /// Initialize Customer spawn at door and assign seat target.
    /// </summary>
    public void InitCustomer(Vector3 doorPos, Vector3 seatPos, Sprite foodToOrder, System.Action onCustomerLeft = null)
    {
        doorPosition = doorPos;
        seatPosition = seatPos;
        desiredFoodSprite = foodToOrder;
        onCustomerLeftCallback = onCustomerLeft;

        transform.position = doorPosition;
        StartCoroutine(WalkToSeatRoutine());
    }

    public CustomerState GetState()
    {
        return currentState;
    }

    private IEnumerator WalkToSeatRoutine()
    {
        currentState = CustomerState.WalkingToSeat;
        UpdateBubbles();

        // Walk through waypoints if assigned
        if (waypointsToSeat != null && waypointsToSeat.Length > 0)
        {
            foreach (Transform wp in waypointsToSeat)
            {
                if (wp != null) yield return StartCoroutine(NavigateToTarget(wp.position));
            }
        }

        yield return StartCoroutine(NavigateToTarget(seatPosition));

        // Arrived at seat
        currentState = CustomerState.WaitingToOrder;
        UpdateBubbles();
        PlayAnimationState(sitState);
        Debug.Log("[CustomerAI] ✅ Khách hàng đã tới ghế và sẵn sàng gọi món!");
    }

    public bool CanInteract(GameObject player)
    {
        if (currentState == CustomerState.WaitingToOrder)
        {
            return true;
        }

        if (currentState == CustomerState.WaitingForFood)
        {
            PlayerFoodCarrier carrier = player.GetComponent<PlayerFoodCarrier>();
            return carrier != null && carrier.IsCarrying();
        }

        return false;
    }

    public void Interact(GameObject player)
    {
        if (currentState == CustomerState.WaitingToOrder)
        {
            currentState = CustomerState.WaitingForFood;
            UpdateBubbles();
            Debug.Log("[CustomerAI] 📝 MC đã nhận đơn thành công! Đã gửi đơn hàng cho Chef.");

            if (RestaurantManager.Instance != null)
            {
                RestaurantManager.Instance.SendOrderToChef(desiredFoodSprite);
            }
            return;
        }

        if (currentState == CustomerState.WaitingForFood)
        {
            PlayerFoodCarrier carrier = player.GetComponent<PlayerFoodCarrier>();
            if (carrier != null && carrier.IsCarrying())
            {
                Sprite deliveredFood = carrier.GetCurrentFoodSprite();
                carrier.ClearFood();
                StartCoroutine(EatingRoutine(deliveredFood));
                Debug.Log("[CustomerAI] 🍲 MC đã giao đồ ăn cho Khách! Khách đang ăn...");
            }
        }
    }

    private IEnumerator EatingRoutine(Sprite foodSprite = null)
    {
        currentState = CustomerState.Eating;
        UpdateBubbles();
        PlayAnimationState(eatingState);
        ShowFoodOnTable(foodSprite);

        yield return new WaitForSeconds(eatingDuration);

        HideFoodOnTable();

        // Finished eating -> Leave
        currentState = CustomerState.Leaving;
        UpdateBubbles();
        Debug.Log("[CustomerAI] 💵 Khách hàng đã ăn xong, trả tiền và đi ra cửa!");

        // Walk back through waypoints in reverse
        if (waypointsToSeat != null && waypointsToSeat.Length > 0)
        {
            for (int i = waypointsToSeat.Length - 1; i >= 0; i--)
            {
                if (waypointsToSeat[i] != null) yield return StartCoroutine(NavigateToTarget(waypointsToSeat[i].position));
            }
        }

        yield return StartCoroutine(NavigateToTarget(doorPosition));

        onCustomerLeftCallback?.Invoke();
        Destroy(gameObject);
    }

    private void ShowFoodOnTable(Sprite foodSprite)
    {
        Sprite spriteToDisplay = foodSprite != null ? foodSprite : desiredFoodSprite;
        if (tableFoodRenderer != null && spriteToDisplay != null)
        {
            tableFoodRenderer.sprite = spriteToDisplay;
            tableFoodRenderer.transform.localPosition = foodDisplayLocalOffset;
            tableFoodRenderer.transform.localScale = foodDisplayScale;
            tableFoodRenderer.gameObject.SetActive(true);
        }
    }

    private void HideFoodOnTable()
    {
        if (tableFoodRenderer != null)
        {
            tableFoodRenderer.gameObject.SetActive(false);
        }
    }

    private IEnumerator NavigateToTarget(Vector3 targetPos)
    {
        if (useAxisAlignedMovement && (waypointsToSeat == null || waypointsToSeat.Length == 0))
        {
            Vector3 intermediateX = new Vector3(targetPos.x, transform.position.y, transform.position.z);
            if (Mathf.Abs(transform.position.x - targetPos.x) > 0.05f)
            {
                yield return StartCoroutine(MoveStraight(intermediateX));
            }

            if (Mathf.Abs(transform.position.y - targetPos.y) > 0.05f)
            {
                yield return StartCoroutine(MoveStraight(targetPos));
            }
        }
        else
        {
            yield return StartCoroutine(MoveStraight(targetPos));
        }
    }

    private IEnumerator MoveStraight(Vector3 targetPos)
    {
        while (Vector2.Distance(transform.position, targetPos) > 0.08f)
        {
            Vector3 direction = (targetPos - transform.position).normalized;
            Vector2 moveDir = new Vector2(direction.x, direction.y);

            UpdateAnimationDirect(moveDir);

            if (rb != null)
            {
                rb.linearVelocity = moveDir * moveSpeed;
            }
            else
            {
                transform.position = Vector3.MoveTowards(transform.position, targetPos, moveSpeed * Time.deltaTime);
            }

            yield return null;
        }

        if (rb != null) rb.linearVelocity = Vector2.zero;
        transform.position = targetPos;
        UpdateAnimationDirect(Vector2.zero);
    }

    private void UpdateAnimationDirect(Vector2 moveDir)
    {
        bool isMoving = moveDir.sqrMagnitude > 0.01f;

        if (animator != null)
        {
            if (HasParameter(animator, isMovingParam)) animator.SetBool(isMovingParam, isMoving);
            if (HasParameter(animator, isWalkingParam)) animator.SetBool(isWalkingParam, isMoving);
            if (HasParameter(animator, moveXParam)) animator.SetFloat(moveXParam, isMoving ? moveDir.x : 0f);
            if (HasParameter(animator, moveYParam)) animator.SetFloat(moveYParam, isMoving ? moveDir.y : 0f);
        }

        if (spriteRenderer != null)
        {
            if (walkLeftState == walkRightState && Mathf.Abs(moveDir.x) > 0.01f)
            {
                spriteRenderer.flipX = moveDir.x < 0f;
            }
            else
            {
                spriteRenderer.flipX = false;
            }
        }

        if (!isMoving)
        {
            if (currentState == CustomerState.Eating)
            {
                PlayAnimationState(eatingState);
                return;
            }

            if (currentState == CustomerState.WaitingToOrder || currentState == CustomerState.WaitingForFood)
            {
                PlayAnimationState(sitState);
                return;
            }

            if (lastDirection.y > 0.1f) PlayAnimationState(idleUpState);
            else if (lastDirection.x < -0.1f) PlayAnimationState(idleLeftState);
            else if (lastDirection.x > 0.1f) PlayAnimationState(idleRightState);
            else PlayAnimationState(idleDownState);
            return;
        }

        // Hysteresis calculation to prevent jittering / moonwalking
        float absX = Mathf.Abs(moveDir.x);
        float absY = Mathf.Abs(moveDir.y);
        float threshold = 0.15f;

        if (primaryDirection.x != 0f)
        {
            if (absY > absX + threshold) primaryDirection = new Vector2(0f, Mathf.Sign(moveDir.y));
            else primaryDirection = new Vector2(Mathf.Sign(moveDir.x), 0f);
        }
        else
        {
            if (absX > absY + threshold) primaryDirection = new Vector2(Mathf.Sign(moveDir.x), 0f);
            else primaryDirection = new Vector2(0f, Mathf.Sign(moveDir.y));
        }

        lastDirection = primaryDirection;

        if (primaryDirection.y > 0f) PlayAnimationState(walkUpState);
        else if (primaryDirection.y < 0f) PlayAnimationState(walkDownState);
        else if (primaryDirection.x < 0f) PlayAnimationState(walkLeftState);
        else PlayAnimationState(walkRightState);
    }

    private void PlayAnimationState(string stateName)
    {
        if (animator == null || string.IsNullOrEmpty(stateName)) return;

        if (animator.runtimeAnimatorController != null)
        {
            if (animator.layerCount == 0)
            {
                animator.Rebind();
            }

            if (animator.layerCount > 0)
            {
                AnimatorStateInfo currentInfo = animator.GetCurrentAnimatorStateInfo(0);
                if (currentStateName != stateName || !currentInfo.IsName(stateName))
                {
                    int stateHash = Animator.StringToHash(stateName);
                    if (animator.HasState(0, stateHash))
                    {
                        animator.Play(stateHash, 0, 0f);
                        animator.Update(0f);
                        currentStateName = stateName;
                    }
                    else
                    {
                        string fallbackName = stateName.StartsWith("customer-") ? stateName.Replace("customer-", "") : "customer-" + stateName;
                        int fallbackHash = Animator.StringToHash(fallbackName);
                        if (animator.HasState(0, fallbackHash))
                        {
                            animator.Play(fallbackHash, 0, 0f);
                            animator.Update(0f);
                            currentStateName = fallbackName;
                        }
                    }
                }
            }
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

    private void UpdateBubbles()
    {
        if (orderBubbleUI != null) orderBubbleUI.SetActive(currentState == CustomerState.WaitingToOrder);
        if (waitingFoodBubbleUI != null) waitingFoodBubbleUI.SetActive(currentState == CustomerState.WaitingForFood);
        if (eatingBubbleUI != null) eatingBubbleUI.SetActive(currentState == CustomerState.Eating);
    }
}
