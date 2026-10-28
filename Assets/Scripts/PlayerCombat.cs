using System.Collections;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Handles MC's Combat Stance switching (Key 1) and Fight Attack action (Key J).
/// Switches Animator between Normal mode and Fight mode (mc-fight-idle, mc-fight-walk, mc-fight-attack).
/// </summary>
public class PlayerCombat : MonoBehaviour
{
    [Header("Combat Key Settings")]
    [Tooltip("Key to toggle Combat Stance (Key 1).")]
    [SerializeField] private KeyCode toggleCombatKey = KeyCode.Alpha1;

    [Tooltip("Key to perform Fight Attack (Key J).")]
    [SerializeField] private KeyCode attackKey = KeyCode.J;

    [Header("Animator Integration")]
    [SerializeField] private Animator animator;
    [SerializeField] private string isFightingParam = "IsFighting";
    [SerializeField] private string isAttackingParam = "IsAttacking";
    [SerializeField] private string attackTriggerParam = "Attack";

    [Header("Attack State Names")]
    [SerializeField] private string attackStateDown = "mc-fight-attack-down";
    [SerializeField] private string attackStateUp = "mc-fight-attack-up";
    [SerializeField] private string attackStateLeft = "mc-fight-attack-left";
    [SerializeField] private string attackStateRight = "mc-fight-attack-right";

    [Tooltip("Duration in seconds of the attack animation lock.")]
    [SerializeField] private float attackDuration = 0.65f;

    [Header("Hitbox Settings")]
    [SerializeField] private float attackDamage = 25f;
    [SerializeField] private float knockbackForce = 4f;
    [SerializeField] private Hitbox attackHitbox;

    [Header("Combat Audio")]
    [SerializeField] private AudioClip swordSwingSound;
    [SerializeField, Range(0f, 1f)] private float swordSwingVolume = 0.8f;

    private PlayerMovement playerMovement;
    private AudioSource combatAudio;
    private Health playerHealth;
    private Hurtbox playerHurtbox;
    private bool isFighting = false;
    private bool isAttacking = false;

    public bool IsFighting => isFighting;
    public bool IsAttacking => isAttacking;

    private void Awake()
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }
        playerMovement = GetComponent<PlayerMovement>();
        combatAudio = gameObject.AddComponent<AudioSource>();
        combatAudio.playOnAwake = false;
        combatAudio.spatialBlend = 0f;

        // Ensure Player Health & Hurtbox
        playerHealth = GetComponent<Health>();
        if (playerHealth == null) playerHealth = gameObject.AddComponent<Health>();

        playerHurtbox = GetComponentInChildren<Hurtbox>();
        if (playerHurtbox == null)
        {
            GameObject hurtboxGO = new GameObject("PlayerHurtbox");
            hurtboxGO.transform.SetParent(transform, false);
            hurtboxGO.transform.localPosition = Vector3.zero;

            BoxCollider2D col = hurtboxGO.AddComponent<BoxCollider2D>();
            col.size = new Vector2(0.8f, 0.8f);
            col.isTrigger = true;

            playerHurtbox = hurtboxGO.AddComponent<Hurtbox>();
            playerHurtbox.OwnerFaction = Faction.Player;
        }

        // Ensure Attack Hitbox
        if (attackHitbox == null)
        {
            Transform existing = transform.Find("AttackHitbox");
            if (existing != null)
            {
                attackHitbox = existing.GetComponent<Hitbox>();
            }

            if (attackHitbox == null)
            {
                GameObject hitboxGO = new GameObject("AttackHitbox");
                hitboxGO.transform.SetParent(transform, false);

                BoxCollider2D col = hitboxGO.AddComponent<BoxCollider2D>();
                col.size = new Vector2(1.2f, 1.2f);
                col.isTrigger = true;

                attackHitbox = hitboxGO.AddComponent<Hitbox>();
                attackHitbox.OwnerFaction = Faction.Player;
            }
        }

        if (attackHitbox != null)
        {
            attackHitbox.Damage = attackDamage;
            attackHitbox.KnockbackForce = knockbackForce;
            attackHitbox.gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        // 1. If currently in Fishing Mode, don't trigger combat sword actions
        if (PlayerFishing.Instance != null && PlayerFishing.Instance.IsFishing)
        {
            if (ReadToggleCombatInput())
            {
                PlayerFishing.Instance.ExitFishingMode();
                ToggleCombatStance();
            }
            return;
        }

        // 2. Toggle Combat Stance with Key 1 (Draw / Sheathe Sword)
        if (ReadToggleCombatInput())
        {
            ToggleCombatStance();
        }

        // 3. Perform Attack only when holding sword (isFighting == true)
        // Ignore attack input while unarmed so normal gameplay cannot be blocked or spam warnings.
        if (isFighting && ReadAttackInput() && !isAttacking)
        {
            TryAttack();
        }
    }

    /// <summary>
    /// Toggles between Normal Stance and Combat Stance (Draw / Sheathe Sword).
    /// </summary>
    public void ToggleCombatStance()
    {
        // If fishing mode was active, exit it
        if (PlayerFishing.Instance != null && PlayerFishing.Instance.IsFishing)
        {
            PlayerFishing.Instance.ExitFishingMode();
        }

        isFighting = !isFighting;

        if (animator != null && HasParameter(animator, isFightingParam))
        {
            animator.SetBool(isFightingParam, isFighting);
        }

        Debug.Log($"[PlayerCombat] ⚔️ Đã chuyển chế độ Cầm kiếm: {(isFighting ? "RÚT KIẾM (BẬT)" : "TRA KIẾM (TẮT)")}");
    }

    /// <summary>
    /// Forcefully disarms combat mode and puts away the sword.
    /// </summary>
    public void DisarmCombat()
    {
        if (isFighting)
        {
            isFighting = false;
            if (animator != null && HasParameter(animator, isFightingParam))
            {
                animator.SetBool(isFightingParam, false);
            }
            Debug.Log("[PlayerCombat] ⚔️ Đã tra kiếm vào bao để chuyển công cụ.");
        }
    }

    /// <summary>
    /// Performs a fight attack action (Only allowed when holding sword).
    /// </summary>
    public void TryAttack()
    {
        if (isAttacking || !isFighting) return;

        StartCoroutine(PerformAttackRoutine());
    }

    private IEnumerator PerformAttackRoutine()
    {
        isAttacking = true;
        MovementLock.Lock(this);

        try
        {
            // Position Hitbox based on facing direction
            string attackState = GetAttackStateName();
            PositionHitbox(attackState);

            // Trigger Animator parameters
            if (animator != null)
            {
                if (HasParameter(animator, attackTriggerParam))
                {
                    animator.SetTrigger(attackTriggerParam);
                }
                if (HasParameter(animator, isAttackingParam))
                {
                    animator.SetBool(isAttackingParam, true);
                }

                int hash = Animator.StringToHash(attackState);
                if (animator.HasState(0, hash))
                {
                    animator.Play(hash, 0, 0f);
                }
            }

            Debug.Log("[PlayerCombat] 🥊 MC thực hiện đòn đánh (Fight Attack - Nút J)!");
            if (swordSwingSound != null)
                combatAudio.PlayOneShot(swordSwingSound, swordSwingVolume * GameSettings.EffectsVolume);

            // Enable Hitbox during swing peak
            if (attackHitbox != null)
            {
                attackHitbox.gameObject.SetActive(true);
            }

            yield return new WaitForSeconds(0.35f);

            // Disable Hitbox after swing peak
            if (attackHitbox != null)
            {
                attackHitbox.gameObject.SetActive(false);
            }

            yield return new WaitForSeconds(Mathf.Max(0f, attackDuration - 0.35f));

            if (animator != null && HasParameter(animator, isAttackingParam))
            {
                animator.SetBool(isAttackingParam, false);
            }
        }
        finally
        {
            if (attackHitbox != null) attackHitbox.gameObject.SetActive(false);
            MovementLock.Unlock(this);
            isAttacking = false;
        }
    }

    private void OnDisable()
    {
        MovementLock.Unlock(this);
        isAttacking = false;
    }

    private void OnDestroy()
    {
        MovementLock.Unlock(this);
    }

    private void PositionHitbox(string attackState)
    {
        if (attackHitbox == null) return;

        Vector3 offset = Vector3.down * 0.6f;
        if (attackState == attackStateUp) offset = Vector3.up * 0.6f;
        else if (attackState == attackStateLeft) offset = Vector3.left * 0.6f;
        else if (attackState == attackStateRight) offset = Vector3.right * 0.6f;

        attackHitbox.transform.localPosition = offset;
    }

    private string GetAttackStateName()
    {
        if (animator == null) return attackStateDown;
        float lastX = HasParameter(animator, "LastMoveX") ? animator.GetFloat("LastMoveX") : 0f;
        float lastY = HasParameter(animator, "LastMoveY") ? animator.GetFloat("LastMoveY") : -1f;

        if (lastY > 0.1f) return attackStateUp;
        if (lastY < -0.1f) return attackStateDown;
        if (lastX < -0.1f) return attackStateLeft;
        if (lastX > 0.1f) return attackStateRight;
        return attackStateDown;
    }

    private bool ReadToggleCombatInput()
    {
        try
        {
            if (Keyboard.current != null)
            {
                if (Keyboard.current.digit1Key.wasPressedThisFrame || Keyboard.current.numpad1Key.wasPressedThisFrame)
                {
                    return true;
                }
            }
        }
        catch { }

        try
        {
            return Input.GetKeyDown(toggleCombatKey) || Input.GetKeyDown(KeyCode.Keypad1);
        }
        catch { return false; }
    }

    private bool ReadAttackInput()
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
            if (Keyboard.current != null && Keyboard.current.jKey.wasPressedThisFrame)
            {
                return true;
            }
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                return true;
            }
        }
        catch { }

        try
        {
            return Input.GetKeyDown(attackKey) || Input.GetMouseButtonDown(0);
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
