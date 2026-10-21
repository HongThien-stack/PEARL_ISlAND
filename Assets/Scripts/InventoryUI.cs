using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>Displays the player's equipment and collected items in a 5x5 grid.</summary>
public class InventoryUI : MonoBehaviour
{
    private const int Capacity = 25;

    [Serializable]
    public class SlotView
    {
        public Image icon;
        public Text nameLabel;
        public Text quantityLabel;
        public Image equippedHighlight;
    }

    private sealed class ItemStack
    {
        public string name;
        public Sprite icon;
        public int quantity;
    }

    [SerializeField] private GameObject panel;
    [SerializeField] private SlotView[] slots = new SlotView[Capacity];

    private readonly ItemStack[] items = new ItemStack[Capacity];
    private PlayerFishing fishing;
    private PlayerCombat combat;
    private PlayerFoodCarrier foodCarrier;
    private int lastWood = -1;
    private int lastFish = -1;
    private Sprite lastFood;

    public static InventoryUI Instance { get; private set; }
    public static bool IsOpen => Instance != null && Instance.panel != null && Instance.panel.activeSelf;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        PlayerMovement player = FindAnyObjectByType<PlayerMovement>();
        if (player != null)
        {
            fishing = player.GetComponent<PlayerFishing>();
            combat = player.GetComponent<PlayerCombat>();
            foodCarrier = player.GetComponent<PlayerFoodCarrier>();
        }

        items[0] = new ItemStack { name = "Sword", quantity = 1 };
        items[1] = new ItemStack { name = "Fishing Rod", quantity = 1 };
        items[2] = new ItemStack { name = "Axe", quantity = 1 };
        SyncExistingItems();
        RefreshAll();
        if (panel != null) panel.SetActive(false);
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.iKey.wasPressedThisFrame)
            SetOpen(!IsOpen);
        else if (IsOpen && keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            SetOpen(false);

        if (IsOpen)
        {
            SyncExistingItems();
            RefreshAll();
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void SetOpen(bool open)
    {
        if (panel == null || panel.activeSelf == open) return;
        if (open)
        {
            SyncExistingItems();
            RefreshAll();
        }
        panel.SetActive(open);
    }

    /// <summary>Add an item to a spare slot or increase an existing stack.</summary>
    public bool AddItem(string itemName, Sprite icon, int amount = 1)
    {
        if (string.IsNullOrWhiteSpace(itemName) || amount <= 0) return false;
        for (int i = 6; i < Capacity; i++)
        {
            if (items[i] == null || items[i].name != itemName) continue;
            items[i].quantity += amount;
            if (icon != null) items[i].icon = icon;
            RefreshSlot(i);
            return true;
        }
        for (int i = 6; i < Capacity; i++)
        {
            if (items[i] != null) continue;
            items[i] = new ItemStack { name = itemName, icon = icon, quantity = amount };
            RefreshSlot(i);
            return true;
        }
        return false;
    }

    private void SyncExistingItems()
    {
        int wood = WoodItem.totalWoodCollected;
        int fish = fishing != null ? fishing.FishCount : 0;
        Sprite food = foodCarrier != null && foodCarrier.IsCarrying()
            ? foodCarrier.GetCurrentFoodSprite() : null;

        if (wood != lastWood)
        {
            items[3] = wood > 0 ? new ItemStack { name = "Wood", quantity = wood } : null;
            lastWood = wood;
        }
        if (fish != lastFish)
        {
            items[4] = fish > 0 ? new ItemStack { name = "Fish", quantity = fish } : null;
            lastFish = fish;
        }
        if (food != lastFood)
        {
            items[5] = food != null ? new ItemStack { name = food.name, icon = food, quantity = 1 } : null;
            lastFood = food;
        }
    }

    private void RefreshAll()
    {
        for (int i = 0; i < Capacity; i++) RefreshSlot(i);
    }

    private void RefreshSlot(int index)
    {
        if (slots == null || index >= slots.Length || slots[index] == null) return;
        SlotView view = slots[index];
        ItemStack item = items[index];
        if (view.icon != null)
        {
            view.icon.sprite = item != null ? item.icon : null;
            view.icon.gameObject.SetActive(view.icon.sprite != null);
        }
        if (view.nameLabel != null)
            view.nameLabel.text = item != null && item.icon == null ? item.name : "";
        if (view.quantityLabel != null)
            view.quantityLabel.text = item != null && item.quantity > 1 ? item.quantity.ToString() : "";
        if (view.equippedHighlight != null)
            view.equippedHighlight.gameObject.SetActive(
                (index == 0 && combat != null && combat.IsFighting) ||
                (index == 1 && fishing != null && fishing.IsFishing));
    }

#if UNITY_EDITOR
    public void Configure(GameObject inventoryPanel, SlotView[] inventorySlots)
    {
        panel = inventoryPanel;
        slots = inventorySlots;
    }
#endif
}
