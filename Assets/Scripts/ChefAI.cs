using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Controls Chef's AI behavior: moving via waypoints or physics,
/// safely playing animation states or parameters without 'Invalid Layer Index' errors or moonwalking,
/// chopping/cooking at Kitchen Table, placing food on Serve Counter, and returning Home.
/// </summary>
public class ChefAI : MonoBehaviour
{
    [Header("Target Positions")]
    [Tooltip("Transform of the Kitchen Table where Chef chops/cooks.")]
    [SerializeField] private Transform kitchenTablePoint;

    [Tooltip("Transform of the Serve Counter where Chef delivers cooked food.")]
    [SerializeField] private Transform serveCounterPoint;

    [Tooltip("Transform of the Chef's resting position in the kitchen.")]
    [SerializeField] private Transform homePoint;

    [Header("Navigation & Pathing")]
    [Tooltip("Order of waypoints from Home to Kitchen Table.")]
    [SerializeField] private Transform[] waypointsToKitchen;

    [Tooltip("Order of waypoints from Kitchen Table to Serve Counter.")]
    [SerializeField] private Transform[] waypointsToServeCounter;

    [Tooltip("Move along X then Y axis if no waypoints are assigned.")]
    [SerializeField] private bool useAxisAlignedMovement = true;

    [Header("Serve Counter Reference")]
    [SerializeField] private ServeCounter serveCounter;

    [Header("Cooking Settings")]
    [Tooltip("Chef movement speed.")]
    [SerializeField] private float moveSpeed = 3f;

    [Tooltip("Time spent chopping/cooking at the kitchen table (in seconds).")]
    [SerializeField] private float cookDuration = 3f;

    [Header("Direct Animation State Names (Matching your Animator Window)")]
    [SerializeField] private string idleDownState = "idle-down-chef";
    [SerializeField] private string idleUpState = "idle-up-chef";
    [SerializeField] private string idleLeftState = "idle-left-chef";
    [SerializeField] private string idleRightState = "idle-right-chef";
    [SerializeField] private string walkDownState = "walk-down-chef";
    [SerializeField] private string walkUpState = "walk-up-chef";
    [SerializeField] private string walkLeftState = "walk-left-chef";
    [SerializeField] private string walkRightState = "walk-right-chef";
    [SerializeField] private string choppingState = "setting-down-chef";
    [SerializeField] private string choppingStateFallback = "cutting-down-chef";

    [Header("Animator Parameter Names (Fallback for Blend Trees)")]
    [SerializeField] private string isMovingParam = "IsMoving";
    [SerializeField] private string isWalkingParam = "IsWalking";
    [SerializeField] private string moveXParam = "MoveX";
    [SerializeField] private string moveYParam = "MoveY";

    [Header("Y-Sorting Settings")]
    [Tooltip("Enable dynamic sorting order calculation based on Y-position.")]
    [SerializeField] private bool dynamicYSorting = true;
    [SerializeField] private int sortingOrderOffset = 30000;
    [SerializeField] private float ySortingMultiplier = 1.3f;

    [Header("State")]
    [SerializeField] private bool isBusy = false;

    private Animator animator;
    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private string currentStateName = "";
    private Vector2 primaryDirection = Vector2.down;
    private Vector2 lastDirection = Vector2.down;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        if (spriteRenderer != null)
        {
            spriteRenderer.spriteSortPoint = SpriteSortPoint.Pivot;
        }

        if (rb != null)
        {
            rb.gravityScale = 0f;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        }

        if (serveCounter == null)
        {
            serveCounter = FindAnyObjectByType<ServeCounter>();
        }
    }

    private void LateUpdate()
    {
        if (dynamicYSorting && spriteRenderer != null)
        {
            spriteRenderer.sortingOrder = sortingOrderOffset + Mathf.RoundToInt(-transform.position.y * ySortingMultiplier);
        }
    }

    public bool IsBusy()
    {
        return isBusy;
    }

    /// <summary>
    /// Trigger Chef to start cooking an ordered food item.
    /// </summary>
    public void StartCookingOrder(Sprite foodSprite)
    {
        if (isBusy) return;
        StartCoroutine(CookingRoutine(foodSprite));
    }

    private IEnumerator CookingRoutine(Sprite foodSprite)
    {
        isBusy = true;

        if (serveCounter == null)
        {
            serveCounter = FindAnyObjectByType<ServeCounter>();
        }

        // 1. Move to Kitchen Table
        if (waypointsToKitchen != null && waypointsToKitchen.Length > 0)
        {
            foreach (Transform wp in waypointsToKitchen)
            {
                if (wp != null) yield return StartCoroutine(NavigateToTarget(wp.position));
            }
        }
        if (kitchenTablePoint != null)
        {
            yield return StartCoroutine(NavigateToTarget(kitchenTablePoint.position));
        }

        // 2. Play Chopping Animation
        PlayAnimationState(choppingState);
        Debug.Log("[ChefAI] 👨‍🍳 Chef đang thái/nấu ăn tại Kitchen Table...");
        yield return new WaitForSeconds(cookDuration);

        // 3. Move to Serve Counter
        if (waypointsToServeCounter != null && waypointsToServeCounter.Length > 0)
        {
            foreach (Transform wp in waypointsToServeCounter)
            {
                if (wp != null) yield return StartCoroutine(NavigateToTarget(wp.position));
            }
        }
        if (serveCounterPoint != null)
        {
            yield return StartCoroutine(NavigateToTarget(serveCounterPoint.position));
        }

        // 4. Place Food on Serve Counter
        if (serveCounter != null)
        {
            serveCounter.PlaceFood(foodSprite);
            Debug.Log("[ChefAI] 🍲 Chef đã đặt đồ ăn lên Serve Counter!");
        }

        // 5. Return Home
        List<Transform> returnPath = new List<Transform>();
        if (waypointsToServeCounter != null) returnPath.AddRange(waypointsToServeCounter);
        if (waypointsToKitchen != null) returnPath.AddRange(waypointsToKitchen);
        returnPath.Reverse();

        foreach (Transform wp in returnPath)
        {
            if (wp != null) yield return StartCoroutine(NavigateToTarget(wp.position));
        }

        if (homePoint != null)
        {
            yield return StartCoroutine(NavigateToTarget(homePoint.position));
        }

        PlayAnimationState(idleDownState);
        isBusy = false;
    }

    private IEnumerator NavigateToTarget(Vector3 targetPos)
    {
        if (useAxisAlignedMovement && (waypointsToKitchen == null || waypointsToKitchen.Length == 0))
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

        // Apply parameters if present
        if (animator != null)
        {
            if (HasParameter(animator, isMovingParam)) animator.SetBool(isMovingParam, isMoving);
            if (HasParameter(animator, isWalkingParam)) animator.SetBool(isWalkingParam, isMoving);
            if (HasParameter(animator, moveXParam)) animator.SetFloat(moveXParam, isMoving ? moveDir.x : 0f);
            if (HasParameter(animator, moveYParam)) animator.SetFloat(moveYParam, isMoving ? moveDir.y : 0f);
        }

        // Never flip sprite horizontally if distinct left/right animations exist!
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
            if (currentStateName == choppingState || currentStateName == choppingStateFallback) return;

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

        if (currentStateName != stateName)
        {
            if (animator.runtimeAnimatorController != null && animator.layerCount > 0)
            {
                int stateHash = Animator.StringToHash(stateName);
                if (animator.HasState(0, stateHash))
                {
                    animator.Play(stateHash, 0, 0f);
                    currentStateName = stateName;
                }
                else if (stateName == choppingState && !string.IsNullOrEmpty(choppingStateFallback))
                {
                    int fallbackHash = Animator.StringToHash(choppingStateFallback);
                    if (animator.HasState(0, fallbackHash))
                    {
                        animator.Play(fallbackHash, 0, 0f);
                        currentStateName = choppingStateFallback;
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
}
