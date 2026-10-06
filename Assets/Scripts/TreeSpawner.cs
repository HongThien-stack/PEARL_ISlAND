using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spawns trees randomly within a designated area (BoxCollider2D).
/// Features: Max tree count limit, spawn interval timer, random tree selection,
/// and automatic 2D Y-sorting for spawned trees.
/// </summary>
public class TreeSpawner : MonoBehaviour
{
    [Header("Tree Assets")]
    [Tooltip("List of Tree Prefabs to spawn. If assigned, prefabs will be instantiated.")]
    [SerializeField] private GameObject[] treePrefabs;

    [Tooltip("List of Tree Sprites. Used if treePrefabs array is empty.")]
    [SerializeField] private Sprite[] treeSprites;

    [Header("Spawn Settings")]
    [Tooltip("Maximum number of active trees allowed in this spawn area.")]
    [SerializeField] private int maxTrees = 10;

    [Tooltip("Time in seconds between each tree spawn attempt.")]
    [SerializeField] private float spawnInterval = 5f;

    [Tooltip("Initial number of trees to spawn when the scene starts.")]
    [SerializeField] private int initialSpawnCount = 3;

    [Tooltip("Scale applied to spawned tree objects.")]
    [SerializeField] private Vector3 treeScale = Vector3.one;

    [Header("Spawn Area")]
    [Tooltip("BoxCollider2D defining the boundary for spawning. If null, uses BoxCollider2D on this GameObject.")]
    [SerializeField] private BoxCollider2D spawnArea;

    [Header("2D Y-Sorting Settings")]
    [SerializeField] private bool applyYSorting = true;
    [SerializeField] private int sortingOrderOffset = 30000;
    [SerializeField] private float ySortingMultiplier = 1.3f;

    private List<GameObject> activeTrees = new List<GameObject>();
    private float spawnTimer = 0f;

    private void Awake()
    {
        if (spawnArea == null)
        {
            spawnArea = GetComponent<BoxCollider2D>();
        }

        if (spawnArea != null)
        {
            spawnArea.isTrigger = true;
        }
    }

    private void Start()
    {
        // Initial spawn batch on start
        int spawnCount = Mathf.Min(initialSpawnCount, maxTrees);
        for (int i = 0; i < spawnCount; i++)
        {
            TrySpawnTree();
        }
    }

    private void Update()
    {
        CleanActiveTreesList();

        if (activeTrees.Count >= maxTrees)
        {
            return;
        }

        spawnTimer += Time.deltaTime;
        if (spawnTimer >= spawnInterval)
        {
            spawnTimer = 0f;
            TrySpawnTree();
        }
    }

    /// <summary>
    /// Attempts to spawn a single tree at a random position inside the spawnArea.
    /// </summary>
    public bool TrySpawnTree()
    {
        CleanActiveTreesList();

        if (activeTrees.Count >= maxTrees)
        {
            return false;
        }

        Vector3 spawnPos = GetRandomSpawnPosition();
        GameObject newTree = null;

        // Option 1: Instantiate from Tree Prefabs
        if (treePrefabs != null && treePrefabs.Length > 0)
        {
            GameObject randomPrefab = treePrefabs[Random.Range(0, treePrefabs.Length)];
            if (randomPrefab != null)
            {
                newTree = Instantiate(randomPrefab, spawnPos, Quaternion.identity, transform);
            }
        }
        // Option 2: Create GameObject with SpriteRenderer from Tree Sprites
        else if (treeSprites != null && treeSprites.Length > 0)
        {
            Sprite randomSprite = treeSprites[Random.Range(0, treeSprites.Length)];
            if (randomSprite != null)
            {
                newTree = new GameObject("SpawnedTree_" + (activeTrees.Count + 1));
                newTree.transform.SetParent(transform, false);
                newTree.transform.position = spawnPos;

                SpriteRenderer sr = newTree.AddComponent<SpriteRenderer>();
                sr.sprite = randomSprite;
                sr.spriteSortPoint = SpriteSortPoint.Pivot;
            }
        }

        if (newTree != null)
        {
            newTree.transform.localScale = treeScale;

            if (applyYSorting)
            {
                SpriteRenderer sr = newTree.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    sr.sortingOrder = sortingOrderOffset + Mathf.RoundToInt(-spawnPos.y * ySortingMultiplier);
                }
            }

            // Ensure spawned trees have ChoppableTree mechanics attached
            if (newTree.GetComponent<ChoppableTree>() == null)
            {
                newTree.AddComponent<ChoppableTree>();
            }

            activeTrees.Add(newTree);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Gets a random position within the BoxCollider2D bounds.
    /// </summary>
    private Vector3 GetRandomSpawnPosition()
    {
        if (spawnArea != null)
        {
            Bounds bounds = spawnArea.bounds;
            float randomX = Random.Range(bounds.min.x, bounds.max.x);
            float randomY = Random.Range(bounds.min.y, bounds.max.y);
            return new Vector3(randomX, randomY, transform.position.z);
        }

        // Fallback to local origin
        return transform.position;
    }

    /// <summary>
    /// Removes destroyed or harvested trees from tracking list.
    /// </summary>
    private void CleanActiveTreesList()
    {
        activeTrees.RemoveAll(tree => tree == null);
    }

    /// <summary>
    /// Returns current number of active trees.
    /// </summary>
    public int GetActiveTreeCount()
    {
        CleanActiveTreesList();
        return activeTrees.Count;
    }

    private void OnDrawGizmosSelected()
    {
        // Visualize the spawn area in scene view
        BoxCollider2D col = spawnArea != null ? spawnArea : GetComponent<BoxCollider2D>();
        if (col != null)
        {
            Gizmos.color = new Color(0.2f, 0.9f, 0.3f, 0.35f);
            Gizmos.DrawCube(col.bounds.center, col.bounds.size);
            Gizmos.color = new Color(0.2f, 0.9f, 0.3f, 0.9f);
            Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
        }
    }
}
