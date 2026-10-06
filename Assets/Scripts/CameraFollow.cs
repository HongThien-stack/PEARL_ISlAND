using UnityEngine;

/// <summary>
/// Smoothly follows a target Transform (e.g., Player) in 2D space.
/// </summary>
public class CameraFollow : MonoBehaviour
{
    public enum CameraMode
    {
        FollowPlayer,
        FixedPosition
    }

    [Header("Camera Mode")]
    [SerializeField] private CameraMode currentMode = CameraMode.FollowPlayer;
    [Tooltip("Target anchor Transform when using FixedPosition mode (e.g. Center of indoor restaurant).")]
    [SerializeField] private Transform fixedAnchor;

    [Header("Target Settings")]
    [Tooltip("The Transform the camera should follow (usually the Player).")]
    [SerializeField] private Transform target;

    [Header("Follow Settings")]
    [Tooltip("Camera offset relative to the target position (Z should be negative, e.g. -10).")]
    [SerializeField] private Vector3 offset = new Vector3(0f, 0f, -10f);

    [Tooltip("Smooth time for camera interpolation (lower is faster).")]
    [SerializeField] private float smoothTime = 0.2f;

    private Vector3 velocity = Vector3.zero;

    private void Start()
    {
        // Auto-detect player target if not assigned in Inspector
        if (target == null)
        {
            FindPlayerTarget();
        }

        if (currentMode == CameraMode.FixedPosition && fixedAnchor != null)
        {
            transform.position = fixedAnchor.position + offset;
            velocity = Vector3.zero;
        }
        else if (currentMode == CameraMode.FollowPlayer && target != null)
        {
            transform.position = target.position + offset;
            velocity = Vector3.zero;
        }
    }

    private void LateUpdate()
    {
        if (currentMode == CameraMode.FixedPosition)
        {
            if (fixedAnchor != null)
            {
                Vector3 destPos = fixedAnchor.position + offset;
                transform.position = Vector3.SmoothDamp(transform.position, destPos, ref velocity, smoothTime);
            }
            return;
        }

        if (target == null)
        {
            FindPlayerTarget();
            if (target == null) return;
        }

        Vector3 targetPosition = target.position + offset;
        transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref velocity, smoothTime);
    }

    private void FindPlayerTarget()
    {
        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
        {
            target = playerObj.transform;
        }
        else
        {
            PlayerMovement playerComp = FindFirstObjectByType<PlayerMovement>();
            if (playerComp != null)
            {
                target = playerComp.transform;
            }
        }
    }

    /// <summary>
    /// Switch camera to follow player smoothly.
    /// </summary>
    public void SetFollowPlayer(Transform playerTransform = null)
    {
        currentMode = CameraMode.FollowPlayer;
        if (playerTransform != null)
        {
            target = playerTransform;
        }
        else if (target == null)
        {
            FindPlayerTarget();
        }
    }

    /// <summary>
    /// Switch camera to a fixed anchor position (e.g. Indoor Restaurant view).
    /// </summary>
    public void SetFixedPosition(Transform anchorTransform, bool snapImmediately = true)
    {
        currentMode = CameraMode.FixedPosition;
        if (anchorTransform != null)
        {
            fixedAnchor = anchorTransform;
        }

        if (fixedAnchor == null)
        {
            GameObject autoAnchor = GameObject.Find("CameraAnchor");
            if (autoAnchor == null) autoAnchor = GameObject.Find("RestaurantAnchor");
            if (autoAnchor == null) autoAnchor = GameObject.Find("IndoorAnchor");
            if (autoAnchor != null) fixedAnchor = autoAnchor.transform;
        }

        if (fixedAnchor != null && snapImmediately)
        {
            transform.position = fixedAnchor.position + offset;
            velocity = Vector3.zero;
        }
    }

    /// <summary>
    /// Call this to manually set a new target for the camera.
    /// </summary>
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }

    /// <summary>
    /// Instantly snaps the camera to the target/anchor position without smoothing.
    /// </summary>
    public void SnapToTarget()
    {
        Transform activeTarget = (currentMode == CameraMode.FixedPosition && fixedAnchor != null) ? fixedAnchor : target;
        if (activeTarget != null)
        {
            transform.position = activeTarget.position + offset;
            velocity = Vector3.zero;
        }
    }
}
