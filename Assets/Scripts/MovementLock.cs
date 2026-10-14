using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Centralized movement lock manager using reference counting.
/// Multiple scripts can lock/unlock movement safely without
/// stepping on each other's toes.
///
/// Usage:
///   MovementLock.Lock(this);     // Lock movement
///   MovementLock.Unlock(this);   // Unlock movement
///   MovementLock.ForceUnlockAll(); // Emergency reset (Escape key)
/// </summary>
public class MovementLock : MonoBehaviour
{
    public static MovementLock Instance { get; private set; }

    private PlayerMovement playerMovement;
    private readonly HashSet<object> lockSources = new HashSet<object>();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) { Destroy(this); return; }

        playerMovement = GetComponent<PlayerMovement>();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        // Emergency: Press Escape to force unlock all movement
        bool escapePressed = false;
        try
        {
#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Keyboard.current != null)
                escapePressed = UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame;
#endif
            if (!escapePressed) escapePressed = Input.GetKeyDown(KeyCode.Escape);
        }
        catch { }

        if (escapePressed && lockSources.Count > 0)
        {
            Debug.LogWarning("[MovementLock] 🔓 ESCAPE pressed — Force unlocking all movement locks!");
            ForceUnlockAll();
        }

        // Sanity check: if lockCount is 0 but movement is disabled, fix it
        if (lockSources.Count == 0 && playerMovement != null && !playerMovement.enabled)
        {
            Debug.LogWarning("[MovementLock] ⚠️ Movement was disabled with no active locks, auto-restoring!");
            playerMovement.enabled = true;
        }
    }

    /// <summary>Lock movement. Call Unlock(source) when done.</summary>
    public static void Lock(object source)
    {
        EnsureInstance(source);
        if (Instance == null || source == null || !Instance.lockSources.Add(source)) return;

        if (Instance.playerMovement != null)
            Instance.playerMovement.enabled = false;

        Debug.Log($"[MovementLock] 🔒 LOCK by {source.GetType().Name} | lockCount={Instance.lockSources.Count}");
    }

    /// <summary>Unlock movement (decrements counter; movement re-enabled when count reaches 0).</summary>
    public static void Unlock(object source)
    {
        if (Instance == null || source == null || !Instance.lockSources.Remove(source)) return;

        if (Instance.lockSources.Count == 0)
        {
            if (Instance.playerMovement != null)
                Instance.playerMovement.enabled = true;

            Debug.Log($"[MovementLock] 🔓 UNLOCK by {source.GetType().Name} | lockCount=0 → Movement RESTORED");
        }
        else
        {
            Debug.Log($"[MovementLock] 🔓 UNLOCK by {source.GetType().Name} | lockCount={Instance.lockSources.Count} (still locked)");
        }
    }

    /// <summary>Force unlock all — emergency reset (Escape key).</summary>
    public static void ForceUnlockAll()
    {
        if (Instance == null) return;
        Instance.lockSources.Clear();
        if (Instance.playerMovement != null)
            Instance.playerMovement.enabled = true;
        Debug.Log("[MovementLock] 🔓 FORCE UNLOCK ALL — lockCount=0 → Movement RESTORED");
    }

    /// <summary>Is movement currently locked?</summary>
    public static bool IsLocked => Instance != null && Instance.lockSources.Count > 0;

    private static void EnsureInstance(object source)
    {
        if (Instance != null || !(source is Component component)) return;

        MovementLock movementLock = component.GetComponent<MovementLock>();
        if (movementLock == null)
        {
            movementLock = component.gameObject.AddComponent<MovementLock>();
        }

        Instance = movementLock;
    }
}
