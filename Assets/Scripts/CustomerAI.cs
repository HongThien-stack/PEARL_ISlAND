using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
/// Manages Customer states: Spawning -> Walking to Table -> Seated Waiting Order -> Waiting for Food -> Eating -> Leaving.
/// Safely plays animation states or parameters without 'Invalid Layer Index' errors or moonwalking.
/// Supports Rigidbody2D physics movement and Waypoint navigation to avoid wall/table clipping.
/// Implements IInteractable for MC taking order and delivering food using 'F' key.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
public class CustomerAI : MonoBehaviour, IInteractable
{
    [System.Serializable]
    public class WaypointGroup
    {
        [Tooltip("One waypoint is selected randomly from this group.")]
        public Transform[] options;
    }

    public enum SeatSide
    {
        Left,
        Right
    }

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
    [Tooltip("Legacy waypoint groups. Prefer assigning the route on each seat in RestaurantManager.")]
    [SerializeField] private WaypointGroup[] waypointGroups;

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
    [Tooltip("Local position of the food when the customer sits on the left side.")]
    [SerializeField] private Vector3 foodDisplayLeftLocalOffset = new Vector3(-0.4f, 0.1f, 0f);
    [Tooltip("Local position of the food when the customer sits on the right side.")]
    [SerializeField] private Vector3 foodDisplayRightLocalOffset = new Vector3(0.4f, 0.1f, 0f);
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
    [SerializeField] private string sitLeftState = "customer-sit-left";
    [FormerlySerializedAs("sitState")]
    [SerializeField] private string sitRightState = "customer-sit-right";
    [SerializeField] private string eatingLeftState = "customer-eat-left";
    [FormerlySerializedAs("eatingState")]
    [SerializeField] private string eatingRightState = "customer-eat-right";

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
    [Tooltip("Bubble background sprite shown behind the requested food.")]
    [SerializeField] private Sprite orderBubbleSprite;
    [Tooltip("SpriteRenderer inside the order bubble that displays the requested food.")]
    [SerializeField] private SpriteRenderer orderFoodRenderer;
    [Tooltip("Optional UI Image inside the order bubble that displays the requested food.")]
    [SerializeField] private Image orderFoodImage;
    [SerializeField] private Vector3 orderBubbleLocalOffset = new Vector3(0f, 0.75f, 0f);
    [SerializeField] private Vector3 orderBubbleScale = new Vector3(0.65f, 0.65f, 1f);

    [Tooltip("Icon or GameObject shown when customer is waiting for food.")]
    [SerializeField] private GameObject waitingFoodBubbleUI;

    [Tooltip("Icon or GameObject shown when customer is eating.")]
    [SerializeField] private GameObject eatingBubbleUI;

    private Vector3 doorPosition;
    private Vector3 seatPosition;
    private SeatSide seatSide = SeatSide.Right;
    private Transform[] selectedWaypoints;
    private Animator animator;
    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private SpriteRenderer orderBubbleRenderer;
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

        if (orderFoodRenderer == null && orderBubbleUI != null)
        {
            orderFoodRenderer = orderBubbleUI.GetComponentInChildren<SpriteRenderer>(true);
        }

        if (orderFoodImage == null && orderBubbleUI != null)
        {
            orderFoodImage = orderBubbleUI.GetComponentInChildren<Image>(true);
        }

        EnsureOrderBubbleVisual();

        if (animator != null && animator.runtimeAnimatorController != null && animator.layerCount == 0)
        {
            animator.Rebind();
        }

        if (rb != null)
        {
            rb.gravityScale = 0f;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        }

        BoxCollider2D collider = GetComponent<BoxCollider2D>();
        if (collider != null)
        {
            collider.enabled = true;
            collider.isTrigger = true;
        }

        UpdateBubbles();
    }

    private void Update()
    {
        if (currentState == CustomerState.WaitingToOrder || currentState == CustomerState.WaitingForFood)
        {
            PlayAnimationState(GetSitState());
        }
        else if (currentState == CustomerState.Eating)
        {
            PlayAnimationState(GetEatingState());
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

            if (orderFoodRenderer != null)
            {
                orderFoodRenderer.sortingLayerID = spriteRenderer.sortingLayerID;
                orderFoodRenderer.sortingOrder = spriteRenderer.sortingOrder + 10;
            }

            if (orderBubbleRenderer != null)
            {
                orderBubbleRenderer.sortingLayerID = spriteRenderer.sortingLayerID;
                orderBubbleRenderer.sortingOrder = spriteRenderer.sortingOrder + 9;
            }
        }
    }

    /// <summary>
    /// Initialize Customer spawn at door and assign seat target.
    /// </summary>
    public void InitCustomer(Vector3 doorPos, Vector3 seatPos, Sprite foodToOrder, System.Action onCustomerLeft = null)
    {
        InitCustomer(doorPos, seatPos, SeatSide.Right, foodToOrder, null, onCustomerLeft);
    }

    public void InitCustomer(
        Vector3 doorPos,
        Vector3 seatPos,
        SeatSide targetSeatSide,
        Sprite foodToOrder,
        Transform[] routeWaypoints,
        System.Action onCustomerLeft = null)
    {
        doorPosition = doorPos;
        seatPosition = seatPos;
        seatSide = targetSeatSide;
        desiredFoodSprite = foodToOrder;
        onCustomerLeftCallback = onCustomerLeft;
        selectedWaypoints = routeWaypoints != null && routeWaypoints.Length > 0
            ? routeWaypoints
            : BuildRandomWaypointRoute();

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
        if (selectedWaypoints != null && selectedWaypoints.Length > 0)
        {
            foreach (Transform wp in selectedWaypoints)
            {
                if (wp != null) yield return StartCoroutine(NavigateToTarget(wp.position));
            }
        }

        yield return StartCoroutine(NavigateToTarget(seatPosition));

        // Arrived at seat
        currentState = CustomerState.WaitingToOrder;
        UpdateBubbles();
        PlayAnimationState(GetSitState());
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
        PlayAnimationState(GetEatingState());
        ShowFoodOnTable(foodSprite);

        yield return new WaitForSeconds(eatingDuration);

        HideFoodOnTable();

        // Finished eating -> Leave
        currentState = CustomerState.Leaving;
        UpdateBubbles();
        Debug.Log("[CustomerAI] 💵 Khách hàng đã ăn xong, trả tiền và đi ra cửa!");

        // Walk back through waypoints in reverse
        if (selectedWaypoints != null && selectedWaypoints.Length > 0)
        {
            for (int i = selectedWaypoints.Length - 1; i >= 0; i--)
            {
                if (selectedWaypoints[i] != null) yield return StartCoroutine(NavigateToTarget(selectedWaypoints[i].position));
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
            tableFoodRenderer.transform.localPosition = GetFoodDisplayLocalOffset();
            tableFoodRenderer.transform.localScale = foodDisplayScale;
            tableFoodRenderer.gameObject.SetActive(true);
        }
    }

    private Vector3 GetFoodDisplayLocalOffset()
    {
        Vector3 sideOffset = seatSide == SeatSide.Left
            ? foodDisplayLeftLocalOffset
            : foodDisplayRightLocalOffset;

        if (sideOffset == Vector3.zero && foodDisplayLocalOffset != Vector3.zero)
        {
            return foodDisplayLocalOffset;
        }

        return sideOffset;
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
        if (useAxisAlignedMovement && (selectedWaypoints == null || selectedWaypoints.Length == 0))
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
                PlayAnimationState(GetEatingState());
                return;
            }

            if (currentState == CustomerState.WaitingToOrder || currentState == CustomerState.WaitingForFood)
            {
                PlayAnimationState(GetSitState());
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

    private Transform[] BuildRandomWaypointRoute()
    {
        if (waypointGroups == null || waypointGroups.Length == 0)
        {
            return waypointsToSeat;
        }

        var route = new System.Collections.Generic.List<Transform>();
        foreach (WaypointGroup group in waypointGroups)
        {
            if (group == null || group.options == null || group.options.Length == 0)
            {
                continue;
            }

            Transform selected = group.options[Random.Range(0, group.options.Length)];
            if (selected != null)
            {
                route.Add(selected);
            }
        }

        if (route.Count < 2)
        {
            Debug.LogWarning("[CustomerAI] Waypoint route needs at least two valid waypoints. Falling back to the legacy route.");
            return waypointsToSeat;
        }

        return route.ToArray();
    }

    private string GetSitState()
    {
        return seatSide == SeatSide.Left ? sitLeftState : sitRightState;
    }

    private string GetEatingState()
    {
        return seatSide == SeatSide.Left ? eatingLeftState : eatingRightState;
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
        bool isOrdering = currentState == CustomerState.WaitingToOrder;
        if (orderBubbleUI != null) orderBubbleUI.SetActive(isOrdering);
        if (orderFoodRenderer != null)
        {
            orderFoodRenderer.sprite = desiredFoodSprite;
            orderFoodRenderer.enabled = isOrdering && desiredFoodSprite != null;
        }
        if (orderFoodImage != null)
        {
            orderFoodImage.sprite = desiredFoodSprite;
            orderFoodImage.enabled = isOrdering && desiredFoodSprite != null;
        }
        if (waitingFoodBubbleUI != null) waitingFoodBubbleUI.SetActive(currentState == CustomerState.WaitingForFood);
        if (eatingBubbleUI != null) eatingBubbleUI.SetActive(currentState == CustomerState.Eating);
    }

    private void EnsureOrderBubbleVisual()
    {
        if (orderBubbleUI == null)
        {
            orderBubbleUI = new GameObject("OrderBubbleUI");
            orderBubbleUI.transform.SetParent(transform, false);
            orderBubbleUI.transform.localPosition = orderBubbleLocalOffset;
            orderBubbleUI.transform.localScale = orderBubbleScale;

            orderBubbleRenderer = orderBubbleUI.AddComponent<SpriteRenderer>();
            orderBubbleRenderer.sprite = orderBubbleSprite;
            orderBubbleRenderer.sortingLayerID = spriteRenderer != null ? spriteRenderer.sortingLayerID : 0;
            orderBubbleRenderer.sortingOrder = 19;

            GameObject foodIcon = new GameObject("FoodIcon");
            foodIcon.transform.SetParent(orderBubbleUI.transform, false);
            foodIcon.transform.localPosition = Vector3.zero;
            orderFoodRenderer = foodIcon.AddComponent<SpriteRenderer>();
            orderFoodRenderer.sortingLayerID = orderBubbleRenderer.sortingLayerID;
            orderFoodRenderer.sortingOrder = 20;
            return;
        }

        if (orderBubbleRenderer == null)
        {
            orderBubbleRenderer = orderBubbleUI.GetComponent<SpriteRenderer>();
            if (orderBubbleRenderer == null)
            {
                orderBubbleRenderer = orderBubbleUI.AddComponent<SpriteRenderer>();
            }
        }

        orderBubbleRenderer.sprite = orderBubbleSprite;

        if (orderFoodRenderer == null && orderFoodImage == null)
        {
            GameObject foodIcon = new GameObject("FoodIcon");
            foodIcon.transform.SetParent(orderBubbleUI.transform, false);
            foodIcon.transform.localPosition = Vector3.zero;
            orderFoodRenderer = foodIcon.AddComponent<SpriteRenderer>();
        }
    }
}
