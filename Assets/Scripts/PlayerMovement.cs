using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Top-down 2D Player Movement for MC.
/// Uses Unity's New Input System (Keyboard & Gamepad) with Rigidbody2D velocity.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float sprintMultiplier = 1.4f;

    [Header("Animator Parameter Names")]
    [SerializeField] private string isMovingParam = "IsMoving";
    [SerializeField] private string moveXParam = "MoveX";
    [SerializeField] private string moveYParam = "MoveY";
    [SerializeField] private string lastMoveXParam = "LastMoveX";
    [SerializeField] private string lastMoveYParam = "LastMoveY";

    private Rigidbody2D rb;
    private Animator animator;
    private Vector2 moveInput;
    private Vector2 lastDirection = Vector2.down;
    private bool isSprinting = false;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();

        if (rb != null)
        {
            rb.gravityScale = 0f;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        }
    }

    private void Update()
    {
        // 1. Read input using New Input System
        float x = 0f;
        float y = 0f;

        Keyboard kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed)    y += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed)  y -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) x += 1f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed)  x -= 1f;

            isSprinting = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
        }

        // Gamepad support
        Gamepad gp = Gamepad.current;
        if (gp != null && Mathf.Approximately(x, 0f) && Mathf.Approximately(y, 0f))
        {
            Vector2 stick = gp.leftStick.ReadValue();
            x = stick.x;
            y = stick.y;
            if (gp.leftShoulder.isPressed || gp.buttonSouth.isPressed)
            {
                isSprinting = true;
            }
        }

        moveInput = new Vector2(x, y);
        if (moveInput.sqrMagnitude > 1f)
        {
            moveInput.Normalize();
        }

        // Determine dominant 4-directional vector for Animator to prevent diagonal animation flickering
        Vector2 animDir = Vector2.zero;
        if (moveInput.sqrMagnitude > 0.001f)
        {
            if (Mathf.Abs(moveInput.x) > Mathf.Abs(moveInput.y))
            {
                animDir = new Vector2(Mathf.Sign(moveInput.x), 0f);
            }
            else if (Mathf.Abs(moveInput.y) > Mathf.Abs(moveInput.x))
            {
                animDir = new Vector2(0f, Mathf.Sign(moveInput.y));
            }
            else
            {
                // When |x| == |y| (diagonal 45 deg), keep previous dominant direction to eliminate jitter
                if (Mathf.Abs(lastDirection.x) > 0.1f)
                {
                    animDir = new Vector2(Mathf.Sign(moveInput.x), 0f);
                }
                else
                {
                    animDir = new Vector2(0f, Mathf.Sign(moveInput.y));
                }
            }

            lastDirection = animDir;
        }

        // 2. Update Animator
        if (animator != null && animator.isActiveAndEnabled)
        {
            bool isMoving = moveInput.sqrMagnitude > 0.001f;
            animator.SetBool(isMovingParam, isMoving);
            animator.SetFloat(moveXParam, isMoving ? animDir.x : 0f);
            animator.SetFloat(moveYParam, isMoving ? animDir.y : 0f);
            animator.SetFloat(lastMoveXParam, lastDirection.x);
            animator.SetFloat(lastMoveYParam, lastDirection.y);
        }
    }

    private void FixedUpdate()
    {
        float speed = moveSpeed * (isSprinting ? sprintMultiplier : 1f);

        if (rb != null)
        {
            rb.linearVelocity = moveInput * speed;
        }
        else
        {
            transform.position += (Vector3)(moveInput * speed * Time.fixedDeltaTime);
        }
    }

    public void SetMovementEnabled(bool value)
    {
        enabled = value;
        if (!value && rb != null) rb.linearVelocity = Vector2.zero;
    }
}
