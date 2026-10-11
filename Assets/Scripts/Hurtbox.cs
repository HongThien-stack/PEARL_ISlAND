using UnityEngine;

public enum Faction
{
    Player,
    Enemy,
    Neutral
}

/// <summary>
/// Hurtbox component attached to damageable objects/entities.
/// Receives hits from Hitbox components and passes damage to Health.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class Hurtbox : MonoBehaviour
{
    [Header("Hurtbox Settings")]
    [SerializeField] private Faction faction = Faction.Enemy;
    [SerializeField] private Health health;
    [SerializeField] private Collider2D hurtboxCollider;

    public Faction OwnerFaction { get => faction; set => faction = value; }
    public Health Health => health;

    private void Awake()
    {
        if (health == null) health = GetComponentInParent<Health>();
        if (hurtboxCollider == null) hurtboxCollider = GetComponent<Collider2D>();
        if (hurtboxCollider != null) hurtboxCollider.isTrigger = true;
    }

    public bool ReceiveHit(float damage, Faction attackerFaction, Vector2 attackOrigin, float knockbackForce)
    {
        if (health == null || health.IsDead) return false;
        if (faction == attackerFaction) return false; // Friendly fire protection

        Vector2 knockbackDir = (transform.position - (Vector3)attackOrigin).normalized;
        return health.TakeDamage(damage, knockbackDir, knockbackForce);
    }
}
