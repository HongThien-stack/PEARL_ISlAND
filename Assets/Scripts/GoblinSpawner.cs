using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spawns Goblin monsters within a designated area.
/// Features:
/// - Max active goblins cap
/// - Periodic respawn timer
/// - Initial spawn on start
/// - Radius or BoxCollider2D area
/// - Minimum distance check from Player to prevent spawning on top of the player
/// - Automatic cleanup when goblins are killed
/// - Gizmos visualization in Scene view
/// </summary>
public class GoblinSpawner : MonoBehaviour
{
    [Header("Goblin Prefab")]
    [Tooltip("The Goblin prefab to instantiate. If left empty, will try to load from 'Assets/Prefabs/Goblin.prefab'.")]
    [SerializeField] private GameObject goblinPrefab;

    [Header("Spawn Limits & Timers")]
    [Tooltip("Maximum number of active Goblins allowed from this spawner.")]
    [SerializeField] private int maxGoblins = 3;

    [Tooltip("Number of Goblins spawned immediately when the game starts.")]
    [SerializeField] private int initialSpawnCount = 1;

    [Tooltip("Time in seconds between spawn attempts.")]
    [SerializeField] private float spawnInterval = 10f;

    [Header("Spawn Area")]
    [Tooltip("Spawn shape mode: Circle radius or BoxCollider2D area.")]
    [SerializeField] private SpawnAreaType areaType = SpawnAreaType.CircleRadius;

    [Tooltip("Radius around this GameObject where Goblins can spawn (Circle mode).")]
    [SerializeField] private float spawnRadius = 5f;

    [Tooltip("Optional BoxCollider2D boundary (Box mode). If null, uses BoxCollider2D on this GameObject.")]
    [SerializeField] private BoxCollider2D boxSpawnArea;

    [Header("Player Distance Check")]
    [Tooltip("Avoid spawning too close to the player.")]
    [SerializeField] private float minDistanceFromPlayer = 3f;

    public enum SpawnAreaType
    {
        CircleRadius,
        BoxCollider
    }

    private readonly List<GameObject> activeGoblins = new List<GameObject>();
    private float spawnTimer = 0f;
    private Transform playerTransform;

    public int ActiveGoblinCount => activeGoblins.Count;

    private void Awake()
    {
        // Ensure any collider on this spawner is strictly a trigger to never block player movement
        var colliders = GetComponents<Collider2D>();
        foreach (var col in colliders)
        {
            col.isTrigger = true;
        }

        if (boxSpawnArea == null && areaType == SpawnAreaType.BoxCollider)
        {
            boxSpawnArea = GetComponent<BoxCollider2D>();
        }

        if (boxSpawnArea != null)
        {
            boxSpawnArea.isTrigger = true;
        }

        // Auto-load Goblin prefab if not assigned
        if (goblinPrefab == null)
        {
#if UNITY_EDITOR
            goblinPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Goblin.prefab");
#endif
        }
    }

    private void OnValidate()
    {
        var colliders = GetComponents<Collider2D>();
        foreach (var col in colliders)
        {
            col.isTrigger = true;
        }
    }

    private void Start()
    {
        FindPlayer();

        // Spawn initial batch
        int countToSpawn = Mathf.Min(initialSpawnCount, maxGoblins);
        for (int i = 0; i < countToSpawn; i++)
        {
            TrySpawnGoblin();
        }
    }

    private void Update()
    {
        CleanDeadGoblins();

        if (activeGoblins.Count >= maxGoblins)
        {
            return;
        }

        spawnTimer += Time.deltaTime;
        if (spawnTimer >= spawnInterval)
        {
            spawnTimer = 0f;
            TrySpawnGoblin();
        }
    }

    /// <summary>
    /// Attempts to spawn a Goblin at a random valid position within the spawn area.
    /// </summary>
    /// <returns>True if successfully spawned, false otherwise.</returns>
    public bool TrySpawnGoblin()
    {
        CleanDeadGoblins();

        if (activeGoblins.Count >= maxGoblins)
        {
            return false;
        }

        if (goblinPrefab == null)
        {
            Debug.LogWarning("[GoblinSpawner] ⚠️ Chưa gán Goblin Prefab vào Spawner!", this);
            return false;
        }

        Vector3 spawnPos = GetRandomSpawnPosition();

        // Check distance to player
        if (playerTransform == null) FindPlayer();
        if (playerTransform != null)
        {
            float distToPlayer = Vector2.Distance(spawnPos, playerTransform.position);
            if (distToPlayer < minDistanceFromPlayer)
            {
                // Try to nudge position away from player
                Vector2 dirAway = ((Vector2)spawnPos - (Vector2)playerTransform.position).normalized;
                spawnPos = (Vector2)playerTransform.position + dirAway * minDistanceFromPlayer;
            }
        }

        // Instantiate Goblin
        GameObject newGoblin = Instantiate(goblinPrefab, spawnPos, Quaternion.identity);
        newGoblin.name = $"Goblin_{activeGoblins.Count + 1}";

        // Parent to this spawner or scene root
        newGoblin.transform.SetParent(transform, true);

        activeGoblins.Add(newGoblin);

        Debug.Log($"[GoblinSpawner] 👹 Đã xuất hiện Goblin mới tại {spawnPos:F1}! (Tổng hiện tại: {activeGoblins.Count}/{maxGoblins})", newGoblin);
        return true;
    }

    private Vector3 GetRandomSpawnPosition()
    {
        if (areaType == SpawnAreaType.BoxCollider && boxSpawnArea != null)
        {
            Bounds b = boxSpawnArea.bounds;
            float randomX = Random.Range(b.min.x, b.max.x);
            float randomY = Random.Range(b.min.y, b.max.y);
            return new Vector3(randomX, randomY, 0f);
        }

        // Circle radius mode
        Vector2 randomCircle = Random.insideUnitCircle * spawnRadius;
        return transform.position + new Vector3(randomCircle.x, randomCircle.y, 0f);
    }

    private void CleanDeadGoblins()
    {
        for (int i = activeGoblins.Count - 1; i >= 0; i--)
        {
            if (activeGoblins[i] == null)
            {
                activeGoblins.RemoveAt(i);
                continue;
            }

            // Also check if Health is dead
            Health hp = activeGoblins[i].GetComponent<Health>();
            if (hp != null && hp.IsDead)
            {
                activeGoblins.RemoveAt(i);
            }
        }
    }

    private void FindPlayer()
    {
        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
        {
            playerTransform = playerObj.transform;
        }
        else
        {
            PlayerMovement pm = Object.FindFirstObjectByType<PlayerMovement>();
            if (pm != null)
            {
                playerTransform = pm.transform;
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.9f, 0.2f, 0.2f, 0.8f);

        if (areaType == SpawnAreaType.BoxCollider && boxSpawnArea != null)
        {
            Gizmos.DrawWireCube(boxSpawnArea.bounds.center, boxSpawnArea.bounds.size);
        }
        else
        {
            Gizmos.DrawWireSphere(transform.position, spawnRadius);
        }

        if (minDistanceFromPlayer > 0f)
        {
            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, minDistanceFromPlayer);
        }
    }
}
