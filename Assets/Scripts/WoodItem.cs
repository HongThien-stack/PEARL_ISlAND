using System.Collections;
using UnityEngine;

/// <summary>
/// Dropped Wood item spawned when a tree is felled.
/// Can be picked up by MC walking near it or interacting.
/// Tracks collected wood count until a full Inventory system is built.
/// </summary>
public class WoodItem : MonoBehaviour, IInteractable
{
    public static int totalWoodCollected = 0;

    [Header("Wood Item Settings")]
    [SerializeField] private int woodAmount = 1;
    [SerializeField] private Sprite woodSprite;

    [Header("Pickup Settings")]
    [Tooltip("If true, automatically picks up when player walks within radius.")]
    [SerializeField] private bool autoPickupOnTouch = true;
    [SerializeField] private float pickupRadius = 1.2f;

    [Header("Audio (Optional)")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip pickupSound;

    private SpriteRenderer spriteRenderer;
    private bool isBeingPickedUp = false;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        }

        if (woodSprite != null)
        {
            spriteRenderer.sprite = woodSprite;
        }

        spriteRenderer.spriteSortPoint = SpriteSortPoint.Pivot;
        spriteRenderer.sortingOrder = 30000 + Mathf.RoundToInt(-transform.position.y * 1.3f);

        Collider2D col = GetComponent<Collider2D>();
        if (col == null)
        {
            CircleCollider2D circleCol = gameObject.AddComponent<CircleCollider2D>();
            circleCol.isTrigger = true;
            circleCol.radius = pickupRadius;
        }

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }
    }

    private void Update()
    {
        if (isBeingPickedUp || !autoPickupOnTouch) return;

        // Auto pickup check
        PlayerMovement pm = FindAnyObjectByType<PlayerMovement>();
        if (pm != null)
        {
            float dist = Vector2.Distance(transform.position, pm.transform.position);
            if (dist <= pickupRadius)
            {
                CollectWood(pm.gameObject);
            }
        }
    }

    public bool CanInteract(GameObject player)
    {
        return !isBeingPickedUp;
    }

    public void Interact(GameObject player)
    {
        CollectWood(player);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (autoPickupOnTouch && !isBeingPickedUp)
        {
            if (other.CompareTag("Player") || other.GetComponent<PlayerMovement>() != null)
            {
                CollectWood(other.gameObject);
            }
        }
    }

    public void CollectWood(GameObject player)
    {
        if (isBeingPickedUp) return;
        isBeingPickedUp = true;

        totalWoodCollected += woodAmount;

        Debug.Log($"[WoodItem] 🪵 MC đã nhặt +{woodAmount} Gỗ! Tổng số gỗ hiện có: {totalWoodCollected}");

        if (audioSource != null && pickupSound != null)
        {
            audioSource.PlayOneShot(pickupSound);
        }

        StartCoroutine(PickupFlyRoutine(player));
    }

    private IEnumerator PickupFlyRoutine(GameObject player)
    {
        float duration = 0.25f;
        float elapsed = 0f;
        Vector3 startPos = transform.position;
        Vector3 startScale = transform.localScale;

        while (elapsed < duration && player != null)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            transform.position = Vector3.Lerp(startPos, player.transform.position, t);
            transform.localScale = Vector3.Lerp(startScale, Vector3.zero, t);
            yield return null;
        }

        Destroy(gameObject);
    }
}
