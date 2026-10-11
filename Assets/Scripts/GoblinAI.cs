using System.Collections;
using UnityEngine;

/// <summary>
/// Comprehensive Top-down 2D AI for Goblin Monster.
/// Supports Idle, Patrol, Player Detection, Chasing, 4-Directional Attacks, and Death.
/// Integrates seamlessly with Goblin Animator, Hitbox/Hurtbox system, and Health.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Health))]
public class GoblinAI : MonoBehaviour
{
    public enum State
    {
        Idle,
        Patrol,
        Chase,
        Attack,
        Dead
    }

    [Header("Current State")]
    [SerializeField] private State currentState = State.Idle;

    [Header("Detection & Combat Ranges")]
    [Tooltip("Radius within which Goblin detects MC and starts chasing.")]
    [SerializeField] private float detectionRadius = 8.0f;

    [Tooltip("Distance at which Goblin stops chasing and attacks.")]
    [SerializeField] private float attackRange = 2.2f;

    [Tooltip("Radius within which Goblin loses interest if MC runs away.")]
    [SerializeField] private float loseTargetRadius = 12.0f;

    [Header("Movement Speeds")]
    [SerializeField] private float patrolSpeed = 1.8f;
    [SerializeField] private float chaseSpeed = 3.2f;

    [Header("Patrol Settings")]
    [Tooltip("Maximum wander distance from spawn position.")]
    [SerializeField] private float wanderRadius = 3.5f;
    [SerializeField] private float idleDurationAtPoint = 2.0f;

    [Header("Combat Settings")]
    [SerializeField] private float attackCooldown = 1.2f;
    [SerializeField] private float attackDamage = 15f;
    [SerializeField] private float attackKnockback = 3.0f;
    [SerializeField] private float attackDuration = 0.6f;

    [Header("Animator Parameter Names")]
    [SerializeField] private string isMovingParam = "IsMoving";
    [SerializeField] private string moveXParam = "MoveX";
    [SerializeField] private string moveYParam = "MoveY";
    [SerializeField] private string lastMoveXParam = "LastMoveX";
    [SerializeField] private string lastMoveYParam = "LastMoveY";
    [SerializeField] private string isAttackingParam = "IsAttacking";
    [SerializeField] private string isDeadParam = "IsDead";

    private Rigidbody2D rb;
    private Animator animator;
    private Health health;
    private Transform playerTarget;

    private Vector2 spawnPoint;
    private Vector2 currentPatrolTarget;
    private Vector2 facingDirection = Vector2.down;
    private float stateTimer = 0f;
    private float lastAttackTime = -999f;
    private bool isAttacking = false;

    private Hitbox attackHitbox;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        health = GetComponent<Health>();

        // Physics setup
        if (rb != null)
        {
            rb.gravityScale = 0f;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            rb.linearDamping = 6f; // Dampen sliding
            rb.mass = 5f;          // Make solid so player cannot push easily
        }

        spawnPoint = transform.position;
        currentPatrolTarget = spawnPoint;

        EnsureHurtbox();
        EnsureAttackHitbox();
        EnsureFloatingHealthBar();
    }

    private void Start()
    {
        FindPlayerTarget();
    }

    private void OnEnable()
    {
        if (health != null)
        {
            health.OnDeath += HandleDeath;
            health.OnTakeDamage += HandleTakeDamage;
        }
    }

    private void OnDisable()
    {
        if (health != null)
        {
            health.OnDeath -= HandleDeath;
            health.OnTakeDamage -= HandleTakeDamage;
        }
    }

    private void Update()
    {
        if (currentState == State.Dead) return;

        if (playerTarget == null)
        {
            FindPlayerTarget();
        }

        switch (currentState)
        {
            case State.Idle:
                UpdateIdle();
                break;
            case State.Patrol:
                UpdatePatrol();
                break;
            case State.Chase:
                UpdateChase();
                break;
            case State.Attack:
                UpdateAttack();
                break;
        }

        UpdateAnimator();
    }

    private void FixedUpdate()
    {
        if (currentState == State.Dead || isAttacking)
        {
            if (rb != null) rb.linearVelocity = Vector2.zero;
            return;
        }

        switch (currentState)
        {
            case State.Patrol:
                MoveTowards(currentPatrolTarget, patrolSpeed);
                break;
            case State.Chase:
                if (playerTarget != null)
                {
                    float dist = Vector2.Distance(transform.position, playerTarget.position);
                    if (dist > attackRange * 0.85f)
                    {
                        MoveTowards(playerTarget.position, chaseSpeed);
                    }
                    else
                    {
                        rb.linearVelocity = Vector2.zero;
                    }
                }
                break;
            default:
                if (rb != null) rb.linearVelocity = Vector2.zero;
                break;
        }
    }

    #region AI Logic

    private void UpdateIdle()
    {
        stateTimer += Time.deltaTime;

        // Check for player detection
        if (playerTarget != null)
        {
            float dist = Vector2.Distance(transform.position, playerTarget.position);
            if (dist <= detectionRadius)
            {
                currentState = State.Chase;
                return;
            }
        }

        // Switch to patrol after idle duration
        if (stateTimer >= idleDurationAtPoint)
        {
            stateTimer = 0f;
            currentPatrolTarget = spawnPoint + Random.insideUnitCircle * wanderRadius;
            currentState = State.Patrol;
        }
    }

    private void UpdatePatrol()
    {
        // Check for player detection
        if (playerTarget != null)
        {
            float dist = Vector2.Distance(transform.position, playerTarget.position);
            if (dist <= detectionRadius)
            {
                currentState = State.Chase;
                return;
            }
        }

        // Check if reached patrol destination
        if (Vector2.Distance(transform.position, currentPatrolTarget) <= 0.3f)
        {
            stateTimer = 0f;
            currentState = State.Idle;
        }
    }

    private void UpdateChase()
    {
        if (playerTarget == null)
        {
            currentState = State.Idle;
            return;
        }

        float dist = Vector2.Distance(transform.position, playerTarget.position);

        // Check if lost player
        if (dist > loseTargetRadius)
        {
            currentState = State.Idle;
            stateTimer = 0f;
            return;
        }

        // Face player
        Vector2 dir = ((Vector2)playerTarget.position - (Vector2)transform.position).normalized;
        if (dir.sqrMagnitude > 0.01f)
        {
            facingDirection = dir;
        }

        // Check if in attack range
        if (dist <= attackRange && Time.time >= lastAttackTime + attackCooldown)
        {
            StartCoroutine(ExecuteAttackRoutine());
        }
    }

    private void UpdateAttack()
    {
        // Logic handled inside ExecuteAttackRoutine
    }

    private IEnumerator ExecuteAttackRoutine()
    {
        currentState = State.Attack;
        isAttacking = true;
        lastAttackTime = Time.time;

        if (rb != null) rb.linearVelocity = Vector2.zero;

        // Determine attack direction towards player
        if (playerTarget != null)
        {
            Vector2 dir = ((Vector2)playerTarget.position - (Vector2)transform.position).normalized;
            if (dir.sqrMagnitude > 0.001f)
            {
                facingDirection = dir;
            }
        }

        // Snap attack direction to 4 dominant directions
        Vector2 attackAnimDir = Vector2.zero;
        if (Mathf.Abs(facingDirection.x) > Mathf.Abs(facingDirection.y))
        {
            attackAnimDir = new Vector2(Mathf.Sign(facingDirection.x), 0f);
        }
        else
        {
            attackAnimDir = new Vector2(0f, Mathf.Sign(facingDirection.y));
        }

        if (animator != null)
        {
            animator.SetBool(isMovingParam, false);
            animator.SetBool(isAttackingParam, true);
            animator.SetFloat(moveXParam, 0f);
            animator.SetFloat(moveYParam, 0f);
            animator.SetFloat(lastMoveXParam, attackAnimDir.x);
            animator.SetFloat(lastMoveYParam, attackAnimDir.y);
        }

        PositionHitbox(attackAnimDir);

        // Delay to hit frame
        yield return new WaitForSeconds(0.2f);

        if (attackHitbox != null)
        {
            attackHitbox.gameObject.SetActive(true);
        }

        // Keep hitbox active briefly
        yield return new WaitForSeconds(0.2f);

        if (attackHitbox != null)
        {
            attackHitbox.gameObject.SetActive(false);
        }

        // Remainder of attack animation
        float remaining = Mathf.Max(0.05f, attackDuration - 0.4f);
        yield return new WaitForSeconds(remaining);

        if (animator != null)
        {
            animator.SetBool(isAttackingParam, false);
        }

        isAttacking = false;
        currentState = State.Chase;
    }

    private void MoveTowards(Vector2 targetPos, float speed)
    {
        Vector2 currentPos = rb != null ? rb.position : (Vector2)transform.position;
        Vector2 dir = (targetPos - currentPos).normalized;

        if (dir.sqrMagnitude > 0.001f)
        {
            facingDirection = dir;
            if (rb != null)
            {
                rb.linearVelocity = dir * speed;
            }
            else
            {
                transform.position += (Vector3)(dir * speed * Time.fixedDeltaTime);
            }
        }
        else
        {
            if (rb != null) rb.linearVelocity = Vector2.zero;
        }
    }

    private void UpdateAnimator()
    {
        if (animator == null || !animator.isActiveAndEnabled) return;

        if (isAttacking)
        {
            animator.SetBool(isAttackingParam, true);
            animator.SetBool(isMovingParam, false);
            return;
        }

        bool isMoving = (currentState == State.Patrol || currentState == State.Chase);
        if (rb != null && rb.linearVelocity.sqrMagnitude < 0.05f)
        {
            isMoving = false;
        }

        // Snap facingDirection to 4 dominant directions for Animator
        Vector2 animDir = Vector2.zero;
        if (facingDirection.sqrMagnitude > 0.001f)
        {
            if (Mathf.Abs(facingDirection.x) > Mathf.Abs(facingDirection.y))
            {
                animDir = new Vector2(Mathf.Sign(facingDirection.x), 0f);
            }
            else
            {
                animDir = new Vector2(0f, Mathf.Sign(facingDirection.y));
            }
        }

        animator.SetBool(isAttackingParam, false);
        animator.SetBool(isMovingParam, isMoving);
        animator.SetFloat(moveXParam, isMoving ? animDir.x : 0f);
        animator.SetFloat(moveYParam, isMoving ? animDir.y : 0f);
        animator.SetFloat(lastMoveXParam, animDir.x);
        animator.SetFloat(lastMoveYParam, animDir.y);
    }

    #endregion

    #region Setup & Helpers

    private void FindPlayerTarget()
    {
        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
        {
            playerTarget = playerObj.transform;
        }
        else
        {
            PlayerMovement pm = Object.FindFirstObjectByType<PlayerMovement>();
            if (pm != null)
            {
                playerTarget = pm.transform;
            }
        }
    }

    private void EnsureHurtbox()
    {
        Hurtbox hurtbox = GetComponentInChildren<Hurtbox>();
        if (hurtbox == null)
        {
            GameObject hurtboxGO = new GameObject("GoblinHurtbox");
            hurtboxGO.transform.SetParent(transform, false);
            hurtboxGO.transform.localPosition = Vector3.zero;

            CircleCollider2D col = hurtboxGO.AddComponent<CircleCollider2D>();
            col.radius = 0.6f;
            col.isTrigger = true;

            hurtbox = hurtboxGO.AddComponent<Hurtbox>();
            hurtbox.OwnerFaction = Faction.Enemy;
        }
    }

    private void EnsureAttackHitbox()
    {
        Transform existing = transform.Find("GoblinAttackHitbox");
        if (existing != null)
        {
            attackHitbox = existing.GetComponent<Hitbox>();
        }

        if (attackHitbox == null)
        {
            GameObject hbGO = new GameObject("GoblinAttackHitbox");
            hbGO.transform.SetParent(transform, false);

            CircleCollider2D col = hbGO.AddComponent<CircleCollider2D>();
            col.radius = 0.65f;
            col.isTrigger = true;

            attackHitbox = hbGO.AddComponent<Hitbox>();
            attackHitbox.OwnerFaction = Faction.Enemy;
            attackHitbox.Damage = attackDamage;
            attackHitbox.KnockbackForce = attackKnockback;
            attackHitbox.gameObject.SetActive(false);
        }
    }

    private void PositionHitbox(Vector2 dir)
    {
        if (attackHitbox == null) return;

        // Offset hitbox in the attack direction
        Vector3 offset = (Vector3)(dir.normalized * 0.75f);
        attackHitbox.transform.localPosition = offset;
    }

    private void EnsureFloatingHealthBar()
    {
        FloatingHealthBar healthBar = GetComponentInChildren<FloatingHealthBar>();
        if (healthBar == null)
        {
            GameObject hbGO = new GameObject("FloatingHealthBar");
            hbGO.transform.SetParent(transform, false);
            hbGO.transform.localPosition = Vector3.zero;
            hbGO.AddComponent<FloatingHealthBar>();
        }
    }

    private void HandleTakeDamage(float damage)
    {
        // Aggro on hit even if outside normal detection range
        if (currentState != State.Dead && playerTarget != null)
        {
            currentState = State.Chase;
        }
    }

    private void HandleDeath()
    {
        currentState = State.Dead;
        isAttacking = false;

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.simulated = false;
        }

        // Disable physical collider so player can walk freely over corpse
        Collider2D[] cols = GetComponents<Collider2D>();
        foreach (var c in cols) c.enabled = false;

        if (animator != null)
        {
            animator.SetBool(isDeadParam, true);
            animator.SetBool(isMovingParam, false);
            animator.SetBool(isAttackingParam, false);
        }

        Destroy(gameObject, 2.5f);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        Gizmos.color = Color.gray;
        Gizmos.DrawWireSphere(Application.isPlaying ? (Vector3)spawnPoint : transform.position, wanderRadius);
    }

    #endregion
}
