using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>Shop sign, proximity prompt, and two-page product catalog for the village house.</summary>
public sealed class ShopUI : MonoBehaviour
{
    private const float InteractRadius = 2.3f;
    private static readonly Color Cream = new Color(1f, .91f, .7f);
    private static readonly Color Brown = new Color(.34f, .19f, .11f);
    private Transform player;
    private SpriteRenderer house;
    private ShopCatalog catalog;
    private Camera worldCamera;
    private Canvas canvas;
    private RectTransform sign;
    private RectTransform prompt;
    private GameObject panel;
    private RectTransform products;
    private Button suppliesTab;
    private Button itemsTab;
    private Text balanceLabel;
    private Text feedbackLabel;
    private bool showItems;

    public bool IsOpen => panel != null && panel.activeSelf;

    public void Configure(Transform playerTransform, SpriteRenderer houseRenderer, ShopCatalog shopCatalog)
    {
        player = playerTransform;
        house = houseRenderer;
        catalog = shopCatalog;
        Build();
        CoinWallet.BalanceChanged -= UpdateBalance;
        CoinWallet.BalanceChanged += UpdateBalance;
        UpdateBalance(CoinWallet.Balance);
    }

    private void OnDestroy() => CoinWallet.BalanceChanged -= UpdateBalance;

    private void Update()
    {
        if (player == null)
        {
            GameObject found = GameObject.FindWithTag("Player");
            if (found != null) player = found.transform;
        }
        if (house == null || canvas == null)
        {
            if (sign != null) sign.gameObject.SetActive(false);
            if (prompt != null) prompt.gameObject.SetActive(false);
            return;
        }
        if (player == null) return;
        // The village sprite is hidden while the restaurant interior is active. Its
        // Renderer.bounds is then empty, so use the sprite's transformed bounds.
        Bounds spriteBounds = house.sprite.bounds;
        Vector3 worldMin = house.transform.TransformPoint(spriteBounds.min);
        Vector3 worldMax = house.transform.TransformPoint(spriteBounds.max);
        Bounds bounds = new Bounds((worldMin + worldMax) * .5f,
            new Vector3(Mathf.Abs(worldMax.x - worldMin.x), Mathf.Abs(worldMax.y - worldMin.y), 0f));
        Vector3 entrance = new Vector3(bounds.center.x, bounds.min.y + .9f, 0f);
        bool near = Vector2.Distance(player.position, entrance) <= InteractRadius;
        bool open = panel.activeSelf;
        FollowWorld(sign, new Vector3(bounds.center.x, bounds.max.y + .7f, 0f));
        FollowWorld(prompt, entrance + Vector3.up * .8f);
        prompt.gameObject.SetActive(near && !open);

        Keyboard keys = Keyboard.current;
        if (keys == null) return;
        if (open && (keys.escapeKey.wasPressedThisFrame || keys.eKey.wasPressedThisFrame)) SetOpen(false);
        else if (!open && near && keys.eKey.wasPressedThisFrame) SetOpen(true);
    }

    private void FollowWorld(RectTransform rect, Vector3 world)
    {
        if (worldCamera == null)
        {
            worldCamera = Camera.main;
            if (worldCamera == null) worldCamera = FindAnyObjectByType<Camera>();
        }
        Camera camera = worldCamera;
        if (camera == null) { rect.gameObject.SetActive(false); return; }
        Vector3 screen = camera.WorldToScreenPoint(world);
        if (screen.z <= 0f) { rect.gameObject.SetActive(false); return; }
        RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)canvas.transform,
            screen, null, out Vector2 local);
        rect.anchoredPosition = local;
        if (rect == sign) rect.gameObject.SetActive(true);
    }

    private void Build()
    {
        GameObject canvasObject = new GameObject("Shop UI", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 550;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.matchWidthOrHeight = .5f;

        sign = MakeRect(canvas.transform, "Shop Sign", new Vector2(180, 70), Vector2.zero);
        MakeText(sign, "SHOP", 24, new Vector2(0, 13), new Vector2(160, 32), Cream);
        MakeText(sign, "▼", 24, new Vector2(0, -16), new Vector2(70, 30), Cream);
        prompt = MakeRect(canvas.transform, "E Prompt", new Vector2(48, 48), Vector2.zero);
        MakeImage(prompt, "E Key", catalog.interactKey, Vector2.zero, new Vector2(42, 42), Color.white);
        prompt.gameObject.SetActive(false);

        panel = MakeRect(canvas.transform, "Shop Panel", new Vector2(680, 545), Vector2.zero).gameObject;
        MakeImage(panel.transform, "Wood Frame", catalog.frame, Vector2.zero, new Vector2(680, 545), Color.white);
        MakeText(panel.transform, "SHOP", 27, new Vector2(0, 235), new Vector2(350, 38), Cream);
        balanceLabel = MakeText(panel.transform, "", 18, new Vector2(198, 235), new Vector2(160, 32), Cream);
        suppliesTab = MakeButton(panel.transform, "FARM SUPPLIES", new Vector2(-145, 183), new Vector2(215, 43), () => ShowTab(false));
        itemsTab = MakeButton(panel.transform, "ITEMS", new Vector2(145, 183), new Vector2(215, 43), () => ShowTab(true));
        MakeButton(panel.transform, "X", new Vector2(300, 235), new Vector2(38, 38), () => SetOpen(false));
        products = MakeRect(panel.transform, "Products", new Vector2(605, 400), new Vector2(0, -45));
        feedbackLabel = MakeText(panel.transform, "", 15, new Vector2(0, -247), new Vector2(580, 27), Cream);
        ShowTab(false);
        panel.SetActive(false);
    }

    private void ShowTab(bool items)
    {
        showItems = items;
        if (feedbackLabel != null) feedbackLabel.text = "Click an item to buy";
        suppliesTab.image.color = items ? new Color(.54f, .32f, .19f) : new Color(.84f, .59f, .32f);
        itemsTab.image.color = items ? new Color(.84f, .59f, .32f) : new Color(.54f, .32f, .19f);
        for (int i = products.childCount - 1; i >= 0; i--)
        {
            GameObject oldCell = products.GetChild(i).gameObject;
            oldCell.SetActive(false);
            Destroy(oldCell);
        }
        ShopCatalog.Product[] list = items ? catalog.items : catalog.farmSupplies;
        if (list == null) return;
        for (int i = 0; i < list.Length; i++)
        {
            ShopCatalog.Product product = list[i];
            int col = i % 5;
            int row = i / 5;
            RectTransform cell = MakeRect(products, "Product " + product.name, new Vector2(108, 115),
                new Vector2((col - 2) * 119, 125 - row * 122));
            Image slot = MakeImage(cell, "Slot", catalog.slot, Vector2.zero, new Vector2(108, 108), new Color(.85f, .77f, .64f));
            slot.raycastTarget = true;
            Button buyButton = cell.gameObject.AddComponent<Button>();
            buyButton.targetGraphic = slot;
            buyButton.onClick.AddListener(() => Buy(product));
            Image icon = MakeImage(cell, "Icon", product.icon, new Vector2(0, 21), new Vector2(61, 61), Color.white);
            icon.preserveAspect = true;
            MakeText(cell, product.name, 12, new Vector2(0, -28), new Vector2(101, 24), Cream);
            MakeText(cell, product.price.ToString("N0"), 12, new Vector2(9, -48),
                new Vector2(72, 18), Cream);
            Image coin = MakeImage(cell, "Price Coin", catalog.coinIcon, new Vector2(-30, -48),
                new Vector2(16, 16), Color.white);
            coin.preserveAspect = true;
        }
    }

    private void Buy(ShopCatalog.Product product)
    {
        InventoryUI inventory = InventoryUI.Instance;
        if (inventory == null || !inventory.CanAddItem(product.name))
        {
            feedbackLabel.text = "Inventory is full";
            return;
        }
        if (!CoinWallet.TrySpend(product.price))
        {
            feedbackLabel.text = "Not enough coins";
            return;
        }
        if (!inventory.AddItem(product.name, product.icon))
        {
            CoinWallet.Add(product.price);
            feedbackLabel.text = "Could not add item";
            return;
        }
        feedbackLabel.text = "Bought " + product.name + "!";
    }

    private void UpdateBalance(int balance)
    {
        if (balanceLabel != null) balanceLabel.text = balance.ToString("N0") + " COINS";
    }

    public void SetOpen(bool open)
    {
        if (panel == null) return;
        if (open && InventoryUI.IsOpen) InventoryUI.Instance.SetOpen(false);
        panel.SetActive(open);
        if (open) prompt.gameObject.SetActive(false);
    }

    private static RectTransform MakeRect(Transform parent, string name, Vector2 size, Vector2 pos)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = pos;
        return rect;
    }

    private static Image MakeImage(Transform parent, string name, Sprite sprite, Vector2 pos, Vector2 size, Color tint)
    {
        RectTransform rect = MakeRect(parent, name, size, pos);
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.type = sprite != null && (name == "Wood Frame" || name == "Slot")
            ? Image.Type.Sliced : Image.Type.Simple;
        image.color = tint;
        image.raycastTarget = false;
        return image;
    }

    private static Text MakeText(Transform parent, string value, int size, Vector2 pos, Vector2 bounds, Color color)
    {
        RectTransform rect = MakeRect(parent, value, bounds, pos);
        Text text = rect.gameObject.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = size;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = color;
        text.text = value;
        text.raycastTarget = false;
        return text;
    }

    private static Button MakeButton(Transform parent, string label, Vector2 pos, Vector2 size, UnityEngine.Events.UnityAction click)
    {
        RectTransform rect = MakeRect(parent, label + " Button", size, pos);
        Image background = rect.gameObject.AddComponent<Image>();
        background.color = new Color(.63f, .42f, .23f);
        Button button = rect.gameObject.AddComponent<Button>();
        button.onClick.AddListener(click);
        MakeText(rect, label, label == "X" ? 22 : 17, Vector2.zero, size, Cream);
        return button;
    }
}
