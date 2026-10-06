using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Teleports the player between locations or maps (e.g. Indoor Restaurant <-> Outdoor Island Map).
/// Plays a smooth screen blackout (Fade Out -> Teleport -> Fade In) transition.
/// Supports both 'F' key interaction (IInteractable) and automatic Walk-In (OnTriggerEnter2D).
/// </summary>
public class MapTeleporter : MonoBehaviour, IInteractable
{
    public enum TriggerMode
    {
        OnKeyPressF,
        OnWalkIn
    }

    [Header("Teleport Destination")]
    [Tooltip("Target Transform position to teleport player to.")]
    [SerializeField] private Transform targetTeleportPoint;

    [Tooltip("Optional offset added to destination position.")]
    [SerializeField] private Vector3 targetPositionOffset = Vector3.zero;

    [Tooltip("Optional: Name of target Unity scene to load. Leave empty if teleporting within the same scene.")]
    [SerializeField] private string targetSceneName = "";

    [Header("Trigger Mode")]
    [SerializeField] private TriggerMode triggerMode = TriggerMode.OnKeyPressF;

    [Header("Camera Behavior On Arrival")]
    [Tooltip("If true, automatically switches camera mode upon teleporting.")]
    [SerializeField] private bool changeCameraMode = true;

    [Tooltip("Camera mode upon arrival (FollowPlayer when going outside, FixedPosition when entering house).")]
    [SerializeField] private CameraFollow.CameraMode arrivalCameraMode = CameraFollow.CameraMode.FollowPlayer;

    [Tooltip("Target anchor Transform when using FixedPosition mode (e.g. Center of indoor restaurant view).")]
    [SerializeField] private Transform fixedCameraAnchor;

    [Header("Transition Timings")]
    [SerializeField] private float fadeOutDuration = 0.4f;
    [SerializeField] private float blackoutHoldTime = 0.2f;
    [SerializeField] private float fadeInDuration = 0.4f;

    [Tooltip("Cooldown delay in seconds required before the player can teleport again.")]
    [SerializeField] private float teleportCooldown = 1.0f;

    [Header("UI Prompt (Optional)")]
    [SerializeField] private GameObject interactPromptUI;

    [Header("Audio (Optional)")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip teleportSound;

    private static float lastGlobalTeleportTime = -999f;
    private bool isTeleporting = false;

    private void Awake()
    {
        if (GetComponent<Collider2D>() == null)
        {
            BoxCollider2D col = gameObject.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
        }

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }
    }

    public bool CanInteract(GameObject player)
    {
        if (isTeleporting) return false;
        return Time.time >= lastGlobalTeleportTime + teleportCooldown;
    }

    public void Interact(GameObject player)
    {
        if (triggerMode == TriggerMode.OnKeyPressF && CanInteract(player))
        {
            StartCoroutine(ExecuteTeleportRoutine(player));
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (triggerMode == TriggerMode.OnWalkIn && other.CompareTag("Player") && CanInteract(other.gameObject))
        {
            StartCoroutine(ExecuteTeleportRoutine(other.gameObject));
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player") && interactPromptUI != null)
        {
            interactPromptUI.SetActive(false);
        }
    }

    private IEnumerator ExecuteTeleportRoutine(GameObject player)
    {
        isTeleporting = true;

        if (interactPromptUI != null)
        {
            interactPromptUI.SetActive(false);
        }

        if (audioSource != null && teleportSound != null)
        {
            audioSource.PlayOneShot(teleportSound);
        }

        // 1. Temporarily disable Player Movement & Rigidbody velocity
        PlayerMovement movement = player.GetComponent<PlayerMovement>();
        Rigidbody2D rb = player.GetComponent<Rigidbody2D>();

        if (movement != null) movement.enabled = false;
        if (rb != null) rb.linearVelocity = Vector2.zero;

        // 2. Fade screen out (Blackout)
        yield return ScreenFader.Instance.StartCoroutine(ScreenFader.Instance.FadeOutRoutine(fadeOutDuration));

        // 3. Teleport Player position or Load Scene
        if (!string.IsNullOrEmpty(targetSceneName))
        {
            AsyncOperation loadOp = SceneManager.LoadSceneAsync(targetSceneName);
            while (!loadOp.isDone)
            {
                yield return null;
            }
        }
        else
        {
            Vector3 destPos = (targetTeleportPoint != null ? targetTeleportPoint.position : transform.position) + targetPositionOffset;

            if (rb != null)
            {
                rb.position = destPos;
            }
            player.transform.position = destPos;

            // Update Camera Mode (Follow Player vs Fixed Indoor Anchor)
            CameraFollow camFollow = FindAnyObjectByType<CameraFollow>();
            if (camFollow != null && changeCameraMode)
            {
                if (arrivalCameraMode == CameraFollow.CameraMode.FixedPosition)
                {
                    Transform anchor = fixedCameraAnchor != null ? fixedCameraAnchor : targetTeleportPoint;
                    camFollow.SetFixedPosition(anchor, snapImmediately: true);
                    Debug.Log($"[MapTeleporter] 📷 Switched Camera to FixedPosition mode at anchor: {(anchor != null ? anchor.name : "NULL")}");
                }
                else
                {
                    camFollow.SetFollowPlayer(player.transform);
                    camFollow.SnapToTarget();
                    Debug.Log($"[MapTeleporter] 📷 Switched Camera to FollowPlayer mode tracking: {player.name}");
                }
            }
        }

        // 4. Hold screen blackout briefly
        if (blackoutHoldTime > 0f)
        {
            yield return new WaitForSeconds(blackoutHoldTime);
        }

        // 5. Fade screen in
        yield return ScreenFader.Instance.StartCoroutine(ScreenFader.Instance.FadeInRoutine(fadeInDuration));

        // 6. Re-enable Player Movement and apply Cooldown
        if (movement != null) movement.enabled = true;

        lastGlobalTeleportTime = Time.time;
        isTeleporting = false;
    }

    private void OnDrawGizmos()
    {
        if (targetTeleportPoint != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(targetTeleportPoint.position + targetPositionOffset, 0.5f);
            Gizmos.DrawLine(transform.position, targetTeleportPoint.position + targetPositionOffset);
        }
    }
}
