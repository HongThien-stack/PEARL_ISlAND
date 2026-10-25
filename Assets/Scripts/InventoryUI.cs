using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>Displays the player's equipment and collected items in a 5x5 grid.</summary>
public class InventoryUI : MonoBehaviour
{
    private const int Capacity = 25;
    private const int MaxWoodPerSlot = 30;
    private const int MaxFishPerSlot = 20;

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
        public bool isFish;
    }

    [SerializeField] private GameObject panel;
    [SerializeField] private SlotView[] slots = new SlotView[Capacity];
    [SerializeField] private Sprite swordIcon;
    [SerializeField] private Sprite fishingRodIcon;
    [SerializeField] private Sprite axeIcon;
    [SerializeField] private Sprite woodIcon;

    private readonly ItemStack[] items = new ItemStack[Capacity];
    private PlayerFishing fishing;
    private PlayerCombat combat;
    private PlayerFoodCarrier foodCarrier;
    private int lastWood = -1;
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

        items[0] = new ItemStack { name = "Sword", icon = swordIcon, quantity = 1 };
        items[1] = new ItemStack { name = "Fishing Rod", icon = fishingRodIcon, quantity = 1 };
        items[2] = new ItemStack { name = "Axe", icon = axeIcon, quantity = 1 };
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
        if (string.Equals(itemName, "Wood", StringComparison.OrdinalIgnoreCase))
            return TryAddWood(amount, icon);
        for (int i = 6; i < Capacity; i++)
        {
            if (items[i] == null || items[i].isFish || items[i].name != itemName) continue;
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

    /// <summary>Add fish to a stack of its own species, with at most 20 fish per slot.</summary>
    public bool TryAddFish(string species, Sprite icon, int amount = 1)
    {
        if (string.IsNullOrWhiteSpace(species) || icon == null || amount <= 0) return false;

        int available = 0;
        for (int i = 4; i < Capacity; i++)
        {
            if (i == 5) continue; // Food being carried has its own slot.
            ItemStack stack = items[i];
            if (stack == null) available += MaxFishPerSlot;
            else if (stack.isFish && stack.name == species)
                available += MaxFishPerSlot - stack.quantity;
        }
        if (available < amount) return false;

        int remaining = amount;
        for (int i = 4; i < Capacity && remaining > 0; i++)
        {
            if (i == 5) continue;
            ItemStack stack = items[i];
            if (stack == null || !stack.isFish || stack.name != species || stack.quantity >= MaxFishPerSlot)
                continue;
            int added = Mathf.Min(remaining, MaxFishPerSlot - stack.quantity);
            stack.quantity += added;
            remaining -= added;
            RefreshSlot(i);
        }
        for (int i = 4; i < Capacity && remaining > 0; i++)
        {
            if (i == 5 || items[i] != null) continue;
            int added = Mathf.Min(remaining, MaxFishPerSlot);
            items[i] = new ItemStack { name = species, icon = icon, quantity = added, isFish = true };
            remaining -= added;
            RefreshSlot(i);
        }
        return true;
    }

    /// <summary>Collect wood only when every piece fits in the inventory.</summary>
    public bool TryAddWood(int amount, Sprite icon = null)
    {
        if (amount <= 0) return false;

        int availableSlots = 1; // Slot 4 is reserved for the first wood stack.
        for (int i = 6; i < Capacity; i++)
            if (items[i] == null || items[i].name == "Wood") availableSlots++;

        if ((long)WoodItem.totalWoodCollected + amount > (long)availableSlots * MaxWoodPerSlot)
            return false;

        if (icon != null) woodIcon = icon;
        WoodItem.totalWoodCollected += amount;
        SyncExistingItems();
        RefreshAll();
        return true;
    }

    private void SyncExistingItems()
    {
        int wood = WoodItem.totalWoodCollected;
        Sprite food = foodCarrier != null && foodCarrier.IsCarrying()
            ? foodCarrier.GetCurrentFoodSprite() : null;

        if (wood != lastWood || (wood > 0 && items[3] != null && items[3].icon != woodIcon))
        {
            items[3] = null;
            for (int i = 6; i < Capacity; i++)
                if (items[i] != null && items[i].name == "Wood") items[i] = null;

            if (wood > 0)
            {
                items[3] = new ItemStack
                {
                    name = "Wood", icon = woodIcon, quantity = Mathf.Min(wood, MaxWoodPerSlot)
                };
                int remaining = wood - items[3].quantity;
                for (int i = 6; i < Capacity && remaining > 0; i++)
                {
                    if (items[i] != null) continue;
                    int count = Mathf.Min(remaining, MaxWoodPerSlot);
                    items[i] = new ItemStack { name = "Wood", icon = woodIcon, quantity = count };
                    remaining -= count;
                }
            }
            lastWood = wood;
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
