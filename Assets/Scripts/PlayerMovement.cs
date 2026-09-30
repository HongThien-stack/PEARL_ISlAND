using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Controls 2D Top-Down player movement compatible with Unity New Input System and Legacy Input.
/// Features Direction Hysteresis, Dynamic Y-Sorting, and Carry Animations when holding food.
/// Updates Animator parameters (IsMoving, MoveX, MoveY, LastMoveX, LastMoveY, IsCarrying) 
/// and plays direct carry states (mc-walk-carry-down, mc-idle-carry-down, etc.).
/// </summary>
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    [Tooltip("Movement speed of the player in units per second.")]
    [SerializeField] private float moveSpeed = 5f;

    [Tooltip("Sprint multiplier when holding Left Shift.")]
    [SerializeField] private float sprintMultiplier = 1.4f;

    [Header("Y-Sorting Settings")]
    [Tooltip("Enable dynamic sorting order calculation based on Y-position.")]
    [SerializeField] private bool dynamicYSorting = true;
    [SerializeField] private int sortingOrderOffset = 30000;
    [SerializeField] private float ySortingMultiplier = 1.3f;

    [Header("Animator Parameter Names")]
    [SerializeField] private string isMovingParam = "IsMoving";
    [SerializeField] private string moveXParam = "MoveX";
    [SerializeField] private string moveYParam = "MoveY";
    [SerializeField] private string lastMoveXParam = "LastMoveX";
    [SerializeField] private string lastMoveYParam = "LastMoveY";
    [SerializeField] private string isCarryingParam = "IsCarrying";

    [Header("Carry Animation State Names (Fallback Direct Play)")]
    [SerializeField] private string idleCarryDown = "mc-idle-carry-down";
    [SerializeField] private string idleCarryUp = "mc-idle-carry-up";
    [SerializeField] private string idleCarryLeft = "mc-idle-carry-left";
    [SerializeField] private string idleCarryRight = "mc-idle-carry-right";
    [SerializeField] private string walkCarryDown = "mc-walk-carry-down";
    [SerializeField] private string walkCarryUp = "mc-walk-carry-up";
    [SerializeField] private string walkCarryLeft = "mc-walk-carry-left";
    [SerializeField] private string walkCarryRight = "mc-walk-carry-right";

    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private Animator animator;
    private PlayerFoodCarrier foodCarrier;

    private Vector2 moveInput;
    private Vector2 moveVelocity;
    private Vector2 primaryDirection = Vector2.down;
    private Vector2 lastDirection = Vector2.down;
    private string currentAnimationState = "";

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
        foodCarrier = GetComponent<PlayerFoodCarrier>();

        if (rb != null)
        {
            rb.gravityScale = 0f;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        }

        if (spriteRenderer != null)
        {
            spriteRenderer.spriteSortPoint = SpriteSortPoint.Pivot;
        }
    }

    private void Update()
    {
        // 1. Read input
        moveInput = ReadMovementInput().normalized;
        bool isSprinting = ReadSprintInput();

        float currentSpeed = moveSpeed * (isSprinting ? sprintMultiplier : 1f);
        moveVelocity = moveInput * currentSpeed;

        // 2. Lock directional state with hysteresis
        UpdateDirectionHysteresis();

        // 3. Update Animator parameters and Carry state
        UpdateAnimatorParameters();
    }

    private void LateUpdate()
    {
        // Dynamic Y-Sorting
        if (dynamicYSorting && spriteRenderer != null)
        {
            spriteRenderer.sortingOrder = sortingOrderOffset + Mathf.RoundToInt(-transform.position.y * ySortingMultiplier);
        }
    }

    private void FixedUpdate()
    {
        if (rb != null)
        {
            rb.linearVelocity = moveVelocity;
        }
        else
        {
            transform.Translate(moveVelocity * Time.fixedDeltaTime, Space.World);
        }
    }

    private void UpdateDirectionHysteresis()
    {
        if (moveInput.sqrMagnitude < 0.01f) return;

        float absX = Mathf.Abs(moveInput.x);
        float absY = Mathf.Abs(moveInput.y);
        float threshold = 0.15f;

        if (primaryDirection.x != 0f)
        {
            if (absY > absX + threshold)
            {
                primaryDirection = new Vector2(0f, Mathf.Sign(moveInput.y));
            }
            else
            {
                primaryDirection = new Vector2(Mathf.Sign(moveInput.x), 0f);
            }
        }
        else
        {
            if (absX > absY + threshold)
            {
                primaryDirection = new Vector2(Mathf.Sign(moveInput.x), 0f);
            }
            else
            {
                primaryDirection = new Vector2(0f, Mathf.Sign(moveInput.y));
            }
        }

        lastDirection = primaryDirection;
    }

    private Vector2 ReadMovementInput()
    {
        float moveX = 0f;
        float moveY = 0f;

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) moveY += 1f;
            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) moveY -= 1f;
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) moveX += 1f;
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) moveX -= 1f;
        }
#else
        try
        {
            moveX = Input.GetAxisRaw("Horizontal");
            moveY = Input.GetAxisRaw("Vertical");
        }
        catch { }
#endif

        return new Vector2(moveX, moveY);
    }

    private bool ReadSprintInput()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            return Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed;
        }
        return false;
#else
        try
        {
            return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        }
        catch { return false; }
#endif
    }

    private void UpdateAnimatorParameters()
    {
        if (animator == null) return;

        bool isMoving = moveInput.sqrMagnitude > 0.01f;
        bool isCarrying = foodCarrier != null && foodCarrier.IsCarrying();

        // 1. Set Parameters for Blend Trees & AnyState Carry Transitions
        if (HasParameter(animator, isMovingParam)) animator.SetBool(isMovingParam, isMoving);
        if (HasParameter(animator, isCarryingParam)) animator.SetBool(isCarryingParam, isCarrying);
        if (HasParameter(animator, moveXParam)) animator.SetFloat(moveXParam, isMoving ? primaryDirection.x : 0f);
        if (HasParameter(animator, moveYParam)) animator.SetFloat(moveYParam, isMoving ? primaryDirection.y : 0f);
        if (HasParameter(animator, lastMoveXParam)) animator.SetFloat(lastMoveXParam, lastDirection.x);
        if (HasParameter(animator, lastMoveYParam)) animator.SetFloat(lastMoveYParam, lastDirection.y);

        // 2. Direct State Play fallback ONLY if Animator Controller does NOT have IsCarrying parameter
        if (isCarrying && !HasParameter(animator, isCarryingParam))
        {
            string targetState = "";
            Vector2 dir = isMoving ? primaryDirection : lastDirection;

            if (dir.y < 0f) targetState = isMoving ? walkCarryDown : idleCarryDown;
            else if (dir.y > 0f) targetState = isMoving ? walkCarryUp : idleCarryUp;
            else if (dir.x < 0f) targetState = isMoving ? walkCarryLeft : idleCarryLeft;
            else if (dir.x > 0f) targetState = isMoving ? walkCarryRight : idleCarryRight;

            PlayStateIfExist(targetState);
        }
        else if (!string.IsNullOrEmpty(currentAnimationState) && currentAnimationState.Contains("carry"))
        {
            currentAnimationState = "";
        }
    }

    private void PlayStateIfExist(string stateName)
    {
        if (string.IsNullOrEmpty(stateName)) return;

        if (currentAnimationState != stateName)
        {
            if (animator != null && animator.runtimeAnimatorController != null && animator.layerCount > 0)
            {
                int hash = Animator.StringToHash(stateName);
                if (animator.HasState(0, hash))
                {
                    animator.Play(hash, 0, 0f);
                    currentAnimationState = stateName;
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
}
