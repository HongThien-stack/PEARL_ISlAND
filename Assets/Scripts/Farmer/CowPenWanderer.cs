using UnityEngine;

/// <summary>
/// Lets a cow wander naturally inside a rectangular pen defined in the
/// coordinate space of its parent. The position is clamped every frame so a
/// cow can never leave the enclosure, even if its target is close to a fence.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer), typeof(Animator))]
public sealed class CowPenWanderer : MonoBehaviour
{
    [Header("Pen Bounds (parent-local)")]
    [SerializeField] private Vector2 minimum = new Vector2(-17f, -16.6f);
    [SerializeField] private Vector2 maximum = new Vector2(-1.2f, -3.2f);
    [SerializeField, Min(0.1f)] private float fencePadding = 0.45f;

    [Header("Wandering")]
    [SerializeField, Min(0.1f)] private float moveSpeed = 1.15f;
    [SerializeField, Min(0.05f)] private float arriveDistance = 0.12f;
    [SerializeField] private Vector2 pauseDuration = new Vector2(0.7f, 2.2f);
    [SerializeField, Min(0f)] private float separationRadius = 1.1f;
    [SerializeField, Range(0f, 2f)] private float separationStrength = 0.75f;

    [Header("Eating")]
    [Tooltip("Random time spent walking before this cow eats again.")]
    [SerializeField] private Vector2 eatInterval = new Vector2(5f, 11f);
    [Tooltip("Random time the eating animation is played.")]
    [SerializeField] private Vector2 eatDuration = new Vector2(2f, 4f);
    [SerializeField] private string walkLeftStateName = "Cow_Walk_Left";
    [SerializeField] private string walkRightStateName = "Cow_Walk_Right";
    [SerializeField] private string eatStateName = "Cow_Eat";

    private SpriteRenderer spriteRenderer;
    private Animator animator;
    private Vector2 target;
    private float pauseTimer;
    private float eatIntervalTimer;
    private float eatDurationTimer;
    private bool isMoving;
    private bool isEating;
    private bool isFacingLeft;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
        spriteRenderer.flipX = false;
        spriteRenderer.spriteSortPoint = SpriteSortPoint.Pivot;
        ClampInsidePen();
    }

    private void Start()
    {
        ChooseNewTarget();

        // The sibling offset guarantees that the four cows do not begin
        // eating together, while later intervals remain independently random.
        eatIntervalTimer = Random.Range(eatInterval.x, eatInterval.y)
            + transform.GetSiblingIndex() * 1.15f;

        // Keep the herd from playing the exact same frame at the same time.
        if (animator.runtimeAnimatorController != null)
        {
            PlayWalkAnimation(false, Random.value);
        }
    }

    private void Update()
    {
        if (isEating)
        {
            eatDurationTimer -= Time.deltaTime;
            if (eatDurationTimer <= 0f)
            {
                FinishEating();
            }
            return;
        }

        eatIntervalTimer -= Time.deltaTime;
        if (eatIntervalTimer <= 0f)
        {
            StartEating();
            return;
        }

        if (pauseTimer > 0f)
        {
            pauseTimer -= Time.deltaTime;
            SetMoving(false);
            return;
        }

        Vector2 localPosition = transform.localPosition;
        Vector2 toTarget = target - localPosition;
        if (toTarget.sqrMagnitude <= arriveDistance * arriveDistance)
        {
            pauseTimer = Random.Range(pauseDuration.x, pauseDuration.y);
            ChooseNewTarget();
            SetMoving(false);
            return;
        }

        Vector2 direction = toTarget.normalized + CalculateSeparation();
        direction.Normalize();

        localPosition += direction * moveSpeed * Time.deltaTime;
        localPosition.x = Mathf.Clamp(localPosition.x, minimum.x + fencePadding, maximum.x - fencePadding);
        localPosition.y = Mathf.Clamp(localPosition.y, minimum.y + fencePadding, maximum.y - fencePadding);
        transform.localPosition = new Vector3(localPosition.x, localPosition.y, transform.localPosition.z);

        // Keep the current facing direction for almost-vertical movement so
        // the cow does not rapidly switch animations because of tiny X values.
        if (Mathf.Abs(direction.x) > 0.05f)
        {
            SetFacingLeft(direction.x < 0f);
        }

        SetMoving(true);
    }

    private Vector2 CalculateSeparation()
    {
        if (separationRadius <= 0f || separationStrength <= 0f)
        {
            return Vector2.zero;
        }

        Vector2 force = Vector2.zero;
        CowPenWanderer[] herd = transform.parent.GetComponentsInChildren<CowPenWanderer>();
        foreach (CowPenWanderer other in herd)
        {
            if (other == this) continue;

            Vector2 offset = (Vector2)transform.localPosition - (Vector2)other.transform.localPosition;
            float distance = offset.magnitude;
            if (distance > 0.001f && distance < separationRadius)
            {
                force += offset.normalized * (1f - distance / separationRadius);
            }
        }

        return force * separationStrength;
    }

    private void SetMoving(bool moving)
    {
        if (isEating) return;
        if (isMoving == moving) return;
        isMoving = moving;
        animator.speed = moving ? 1f : 0f;
    }

    private void StartEating()
    {
        isEating = true;
        isMoving = false;
        animator.speed = 1f;
        animator.Play(eatStateName, 0, Random.value);
        eatDurationTimer = Random.Range(eatDuration.x, eatDuration.y);
    }

    private void FinishEating()
    {
        isEating = false;
        pauseTimer = 0f;
        eatIntervalTimer = Random.Range(eatInterval.x, eatInterval.y);
        animator.speed = 1f;
        PlayWalkAnimation(isFacingLeft, Random.value);
        ChooseNewTarget();
    }

    private void SetFacingLeft(bool faceLeft)
    {
        if (isFacingLeft == faceLeft) return;

        float normalizedTime = 0f;
        if (animator.runtimeAnimatorController != null)
        {
            normalizedTime = animator.GetCurrentAnimatorStateInfo(0).normalizedTime % 1f;
        }

        PlayWalkAnimation(faceLeft, normalizedTime);
    }

    private void PlayWalkAnimation(bool faceLeft, float normalizedTime)
    {
        isFacingLeft = faceLeft;

        // Facing is authored in the clips: left keeps the source rotation,
        // while right rotates 180 degrees around Y. Do not flip the renderer
        // here, otherwise the animation rotation would be reversed again.
        animator.Play(faceLeft ? walkLeftStateName : walkRightStateName, 0, normalizedTime);
    }

    private void ChooseNewTarget()
    {
        target = new Vector2(
            Random.Range(minimum.x + fencePadding, maximum.x - fencePadding),
            Random.Range(minimum.y + fencePadding, maximum.y - fencePadding));
    }

    private void ClampInsidePen()
    {
        Vector3 localPosition = transform.localPosition;
        localPosition.x = Mathf.Clamp(localPosition.x, minimum.x + fencePadding, maximum.x - fencePadding);
        localPosition.y = Mathf.Clamp(localPosition.y, minimum.y + fencePadding, maximum.y - fencePadding);
        transform.localPosition = localPosition;
    }

    private void OnValidate()
    {
        if (maximum.x < minimum.x) maximum.x = minimum.x;
        if (maximum.y < minimum.y) maximum.y = minimum.y;
        pauseDuration.x = Mathf.Max(0f, pauseDuration.x);
        pauseDuration.y = Mathf.Max(pauseDuration.x, pauseDuration.y);
        eatInterval.x = Mathf.Max(0.5f, eatInterval.x);
        eatInterval.y = Mathf.Max(eatInterval.x, eatInterval.y);
        eatDuration.x = Mathf.Max(0.5f, eatDuration.x);
        eatDuration.y = Mathf.Max(eatDuration.x, eatDuration.y);
    }

    private void OnDrawGizmosSelected()
    {
        if (transform.parent == null) return;

        Vector2 size = maximum - minimum;
        Vector2 center = (minimum + maximum) * 0.5f;
        Gizmos.color = new Color(0.2f, 0.9f, 0.35f, 0.9f);
        Gizmos.matrix = transform.parent.localToWorldMatrix;
        Gizmos.DrawWireCube(center, size);
    }
}
