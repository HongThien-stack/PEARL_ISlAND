using UnityEngine;

/// <summary>
/// Smoothly follows a target Transform (e.g., Player) in 2D space.
/// </summary>
public class CameraFollow : MonoBehaviour
{
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
            GameObject playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null)
            {
                target = playerObj.transform;
            }
            else
            {
                PlayerMovement playerComp = FindAnyObjectByType<PlayerMovement>();
                if (playerComp != null)
                {
                    target = playerComp.transform;
                }
            }
        }
    }

    private void LateUpdate()
    {
        if (target == null) return;

        Vector3 targetPosition = target.position + offset;
        transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref velocity, smoothTime);
    }

    /// <summary>
    /// Call this to manually set a new target for the camera.
    /// </summary>
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }
}
