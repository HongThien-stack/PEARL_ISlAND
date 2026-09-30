using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Detects nearby interactable objects and triggers interaction when pressing the 'F' key.
/// Compatible with both New Input System and Legacy Input.
/// </summary>
public class PlayerInteraction : MonoBehaviour
{
    [Header("Interaction Settings")]
    [Tooltip("Radius around the player to search for interactable objects.")]
    [SerializeField] private float interactRadius = 1.5f;

    [Tooltip("Layer mask for interactable objects.")]
    [SerializeField] private LayerMask interactableLayer = ~0;

    [Header("UI Prompt (Optional)")]
    [Tooltip("Visual indicator or text prompt shown when near an interactable object.")]
    [SerializeField] private GameObject interactPromptUI;

    private IInteractable currentInteractable;

    private void Update()
    {
        DetectInteractable();

        if (ReadInteractInput() && currentInteractable != null)
        {
            currentInteractable.Interact(gameObject);
        }
    }

    private void DetectInteractable()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, interactRadius, interactableLayer);
        IInteractable closest = null;
        float closestDistance = float.MaxValue;

        foreach (Collider2D hit in hits)
        {
            IInteractable interactable = hit.GetComponent<IInteractable>();
            if (interactable != null && interactable.CanInteract(gameObject))
            {
                float dist = Vector2.Distance(transform.position, hit.transform.position);
                if (dist < closestDistance)
                {
                    closestDistance = dist;
                    closest = interactable;
                }
            }
        }

        currentInteractable = closest;

        if (interactPromptUI != null)
        {
            interactPromptUI.SetActive(currentInteractable != null);
        }
    }

    private bool ReadInteractInput()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
        {
            return true;
        }
#endif
        try
        {
            return Input.GetKeyDown(KeyCode.F);
        }
        catch { return false; }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactRadius);
    }
}

/// <summary>
/// Interface implemented by any object MC can interact with using 'F'.
/// </summary>
public interface IInteractable
{
    bool CanInteract(GameObject player);
    void Interact(GameObject player);
}
