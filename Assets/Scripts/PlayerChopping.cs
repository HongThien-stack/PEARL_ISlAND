using System.Collections;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Handles MC's tree chopping action.
/// Triggers MC chopping animations and hits nearby ChoppableTree objects.
/// </summary>
public class PlayerChopping : MonoBehaviour
{
    [Header("Chopping Settings")]
    [Tooltip("Radius in front of player to detect trees.")]
    [SerializeField] private float chopRadius = 1.8f;

    [Tooltip("Layer mask for choppable trees.")]
    [SerializeField] private LayerMask treeLayer = ~0;

    [Header("Chopping Key")]
    [Tooltip("Key to trigger chopping action.")]
    [SerializeField] private KeyCode chopKey = KeyCode.E;

    [Header("Animator Integration")]
    [SerializeField] private Animator animator;
    [SerializeField] private string chopTriggerParam = "Chop";
    [SerializeField] private string isChoppingParam = "IsChopping";

    [Header("Direct Play Fallback States")]
    [SerializeField] private string chopStateDown = "mc-chop-tree-down";
    [SerializeField] private string chopStateUp = "mc-chop-tree-up";
    [SerializeField] private string chopStateLeft = "mc-chop-tree-left";
    [SerializeField] private string chopStateRight = "mc-chop-tree-right";

    private PlayerMovement playerMovement;
    private bool isChopping = false;

    private void Awake()
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }
        playerMovement = GetComponent<PlayerMovement>();
    }

    private void Update()
    {
        if (ReadChopInput() && !isChopping)
        {
            TryChopTree();
        }
    }

    /// <summary>
    /// Attempts to chop a tree in front of MC.
    /// </summary>
    public void TryChopTree()
    {
        if (isChopping) return;

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, chopRadius, treeLayer);
        ChoppableTree targetTree = null;
        float closestDist = float.MaxValue;

        foreach (Collider2D hit in hits)
        {
            ChoppableTree tree = hit.GetComponent<ChoppableTree>();
            if (tree != null && tree.CanInteract(gameObject))
            {
                float dist = Vector2.Distance(transform.position, hit.transform.position);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    targetTree = tree;
                }
            }
        }

        if (targetTree != null)
        {
            StartCoroutine(PerformChopRoutine(targetTree));
        }
        else
        {
            // Play chop animation even if no tree is nearby
            StartCoroutine(PerformChopRoutine(null));
        }
    }

    private IEnumerator PerformChopRoutine(ChoppableTree targetTree)
    {
        isChopping = true;

        if (playerMovement != null) playerMovement.enabled = false;

        // Trigger Animator parameters
        if (animator != null)
        {
            if (HasParameter(animator, chopTriggerParam))
            {
                animator.SetTrigger(chopTriggerParam);
            }
            if (HasParameter(animator, isChoppingParam))
            {
                animator.SetBool(isChoppingParam, true);
            }

            // Direct state play fallback with instant evaluation
            string chopState = GetChopStateName();
            int hash = Animator.StringToHash(chopState);
            if (animator.HasState(0, hash))
            {
                animator.Play(hash, 0, 0f);
            }
        }

        // Hit tree
        if (targetTree != null)
        {
            targetTree.HitTree(gameObject);
        }

        yield return new WaitForSeconds(0.95f);

        if (animator != null && HasParameter(animator, isChoppingParam))
        {
            animator.SetBool(isChoppingParam, false);
        }

        if (playerMovement != null) playerMovement.enabled = true;

        isChopping = false;
    }

    private string GetChopStateName()
    {
        if (animator == null) return chopStateDown;
        float lastX = HasParameter(animator, "LastMoveX") ? animator.GetFloat("LastMoveX") : 0f;
        float lastY = HasParameter(animator, "LastMoveY") ? animator.GetFloat("LastMoveY") : -1f;

        if (lastY > 0.1f) return chopStateUp;
        if (lastY < -0.1f) return chopStateDown;
        if (lastX < -0.1f) return chopStateLeft;
        if (lastX > 0.1f) return chopStateRight;
        return chopStateDown;
    }

    private bool ReadChopInput()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            if (Keyboard.current.eKey.wasPressedThisFrame || Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                return true;
            }
        }
#endif
        try
        {
            return Input.GetKeyDown(chopKey) || Input.GetKeyDown(KeyCode.Space);
        }
        catch { return false; }
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

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, chopRadius);
    }
}
