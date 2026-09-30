using UnityEngine;

/// <summary>
/// Singleton manager coordinating restaurant business flow, customer spawning, and order dispatches.
/// Supports single or multiple food item menus (e.g. Pho Bo, Grilled Pork Rice).
/// </summary>
public class RestaurantManager : MonoBehaviour
{
    public static RestaurantManager Instance { get; private set; }

    [Header("Restaurant References")]
    [Tooltip("Reference to the Chef AI in the scene.")]
    [SerializeField] private ChefAI chefAI;

    [Tooltip("Point where customer spawns and leaves (e.g. Door).")]
    [SerializeField] private Transform doorPoint;

    [Tooltip("Point where customer sits (e.g. Table / Chair).")]
    [SerializeField] private Transform tableSeatPoint;

    [Tooltip("Prefab of Customer GameObject to instantiate.")]
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
    [SerializeField] private bool hasActiveCustomer = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public bool IsShopOpen()
    {
        return isShopOpen;
    }

    public bool HasActiveCustomer()
    {
        if (hasActiveCustomer)
        {
            CustomerAI activeCust = FindAnyObjectByType<CustomerAI>();
            if (activeCust == null)
            {
                hasActiveCustomer = false;
            }
        }
        return hasActiveCustomer;
    }

    /// <summary>
    /// MC presses F at Door to open business.
    /// </summary>
    public void StartBusiness()
    {
        if (isShopOpen) return;

        isShopOpen = true;
        Debug.Log("==========================================");
        Debug.Log("[RestaurantManager] BUSINESS STARTED! Open for customers.");
        Debug.Log("==========================================");

        SpawnCustomer();
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
        if (HasActiveCustomer())
        {
            Debug.LogWarning("[RestaurantManager] ⚠️ Đã có khách hàng trong nhà hàng!");
            return;
        }

        // Auto-find references if missing
        if (chefAI == null) chefAI = FindAnyObjectByType<ChefAI>();
        if (sceneCustomer == null && customerPrefab == null) sceneCustomer = FindAnyObjectByType<CustomerAI>();

        // Check specifically what is missing and print clear debug warnings
        if (doorPoint == null)
        {
            Debug.LogError("[RestaurantManager] ❌ THIẾU 'Door Point'! Hãy kéo Transform vị trí Cửa vào ô Door Point trên RestaurantManager.");
            return;
        }

        if (tableSeatPoint == null)
        {
            Debug.LogError("[RestaurantManager] ❌ THIẾU 'Table Seat Point'! Hãy kéo Transform vị trí Bàn Ăn vào ô Table Seat Point trên RestaurantManager.");
            return;
        }

        CustomerAI customer = null;

        if (customerPrefab != null)
        {
            GameObject customerGO = Instantiate(customerPrefab, doorPoint.position, Quaternion.identity);
            customer = customerGO.GetComponent<CustomerAI>();
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
            hasActiveCustomer = true;
            Sprite foodToOrder = GetCustomerOrderSprite();

            customer.InitCustomer(
                doorPoint.position, 
                tableSeatPoint.position, 
                foodToOrder, 
                OnCustomerLeft
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

    private void OnCustomerLeft()
    {
        hasActiveCustomer = false;
        Debug.Log("[RestaurantManager] Customer left the restaurant. Next customer arriving soon!");

        if (isShopOpen)
        {
            StartCoroutine(AutoSpawnNextCustomerRoutine());
        }
    }

    private System.Collections.IEnumerator AutoSpawnNextCustomerRoutine()
    {
        yield return new WaitForSeconds(2.5f);
        if (isShopOpen && !hasActiveCustomer)
        {
            SpawnCustomer();
        }
    }
}
