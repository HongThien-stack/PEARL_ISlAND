using UnityEngine;

/// <summary>Keeps a chick near its assigned hen while retaining walk/eat cycles.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer), typeof(Animator))]
public sealed class ChickFollower : MonoBehaviour
{
    [SerializeField] private Transform mother;
    [SerializeField] private Vector2 followOffset;
    [SerializeField] private Vector2 minimum = new Vector2(-17f, -16.6f);
    [SerializeField] private Vector2 maximum = new Vector2(-1.2f, -3.2f);
    [SerializeField, Min(0.1f)] private float moveSpeed = 1.55f;
    [SerializeField, Min(0.05f)] private float followDistance = 0.28f;
    [SerializeField] private Vector2 eatInterval = new Vector2(7f, 14f);
    [SerializeField] private Vector2 eatDuration = new Vector2(0.8f, 1.8f);
    [SerializeField] private string walkLeftStateName = "Chick_Walk_Left";
    [SerializeField] private string walkRightStateName = "Chick_Walk_Right";
    [SerializeField] private string eatStateName = "Chick_Eat";

    private SpriteRenderer spriteRenderer;
    private Animator animator;
    private float eatTimer;
    private float eatDurationTimer;
    private bool eating;
    private bool facingLeft;

    public void Configure(Transform assignedMother, Vector2 assignedOffset, Vector2 penMinimum, Vector2 penMaximum)
    {
        mother = assignedMother;
        followOffset = assignedOffset;
        minimum = penMinimum;
        maximum = penMaximum;
    }

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
        spriteRenderer.flipX = false;
        spriteRenderer.spriteSortPoint = SpriteSortPoint.Pivot;
    }

    private void Start()
    {
        eatTimer = Random.Range(eatInterval.x, eatInterval.y) + transform.GetSiblingIndex() * 0.35f;
        PlayWalk(false, Random.value);
    }

    private void Update()
    {
        if (mother == null) return;

        if (eating)
        {
            eatDurationTimer -= Time.deltaTime;
            if (eatDurationTimer <= 0f)
            {
                eating = false;
                eatTimer = Random.Range(eatInterval.x, eatInterval.y);
                PlayWalk(facingLeft, Random.value);
            }
            return;
        }

        eatTimer -= Time.deltaTime;
        Vector2 motherLocal = transform.parent.InverseTransformPoint(mother.position);
        Vector2 desired = motherLocal + followOffset;
        Vector2 current = transform.localPosition;
        Vector2 delta = desired - current;

        if (eatTimer <= 0f && delta.sqrMagnitude < 1.5f * 1.5f)
        {
            eating = true;
            animator.speed = 1f;
            animator.Play(eatStateName, 0, Random.value);
            eatDurationTimer = Random.Range(eatDuration.x, eatDuration.y);
            return;
        }

        if (delta.sqrMagnitude <= followDistance * followDistance)
        {
            animator.speed = 0f;
            return;
        }

        Vector2 direction = delta.normalized;
        float catchUpMultiplier = delta.sqrMagnitude > 2.25f ? 1.65f : 1f;
        current += direction * moveSpeed * catchUpMultiplier * Time.deltaTime;
        current.x = Mathf.Clamp(current.x, minimum.x + 0.25f, maximum.x - 0.25f);
        current.y = Mathf.Clamp(current.y, minimum.y + 0.25f, maximum.y - 0.25f);
        transform.localPosition = new Vector3(current.x, current.y, transform.localPosition.z);

        if (Mathf.Abs(direction.x) > 0.04f && facingLeft != (direction.x < 0f))
        {
            PlayWalk(direction.x < 0f, animator.GetCurrentAnimatorStateInfo(0).normalizedTime % 1f);
        }
        animator.speed = 1f;
    }

    private void PlayWalk(bool left, float normalizedTime)
    {
        facingLeft = left;
        // Chick sprites use the same left-facing source convention as cows.
        spriteRenderer.flipX = !left;
        animator.Play(left ? walkLeftStateName : walkRightStateName, 0, normalizedTime);
    }
}
