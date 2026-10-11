using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Hitbox trigger component attached to attacks and weapons.
/// Deals damage to any colliding Hurtbox belonging to an opposing Faction.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class Hitbox : MonoBehaviour
{
    [Header("Hitbox Settings")]
    [SerializeField] private Faction ownerFaction = Faction.Player;
    [SerializeField] private float damage = 25f;
    [SerializeField] private float knockbackForce = 4f;
    [SerializeField] private Collider2D hitboxCollider;

    private HashSet<Hurtbox> hitHurtboxes = new HashSet<Hurtbox>();

    public Faction OwnerFaction { get => ownerFaction; set => ownerFaction = value; }
    public float Damage { get => damage; set => damage = value; }
    public float KnockbackForce { get => knockbackForce; set => knockbackForce = value; }

    private void Awake()
    {
        if (hitboxCollider == null) hitboxCollider = GetComponent<Collider2D>();
        if (hitboxCollider != null) hitboxCollider.isTrigger = true;
    }

    private void OnEnable()
    {
        hitHurtboxes.Clear();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        Hurtbox hurtbox = other.GetComponent<Hurtbox>();
        if (hurtbox == null) hurtbox = other.GetComponentInParent<Hurtbox>();

        if (hurtbox != null && !hitHurtboxes.Contains(hurtbox))
        {
            if (hurtbox.OwnerFaction != ownerFaction)
            {
                if (hurtbox.ReceiveHit(damage, ownerFaction, transform.position, knockbackForce))
                {
                    hitHurtboxes.Add(hurtbox);
                    Debug.Log($"[Hitbox] 💥 {gameObject.name} trúng {other.gameObject.name}! Đã gây {damage} sát thương!");
                }
            }
        }
    }
}
