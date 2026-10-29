using System.Collections;
using UnityEngine;

/// <summary>
/// Singleton manager coordinating restaurant business flow, customer spawning, and order dispatches.
/// Supports single or multiple food item menus (e.g. Pho Bo, Grilled Pork Rice).
/// </summary>
public class RestaurantManager : MonoBehaviour
{
    [System.Serializable]
    public class SeatTarget
    {
        [SerializeField] public Transform point;
        [SerializeField] public CustomerAI.SeatSide side = CustomerAI.SeatSide.Right;
        [Tooltip("Route for this seat, for example Waypoint 1 -> Waypoint 1_1.")]
        [SerializeField] public Transform[] waypoints;
        [HideInInspector] public bool occupied;

        public Transform Point => point;
        public CustomerAI.SeatSide Side => side;
        public Transform[] Waypoints => waypoints;
    }

    public static RestaurantManager Instance { get; private set; }

    [Header("Restaurant References")]
    [Tooltip("Reference to the Chef AI in the scene.")]
    [SerializeField] private ChefAI chefAI;

    [Tooltip("Point where customer spawns and leaves (e.g. Door).")]
    [SerializeField] private Transform doorPoint;

    [Tooltip("Available seats. Each seat owns its route and must be marked Left or Right.")]
    [SerializeField] private SeatTarget[] seats;
    [Tooltip("Legacy single-seat reference. Used when Seats is empty.")]
    [SerializeField] private Transform tableSeatPoint;

    [Tooltip("Customer prefabs to choose from randomly when spawning.")]
    [SerializeField] private GameObject[] customerPrefabs;

    [Tooltip("Legacy single customer prefab. Used when Customer Prefabs is empty.")]
    [SerializeField] private GameObject customerPrefab;

    [Tooltip("Existing Customer instance in scene (if not using Prefab).")]
    [SerializeField] private CustomerAI sceneCustomer;

    [Header("Food Menu")]
    [Tooltip("Single default food sprite to order.")]
    [SerializeField] private Sprite foodSpriteToOrder;

    [Tooltip("Array of available food sprites (e.g. Pho Bo, Cơm tấm). If assigned, customers will pick randomly from here!")]
    [SerializeField] private Sprite[] availableFoodMenu;

    [Header("Shop State")]
    [SerializeField] private bool isShopOpen = false;
    [SerializeField] private int maxCustomers = 10;
    [SerializeField] private float customerSpawnInterval = 5f;

    private Coroutine spawnRoutine;
    private bool previousShopOpenState;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DisableSceneCustomerTemplateWhenPrefabIsAssigned();
    }

    private void Start()
    {
        previousShopOpenState = isShopOpen;
        if (isShopOpen)
        {
            SpawnCustomer();
            spawnRoutine = StartCoroutine(TimedSpawnRoutine());
        }
    }

    private void Update()
    {
        if (isShopOpen == previousShopOpenState)
        {
            return;
        }

        previousShopOpenState = isShopOpen;

        if (isShopOpen)
        {
            SpawnCustomer();
            if (spawnRoutine == null)
            {
                spawnRoutine = StartCoroutine(TimedSpawnRoutine());
            }
        }
        else if (spawnRoutine != null)
        {
            StopCoroutine(spawnRoutine);
            spawnRoutine = null;
        }
    }

    public bool IsShopOpen()
    {
        return isShopOpen;
    }

    public bool HasActiveCustomer()
    {
        return FindObjectsByType<CustomerAI>(FindObjectsSortMode.None).Length > 0;
    }

    private void DisableSceneCustomerTemplateWhenPrefabIsAssigned()
    {
        if (!HasCustomerPrefab() || sceneCustomer == null)
        {
            return;
        }

        if (customerPrefab == sceneCustomer.gameObject || (customerPrefabs != null && System.Array.IndexOf(customerPrefabs, sceneCustomer.gameObject) >= 0))
        {
            sceneCustomer.gameObject.SetActive(false);
            Debug.Log("[RestaurantManager] Scene Customer template disabled because Customer Prefab is assigned.");
        }
    }

    private bool HasCustomerPrefab()
    {
        if (customerPrefabs != null)
        {
            foreach (GameObject prefab in customerPrefabs)
            {
                if (prefab != null)
                {
                    return true;
                }
            }
        }

        return customerPrefab != null;
    }

    private GameObject GetRandomCustomerPrefab()
    {
        if (customerPrefabs != null && customerPrefabs.Length > 0)
        {
            var validPrefabs = new System.Collections.Generic.List<GameObject>();
            foreach (GameObject prefab in customerPrefabs)
            {
                if (prefab != null)
                {
                    validPrefabs.Add(prefab);
                }
            }

            if (validPrefabs.Count > 0)
            {
                return validPrefabs[Random.Range(0, validPrefabs.Count)];
            }
        }

        return customerPrefab;
    }

    /// <summary>
    /// MC presses F at Door to open business.
    /// </summary>
    public void StartBusiness()
    {
        if (isShopOpen) return;

        isShopOpen = true;
        previousShopOpenState = true;
        Debug.Log("==========================================");
        Debug.Log("[RestaurantManager] BUSINESS STARTED! Open for customers.");
        Debug.Log("==========================================");

        SpawnCustomer();
        if (spawnRoutine == null)
        {
            spawnRoutine = StartCoroutine(TimedSpawnRoutine());
        }
    }

    /// <summary>
    /// Get a food sprite for customer order (random from menu or single assigned).
    /// </summary>
    public Sprite GetCustomerOrderSprite()
    {
        if (availableFoodMenu != null && availableFoodMenu.Length > 0)
        {
            int randomIndex = Random.Range(0, availableFoodMenu.Length);
            return availableFoodMenu[randomIndex];
        }

        return foodSpriteToOrder;
    }

    /// <summary>
    /// Spawns a customer at the door who moves to the seat.
    /// </summary>
    public void SpawnCustomer()
    {
        int configuredCapacity = seats != null && seats.Length > 0
            ? seats.Length
            : 1;
        int customerCapacity = Mathf.Min(Mathf.Max(1, maxCustomers), configuredCapacity);

        if (FindObjectsByType<CustomerAI>(FindObjectsSortMode.None).Length >= customerCapacity)
        {
            return;
        }

        // Auto-find references if missing
        if (chefAI == null) chefAI = FindAnyObjectByType<ChefAI>();
        if (sceneCustomer == null && !HasCustomerPrefab()) sceneCustomer = FindAnyObjectByType<CustomerAI>();

        // Check specifically what is missing and print clear debug warnings
        if (doorPoint == null)
        {
            Debug.LogError("[RestaurantManager] ❌ THIẾU 'Door Point'! Hãy kéo Transform vị trí Cửa vào ô Door Point trên RestaurantManager.");
            return;
        }

        SeatTarget seat = FindAvailableSeat();
        if (seat == null || seat.Point == null)
        {
            Debug.LogWarning("[RestaurantManager] Không còn ghế trống hoặc chưa cấu hình Seats.");
            return;
        }

        if (seats != null && seats.Length > 0 && (seat.Waypoints == null || seat.Waypoints.Length < 2))
        {
            Debug.LogError($"[RestaurantManager] Ghế '{seat.Point.name}' cần ít nhất 2 waypoint trong route riêng của ghế.");
            return;
        }

        CustomerAI customer = null;

        GameObject selectedPrefab = GetRandomCustomerPrefab();
        if (selectedPrefab != null)
        {
            GameObject customerGO = Instantiate(selectedPrefab, doorPoint.position, Quaternion.identity);
            customerGO.SetActive(true);
            customer = customerGO.GetComponent<CustomerAI>();
            if (customer == null)
            {
                Debug.LogError($"[RestaurantManager] Customer prefab '{selectedPrefab.name}' does not contain CustomerAI.");
                Destroy(customerGO);
                return;
            }
        }
        else if (sceneCustomer != null)
        {
            customer = sceneCustomer;
            customer.gameObject.SetActive(true);
        }
        else
        {
            Debug.LogError("[RestaurantManager] ❌ THIẾU 'Customer Prefab' hoặc 'Scene Customer'! Hãy gán GameObject Khách Hàng vào RestaurantManager.");
            return;
        }

        if (customer != null)
        {
            seat.occupied = true;
            Sprite foodToOrder = GetCustomerOrderSprite();
            customer.InitCustomer(
                doorPoint.position, 
                seat.Point.position,
                seat.Side,
                foodToOrder, 
                seat.Waypoints,
                () => OnCustomerLeft(seat)
            );

            if (foodToOrder != null)
            {
                Debug.Log($"[RestaurantManager] ✅ Khách hàng xuất hiện và muốn gọi món: {foodToOrder.name}!");
            }
            else
            {
                Debug.LogWarning("[RestaurantManager] ⚠️ Khách xuất hiện nhưng chưa gán Sprite món ăn trong RestaurantManager (Food Sprite To Order hoặc Available Food Menu).");
            }
        }
    }

    /// <summary>
    /// Called by CustomerAI when MC presses F to take order.
    /// </summary>
    public void SendOrderToChef(Sprite foodSprite)
    {
        if (chefAI == null) chefAI = FindAnyObjectByType<ChefAI>();

        if (chefAI != null)
        {
            chefAI.StartCookingOrder(foodSprite);
        }
        else
        {
            Debug.LogWarning("[RestaurantManager] ChefAI reference missing in RestaurantManager!");
        }
    }

    private SeatTarget FindAvailableSeat()
    {
        if (seats == null || seats.Length == 0)
        {
            if (tableSeatPoint == null) return null;

            return new SeatTarget
            {
                point = tableSeatPoint,
                side = CustomerAI.SeatSide.Right,
                waypoints = new Transform[0]
            };
        }

        var availableSeats = new System.Collections.Generic.List<SeatTarget>();
        foreach (SeatTarget seat in seats)
        {
            if (seat != null && !seat.occupied && seat.Point != null)
            {
                availableSeats.Add(seat);
            }
        }

        if (availableSeats.Count == 0)
        {
            return null;
        }

        return availableSeats[Random.Range(0, availableSeats.Count)];
    }

    private void OnCustomerLeft(SeatTarget seat)
    {
        if (seat != null)
        {
            seat.occupied = false;
        }

        Debug.Log("[RestaurantManager] Customer left the restaurant. A seat is available.");
    }

    private IEnumerator TimedSpawnRoutine()
    {
        while (isShopOpen)
        {
            yield return new WaitForSeconds(Mathf.Max(0.1f, customerSpawnInterval));
            SpawnCustomer();
        }
    }
}
