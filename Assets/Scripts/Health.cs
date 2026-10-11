using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Health component for Player, Enemies, and destructible entities.
/// Manages HP, hit flash visual feedback, knockback, and death events.
/// </summary>
public class Health : MonoBehaviour
{
    [Header("Health Settings")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float currentHealth;
    [SerializeField] private bool destroyOnDeath = true;
    [SerializeField] private float deathDelay = 1.0f;

    [Header("Invulnerability & Visuals")]
    [SerializeField] private float invulnerabilityDuration = 0.2f;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Color hitFlashColor = Color.red;

    public event Action<float, float> OnHealthChanged; // (current, max)
    public event Action<float> OnTakeDamage;           // (damageAmount)
    public event Action OnDeath;

    private bool isDead = false;
    private bool isInvulnerable = false;
    private Color originalSpriteColor = Color.white;
    private Rigidbody2D rb;

    public float MaxHealth => maxHealth;
    public float CurrentHealth => currentHealth;
    public bool IsDead => isDead;

    private void Awake()
    {
        currentHealth = maxHealth;
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null) originalSpriteColor = spriteRenderer.color;
        rb = GetComponent<Rigidbody2D>();
    }

    private void Start()
    {
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public bool TakeDamage(float damage, Vector2 knockbackDirection = default, float knockbackForce = 0f)
    {
        if (isDead || isInvulnerable || damage <= 0f) return false;

        currentHealth = Mathf.Max(0f, currentHealth - damage);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        OnTakeDamage?.Invoke(damage);

        // Flash sprite
        if (spriteRenderer != null)
        {
            StartCoroutine(HitFlashRoutine());
        }

        // Apply Knockback
        if (knockbackForce > 0f && rb != null && knockbackDirection != Vector2.zero)
        {
            rb.AddForce(knockbackDirection.normalized * knockbackForce, ForceMode2D.Impulse);
        }

        Debug.Log($"[Health] 🩸 {gameObject.name} lấy sát thương: -{damage} HP (Còn lại: {currentHealth}/{maxHealth})");

        // Check Death
        if (currentHealth <= 0f && !isDead)
        {
            Die();
        }
        else
        {
            StartCoroutine(InvulnerabilityRoutine());
        }

        return true;
    }

    public void Heal(float amount)
    {
        if (isDead || amount <= 0f) return;
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    private void Die()
    {
        isDead = true;
        OnDeath?.Invoke();
        Debug.Log($"[Health] 💀 {gameObject.name} đã bị tiêu diệt!");

        if (destroyOnDeath)
        {
            Destroy(gameObject, deathDelay);
        }
    }

    private IEnumerator HitFlashRoutine()
    {
        if (spriteRenderer == null) yield break;
        spriteRenderer.color = hitFlashColor;
        yield return new WaitForSeconds(0.1f);
        spriteRenderer.color = originalSpriteColor;
    }

    private IEnumerator InvulnerabilityRoutine()
    {
        isInvulnerable = true;
        yield return new WaitForSeconds(invulnerabilityDuration);
        isInvulnerable = false;
    }
}
