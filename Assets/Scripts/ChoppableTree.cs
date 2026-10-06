using System.Collections;
using UnityEngine;

/// <summary>
/// Controls tree chopping mechanics. Requires 5 hits to fell the tree.
/// Plays tree hit shake and fall animation (Tree1), drops Wood GameObjects,
/// and notifies TreeSpawner upon destruction.
/// </summary>
public class ChoppableTree : MonoBehaviour, IInteractable
{
    [Header("Chopping Settings")]
    [Tooltip("Number of chops required to fell this tree.")]
    [SerializeField] private int maxHits = 5;

    [Tooltip("Current hits received.")]
    [SerializeField] private int currentHits = 0;

    [Header("Animation & Visuals")]
    [SerializeField] private Animator animator;
    [Tooltip("Animator trigger or state name for tree falling animation.")]
    [SerializeField] private string fallStateName = "Tree-fall";
    [SerializeField] private string fallTriggerParam = "Fall";

    [Header("Wood Drops")]
    [Tooltip("Wood item Prefab or GameObject spawned when the tree falls.")]
    [SerializeField] private GameObject woodPrefab;

    [Tooltip("Number of Wood items dropped when felled.")]
    [SerializeField] private int woodDropCount = 3;

    [Tooltip("Radius around tree base where wood items spawn.")]
    [SerializeField] private float dropRadius = 1.0f;

    [Tooltip("Time in seconds before the tree object is destroyed after starting to fall.")]
    [SerializeField] private float fallDestroyDelay = 1.2f;

    [Header("Audio & Effects (Optional)")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip hitSound;
    [SerializeField] private AudioClip fallSound;

    private bool isFelling = false;
    private Vector3 originalLocalPos;

    private void Awake()
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        originalLocalPos = transform.localPosition;

        if (GetComponent<Collider2D>() == null)
        {
            BoxCollider2D col = gameObject.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
        }
    }

    public bool CanInteract(GameObject player)
    {
        return !isFelling && currentHits < maxHits;
    }

    public void Interact(GameObject player)
    {
        HitTree(player);
    }

    /// <summary>
    /// Called when MC chops this tree. Returns true if hit was registered.
    /// </summary>
    public bool HitTree(GameObject player)
    {
        if (isFelling || currentHits >= maxHits)
        {
            return false;
        }

        currentHits++;

        if (audioSource != null && hitSound != null)
        {
            audioSource.PlayOneShot(hitSound);
        }

        StartCoroutine(HitShakeRoutine());

        Debug.Log($"[ChoppableTree] 🪓 Chặt cây {gameObject.name}! ({currentHits}/{maxHits})");

        if (currentHits >= maxHits)
        {
            StartCoroutine(FellTreeRoutine());
        }

        return true;
    }

    private IEnumerator HitShakeRoutine()
    {
        float shakeTime = 0.15f;
        float elapsed = 0f;
        float shakeAmount = 0.08f;

        while (elapsed < shakeTime)
        {
            elapsed += Time.deltaTime;
            float offsetX = Random.Range(-shakeAmount, shakeAmount);
            transform.localPosition = originalLocalPos + new Vector3(offsetX, 0f, 0f);
            yield return null;
        }

        transform.localPosition = originalLocalPos;
    }

    private IEnumerator FellTreeRoutine()
    {
        isFelling = true;

        if (audioSource != null && fallSound != null)
        {
            audioSource.PlayOneShot(fallSound);
        }

        // Play Fall animation if Animator is attached
        if (animator != null)
        {
            if (HasParameter(animator, fallTriggerParam))
            {
                animator.SetTrigger(fallTriggerParam);
            }
            else
            {
                int stateHash = Animator.StringToHash(fallStateName);
                if (animator.HasState(0, stateHash))
                {
                    animator.Play(stateHash, 0, 0f);
                }
            }
        }

        Debug.Log($"[ChoppableTree] 🪵 Cây {gameObject.name} đã bị chặt đổ! Đang rơi ra Gỗ...");

        // Disable collider so player doesn't keep hitting the falling tree
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;

        yield return new WaitForSeconds(0.4f);

        // Spawn Wood items
        SpawnWoodDrops();

        yield return new WaitForSeconds(fallDestroyDelay - 0.4f);

        Destroy(gameObject);
    }

    private void SpawnWoodDrops()
    {
        for (int i = 0; i < woodDropCount; i++)
        {
            Vector2 randomOffset = Random.insideUnitCircle * dropRadius;
            Vector3 dropPos = transform.position + new Vector3(randomOffset.x, randomOffset.y, 0f);

            if (woodPrefab != null)
            {
                Instantiate(woodPrefab, dropPos, Quaternion.identity);
            }
            else
            {
                // Fallback: Create dynamic Wood GameObject with WoodItem component
                GameObject woodGO = new GameObject("WoodItem");
                woodGO.transform.position = dropPos;
                woodGO.AddComponent<WoodItem>();
            }
        }
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
