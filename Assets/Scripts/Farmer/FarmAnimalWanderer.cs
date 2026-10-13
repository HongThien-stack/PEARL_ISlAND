using UnityEngine;

/// <summary>
/// Shared pen movement/eating behaviour for pigs and adult chickens.
/// Coordinates are expressed in the direct parent's local space.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer), typeof(Animator))]
public sealed class FarmAnimalWanderer : MonoBehaviour
{
    [SerializeField] private Vector2 minimum = new Vector2(-17f, -16.6f);
    [SerializeField] private Vector2 maximum = new Vector2(-1.2f, -3.2f);
    [SerializeField, Min(0.05f)] private float fencePadding = 0.45f;
    [SerializeField, Min(0.1f)] private float moveSpeed = 1.15f;
    [SerializeField, Min(0.05f)] private float arriveDistance = 0.12f;
    [SerializeField] private Vector2 pauseDuration = new Vector2(0.7f, 2.2f);
    [SerializeField, Min(0f)] private float separationRadius = 1.1f;
    [SerializeField, Range(0f, 2f)] private float separationStrength = 0.75f;
    [SerializeField] private Vector2 eatInterval = new Vector2(5f, 11f);
    [SerializeField] private Vector2 eatDuration = new Vector2(2f, 4f);
    [SerializeField] private string walkLeftStateName = "Walk_Left";
    [SerializeField] private string walkRightStateName = "Walk_Right";
    [SerializeField] private string eatStateName = "Eat";

    private SpriteRenderer spriteRenderer;
    private Animator animator;
    private Vector2 target;
    private float pauseTimer;
    private float eatIntervalTimer;
    private float eatDurationTimer;
    private bool isMoving;
    private bool isEating;
    private bool isFacingLeft;

    public bool IsEating => isEating;
    public bool IsMoving => isMoving;
    public bool IsFacingLeft => isFacingLeft;

    public void Configure(
        Vector2 penMinimum,
        Vector2 penMaximum,
        float speed,
        float spacing,
        string walkLeft,
        string walkRight,
        string eat)
    {
        minimum = penMinimum;
        maximum = penMaximum;
        moveSpeed = speed;
        separationRadius = spacing;
        walkLeftStateName = walkLeft;
        walkRightStateName = walkRight;
        eatStateName = eat;
        ClampInsidePen();
    }

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
        eatIntervalTimer = Random.Range(eatInterval.x, eatInterval.y)
            + transform.GetSiblingIndex() * 0.85f;

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
            if (eatDurationTimer <= 0f) FinishEating();
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

        Vector2 direction = (toTarget.normalized + CalculateSeparation()).normalized;
        localPosition += direction * moveSpeed * Time.deltaTime;
        localPosition.x = Mathf.Clamp(localPosition.x, minimum.x + fencePadding, maximum.x - fencePadding);
        localPosition.y = Mathf.Clamp(localPosition.y, minimum.y + fencePadding, maximum.y - fencePadding);
        transform.localPosition = new Vector3(localPosition.x, localPosition.y, transform.localPosition.z);

        if (Mathf.Abs(direction.x) > 0.05f) SetFacingLeft(direction.x < 0f);
        SetMoving(true);
    }

    private Vector2 CalculateSeparation()
    {
        if (separationRadius <= 0f || separationStrength <= 0f) return Vector2.zero;

        Vector2 force = Vector2.zero;
        FarmAnimalWanderer[] herd = transform.parent.GetComponentsInChildren<FarmAnimalWanderer>();
        foreach (FarmAnimalWanderer other in herd)
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
        if (isEating || isMoving == moving) return;
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
        float normalizedTime = animator.runtimeAnimatorController == null
            ? 0f
            : animator.GetCurrentAnimatorStateInfo(0).normalizedTime % 1f;
        PlayWalkAnimation(faceLeft, normalizedTime);
    }

    private void PlayWalkAnimation(bool faceLeft, float normalizedTime)
    {
        isFacingLeft = faceLeft;

        // Like the cow sprites, these sheets are authored facing left.
        // Keep the source orientation when moving left and mirror only when
        // moving right, so the animal never appears to walk backwards.
        spriteRenderer.flipX = !faceLeft;
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
}
