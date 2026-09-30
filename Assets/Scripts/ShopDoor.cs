using UnityEngine;

/// <summary>
/// Door / Sign post where MC presses 'F' to start business shift and open the restaurant.
/// </summary>
public class ShopDoor : MonoBehaviour, IInteractable
{
    [Header("Door Settings")]
    [Tooltip("Prompt UI or highlight displayed when near the door.")]
    [SerializeField] private GameObject doorPromptUI;

    public bool CanInteract(GameObject player)
    {
        if (RestaurantManager.Instance == null) return false;
        return !RestaurantManager.Instance.IsShopOpen() || !RestaurantManager.Instance.HasActiveCustomer();
    }

    public void Interact(GameObject player)
    {
        if (!CanInteract(player)) return;

        if (!RestaurantManager.Instance.IsShopOpen())
        {
            Debug.Log("[ShopDoor] MC pressed F at Door! Opening Restaurant and starting business...");
            RestaurantManager.Instance.StartBusiness();
        }
        else if (!RestaurantManager.Instance.HasActiveCustomer())
        {
            Debug.Log("[ShopDoor] MC pressed F at Door! Inviting next customer into restaurant...");
            RestaurantManager.Instance.SpawnCustomer();
        }

        if (doorPromptUI != null)
        {
            doorPromptUI.SetActive(false);
        }
    }
}
