using UnityEngine;

/// <summary>
/// Controls 2D Top-Down player movement using WASD or Arrow Keys.
/// Compatible with Rigidbody2D (physics/collisions) and direct Transform translation.
/// </summary>
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    [Tooltip("Movement speed of the player in units per second.")]
    [SerializeField] private float moveSpeed = 5f;

    [Header("Components")]
    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private Animator animator;

    private Vector2 movement;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();

        // Configure Rigidbody2D for top-down 2D movement if attached
        if (rb != null)
        {
            rb.gravityScale = 0f;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        }
    }

    private void Update()
    {
        // Get WASD / Arrow Key input
        float moveX = Input.GetAxisRaw("Horizontal");
        float moveY = Input.GetAxisRaw("Vertical");

        movement = new Vector2(moveX, moveY).normalized;

        // Flip sprite based on horizontal direction
        if (spriteRenderer != null && moveX != 0)
        {
            spriteRenderer.flipX = moveX < 0;
        }

        // Update Animator state if available
        if (animator != null)
        {
            bool isMoving = movement.sqrMagnitude > 0.01f;
            animator.SetBool("IsMoving", isMoving);
            if (isMoving)
            {
                animator.SetFloat("MoveX", moveX);
                animator.SetFloat("MoveY", moveY);
            }
        }
    }

    private void FixedUpdate()
    {
        if (movement.sqrMagnitude < 0.001f)
        {
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
            }
            return;
        }

        // Move using Rigidbody2D if available, otherwise Transform
        if (rb != null)
        {
            rb.MovePosition(rb.position + movement * moveSpeed * Time.fixedDeltaTime);
        }
        else
        {
            transform.Translate(movement * moveSpeed * Time.fixedDeltaTime, Space.World);
        }
    }
}
