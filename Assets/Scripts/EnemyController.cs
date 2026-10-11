using UnityEngine;

/// <summary>
/// Sample Enemy component equipped with Health, Hurtbox, and Overhead FloatingHealthBar.
/// Demonstrates hit reaction, taking damage, and dying.
/// </summary>
[RequireComponent(typeof(Health))]
[RequireComponent(typeof(Rigidbody2D))]
public class EnemyController : MonoBehaviour
{
    [Header("Enemy Settings")]
    [SerializeField] private string enemyName = "Monster";
    [SerializeField] private float maxHp = 100f;

    private Health health;

    private void Awake()
    {
        health = GetComponent<Health>();

        // Configure Rigidbody2D
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.gravityScale = 0f;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            rb.linearDamping = 5f; // Add dampening for smooth knockback
        }

        // Ensure Hurtbox attached to Enemy (Faction.Enemy)
        Hurtbox hurtbox = GetComponentInChildren<Hurtbox>();
        if (hurtbox == null)
        {
            GameObject hurtboxGO = new GameObject("Hurtbox");
            hurtboxGO.transform.SetParent(transform, false);
            hurtboxGO.transform.localPosition = Vector3.zero;

            BoxCollider2D col = hurtboxGO.AddComponent<BoxCollider2D>();
            col.size = new Vector2(0.9f, 0.9f);
            col.isTrigger = true;

            hurtbox = hurtboxGO.AddComponent<Hurtbox>();
            hurtbox.OwnerFaction = Faction.Enemy;
        }

        // Ensure Floating Health Bar overhead
        FloatingHealthBar healthBar = GetComponentInChildren<FloatingHealthBar>();
        if (healthBar == null)
        {
            GameObject healthBarGO = new GameObject("FloatingHealthBar");
            healthBarGO.transform.SetParent(transform, false);
            healthBarGO.transform.localPosition = Vector3.zero;
            healthBar = healthBarGO.AddComponent<FloatingHealthBar>();
        }
    }
}
