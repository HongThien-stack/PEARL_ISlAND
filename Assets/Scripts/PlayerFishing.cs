using System.Collections;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Handles MC's Fishing mechanics (Key 2 to toggle Fishing Mode, Left Click to Cast).
/// Features:
/// - Key 2 toggles Fishing Stance (idle holding rod, can walk freely)
/// - Left Click casts fishing rod with directional animation (mc-fish-cast-*)
/// - Spawns pixel fishing line and bobber at rod tip
/// - Water waiting timer for fish bite
/// - Launches horizontal FishMinigameUI (Stardew Valley capture bar)
/// - Smooth catch resolution with fish reward
/// </summary>
public class PlayerFishing : MonoBehaviour
{
    public static PlayerFishing Instance { get; private set; }

    [Header("Input Keys")]
    [Tooltip("Key to toggle Fishing Stance (Key 2).")]
    [SerializeField] private KeyCode toggleFishingKey = KeyCode.Alpha2;

    [Header("Animator Integration")]
    [SerializeField] private Animator animator;
    [SerializeField] private string isFishingParam = "IsFishing";
    [SerializeField] private string isCastingParam = "IsCasting";

    [Header("Cast & Bite Settings")]
    [Tooltip("Swing delay before line and bobber are tossed into water.")]
    [SerializeField] private float castSwingDelay = 0.82f;

    [Tooltip("Distance bobber lands in front of the player.")]
    [SerializeField] private float castDistance = 2.6f;

    [Header("Rod Tip Anchor Positions")]
    [Tooltip("Local RodTipAnchor position for mc-fish-cast-down.")]
    [SerializeField] private Vector3 rodTipDown = new Vector3(0.35f, -0.45f, 0f);

    [Tooltip("Local RodTipAnchor position for mc-fish-cast-up.")]
    [SerializeField] private Vector3 rodTipUp = new Vector3(-0.25f, 0.7f, 0f);

    [Tooltip("Local RodTipAnchor position for mc-fish-cast-left.")]
    [SerializeField] private Vector3 rodTipLeft = new Vector3(-0.85f, 0.05f, 0f);

    [Tooltip("Local RodTipAnchor position for mc-fish-cast-right.")]
    [SerializeField] private Vector3 rodTipRight = new Vector3(0.85f, 0.05f, 0f);

    [Tooltip("GameObject used as the fishing rod tip anchor. Assign a child object from the hierarchy.")]
    [SerializeField] private GameObject rodTipAnchor;

    [Tooltip("Sprite shown on the bobber at the end of the fishing line.")]
    [SerializeField] private Sprite bobberSprite;

    [Tooltip("Minimum wait time in seconds before fish bites.")]
    [SerializeField] private float minBiteDelay = 2.2f;

    [Tooltip("Maximum wait time in seconds before fish bites.")]
    [SerializeField] private float maxBiteDelay = 4.2f;

    [Header("Inventory & Stats")]
    [SerializeField] private int fishCount = 0;
    private FishCatalog fishCatalog;
    private FishCatalog.Fish pendingFish;

    private PlayerCombat playerCombat;
    [SerializeField] private FishMinigameUI fishMinigameUI;
    private FishingBobber currentBobber;
    private Coroutine fishingRoutine;

    private bool isFishing = false;
    private bool isCastingOrFishing = false;

    public bool IsFishing => isFishing;
    public bool IsCastingOrFishing => isCastingOrFishing;
    public int FishCount => fishCount;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this)
        {
            Destroy(this);
            return;
        }

        if (animator == null) animator = GetComponent<Animator>();
        playerCombat = GetComponent<PlayerCombat>();
        fishCatalog = Resources.Load<FishCatalog>("FishCatalog");
        if (fishMinigameUI == null)
        {
            fishMinigameUI = FishMinigameUI.Instance;
        }
        if (fishMinigameUI == null)
        {
            fishMinigameUI = FindFirstObjectByType<FishMinigameUI>(FindObjectsInactive.Include);
        }
        if (fishMinigameUI == null)
        {
            Debug.LogWarning("[PlayerFishing] Không tìm thấy FishMinigameUI trong scene.");
        }

        // Ensure Rod Tip Anchor Transform exists
        if (rodTipAnchor == null)
        {
            Transform existingTip = transform.Find("RodTipAnchor");
            if (existingTip != null)
            {
                rodTipAnchor = existingTip.gameObject;
            }
            else
            {
                rodTipAnchor = new GameObject("RodTipAnchor");
                rodTipAnchor.transform.SetParent(transform, false);
                rodTipAnchor.transform.localPosition = rodTipDown;
            }
        }

    }

    private void Update()
    {
        // 1. Toggle Fishing Stance with Key 2
        if (ReadToggleFishingInput())
        {
            ToggleFishingMode();
        }

        // 2. Cast Fishing Rod on Left Mouse Click (only when in Fishing Stance and not already busy)
        // Also check minigame is NOT active to prevent cast input during minigame
        bool minigameActive = fishMinigameUI != null && fishMinigameUI.IsPlaying;
        if (isFishing && !isCastingOrFishing && !minigameActive && ReadCastInput())
        {
            StartCast();
        }
    }

    /// <summary>
    /// Toggles between Normal mode and Fishing mode (idle holding rod).
    /// </summary>
    public void ToggleFishingMode()
    {
        isFishing = !isFishing;

        if (isFishing)
        {
            // Disarm combat sword if holding
            if (playerCombat != null)
            {
                playerCombat.DisarmCombat();
            }

            if (animator != null && HasParameter(animator, isFishingParam))
            {
                animator.SetBool(isFishingParam, true);
            }

            Debug.Log("[PlayerFishing] 🎣 Đã chuyển sang chế độ CẦM CẦN CÂU (BẬT). Nhấn Chuột Trái để quăng câu!");
        }
        else
        {
            ExitFishingMode();
        }
    }

    /// <summary>
    /// Exits fishing stance and cancels any ongoing fishing action.
    /// </summary>
    public void ExitFishingMode()
    {
        isFishing = false;

        CancelActiveFishing();

        if (animator != null)
        {
            if (HasParameter(animator, isFishingParam)) animator.SetBool(isFishingParam, false);
            if (HasParameter(animator, isCastingParam)) animator.SetBool(isCastingParam, false);
        }

        Debug.Log("[PlayerFishing] 🎣 Đã cất cần câu (TẮT).");
    }

    private void StartCast()
    {
        if (fishingRoutine != null) StopCoroutine(fishingRoutine);
        fishingRoutine = StartCoroutine(CastAndFishRoutine());
    }

    private IEnumerator CastAndFishRoutine()
    {
        isCastingOrFishing = true;
        MovementLock.Lock(this);

        try
        {
            Vector2 facingDir = GetFacingDirection();
            UpdateRodTipPosition(facingDir);

            // Trigger Animator Cast state
            string castStateName = GetCastStateName(facingDir);
            if (animator != null)
            {
                if (HasParameter(animator, isCastingParam))
                {
                    animator.SetBool(isCastingParam, true);
                }

                int hash = Animator.StringToHash(castStateName);
                if (animator.HasState(0, hash))
                {
                    animator.Play(hash, 0, 0f);
                }
            }

            Debug.Log($"[PlayerFishing] 🎣 Quăng cần câu hướng {facingDir}!");

            // Wait for the swing animation to complete its forward toss
            yield return new WaitForSeconds(castSwingDelay);

            // Player can freely move while waiting for fish to bite
            MovementLock.Unlock(this);

            // Spawn Fishing Bobber in front of the player
            Vector3 landingPos = transform.position + new Vector3(facingDir.x, facingDir.y, 0f) * castDistance;
            SpawnBobber(landingPos);

            // Reset casting animation to holding idle pose
            if (animator != null && HasParameter(animator, isCastingParam))
            {
                animator.SetBool(isCastingParam, false);
            }

            // Wait for fish to bite (player can move freely during this time)
            float biteWaitTime = Random.Range(minBiteDelay, maxBiteDelay);
            yield return new WaitForSeconds(biteWaitTime);

            // Only proceed if still in fishing mode
            if (!isFishing) yield break;

            // Fish bites!
            if (currentBobber != null)
            {
                currentBobber.OnBite();
            }

            // Launch Minigame UI
            pendingFish = fishCatalog != null ? fishCatalog.RandomFish() : default;
            if (fishMinigameUI != null)
            {
                fishMinigameUI.StartMinigame(HandleCatchSuccess, HandleCatchFail, pendingFish.icon);
            }
            else
            {
                HandleCatchSuccess();
            }
        }
        finally
        {
            MovementLock.Unlock(this);
            isCastingOrFishing = false;
            fishingRoutine = null;
        }
    }

    private void SpawnBobber(Vector3 landingPos)
    {
        if (currentBobber != null)
        {
            currentBobber.ReelIn();
            currentBobber = null;
        }

        GameObject bobberGO = new GameObject("FishingBobber");
        currentBobber = bobberGO.AddComponent<FishingBobber>();
        currentBobber.Initialize(rodTipAnchor != null ? rodTipAnchor.transform : null, landingPos, bobberSprite);
    }

    private void HandleCatchSuccess()
    {
        FishCatalog.Fish caught = pendingFish.icon != null
            ? pendingFish : fishCatalog != null ? fishCatalog.RandomFish() : default;
        pendingFish = default;
        if (caught.icon == null || InventoryUI.Instance == null ||
            !InventoryUI.Instance.TryAddFish(caught.name, caught.icon))
        {
            Debug.LogWarning("[PlayerFishing] No fish sprite is configured or the inventory is full; the fish was released.");
            FinishCatch();
            return;
        }

        fishCount++;
        Debug.Log($"[PlayerFishing] Caught {caught.name}! Total fish: {fishCount}");

        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.AddProgress(QuestType.CatchFish, 1);
        }

        FinishCatch();
    }

    private void FinishCatch()
    {
        if (currentBobber != null)
        {
            currentBobber.ReelIn();
            currentBobber = null;
        }

        // Always restore movement on completion
        MovementLock.Unlock(this);
        isCastingOrFishing = false;
        fishingRoutine = null;
    }

#if UNITY_EDITOR
    [ContextMenu("Test Catch Fish")]
    private void TestCatchFish()
    {
        if (Application.isPlaying) HandleCatchSuccess();
    }
#endif

    private void HandleCatchFail()
    {
        pendingFish = default;
        Debug.Log("[PlayerFishing] 💨 Cá đã cắn đứt dây và trốn thoát!");

        if (currentBobber != null)
        {
            currentBobber.ReelIn();
            currentBobber = null;
        }

        // Always restore movement on fail
        MovementLock.Unlock(this);
        isCastingOrFishing = false;
        fishingRoutine = null;
    }

    private void CancelActiveFishing()
    {
        pendingFish = default;
        if (fishingRoutine != null)
        {
            StopCoroutine(fishingRoutine);
            fishingRoutine = null;
        }

        if (currentBobber != null)
        {
            currentBobber.ReelIn();
            currentBobber = null;
        }

        if (fishMinigameUI != null && fishMinigameUI.IsPlaying)
        {
            fishMinigameUI.CloseMinigame();
        }

        MovementLock.Unlock(this);
        isCastingOrFishing = false;
    }

    private void OnDisable()
    {
        MovementLock.Unlock(this);
        isCastingOrFishing = false;
    }

    private void OnDestroy()
    {
        MovementLock.Unlock(this);
    }

    private void UpdateRodTipPosition(Vector2 dir)
    {
        if (rodTipAnchor == null) return;

        if (dir.y > 0.1f)
        {
            rodTipAnchor.transform.localPosition = rodTipUp;
        }
        else if (dir.y < -0.1f)
        {
            rodTipAnchor.transform.localPosition = rodTipDown;
        }
        else if (dir.x < -0.1f)
        {
            rodTipAnchor.transform.localPosition = rodTipLeft;
        }
        else
        {
            rodTipAnchor.transform.localPosition = rodTipRight;
        }
    }

    private Vector2 GetFacingDirection()
    {
        if (animator == null) return Vector2.down;

        float lastX = HasParameter(animator, "LastMoveX") ? animator.GetFloat("LastMoveX") : 0f;
        float lastY = HasParameter(animator, "LastMoveY") ? animator.GetFloat("LastMoveY") : -1f;

        if (Mathf.Abs(lastX) > Mathf.Abs(lastY))
        {
            return lastX > 0f ? Vector2.right : Vector2.left;
        }
        else
        {
            return lastY > 0f ? Vector2.up : Vector2.down;
        }
    }

    private string GetCastStateName(Vector2 dir)
    {
        if (dir == Vector2.up) return "mc-fish-cast-up";
        if (dir == Vector2.left) return "mc-fish-cast-left";
        if (dir == Vector2.right) return "mc-fish-cast-right";
        return "mc-fish-cast-down";
    }

    private bool ReadToggleFishingInput()
    {
        try
        {
            if (Keyboard.current != null)
            {
                if (Keyboard.current.digit2Key.wasPressedThisFrame || Keyboard.current.numpad2Key.wasPressedThisFrame)
                {
                    return true;
                }
            }
        }
        catch { }

        try
        {
            return Input.GetKeyDown(toggleFishingKey) || Input.GetKeyDown(KeyCode.Keypad2);
        }
        catch { return false; }
    }

    private bool ReadCastInput()
    {
        if (MovementLock.IsLocked || QuestWindowUI.IsOpen || VisualNovelDialogueUI.IsOpen)
        {
            return false;
        }

        if (UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
        {
            return false;
        }

        try
        {
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                return true;
            }
        }
        catch { }

        try
        {
            return Input.GetMouseButtonDown(0);
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
}
