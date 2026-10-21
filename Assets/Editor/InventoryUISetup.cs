using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Creates a persistent Inventory Canvas and all 25 slots in the open game scene.</summary>
public static class InventoryUISetup
{
    private const string SheetPath = "Assets/UI/relaxing-farming-game-with-light-wooden-frame3-Sheet-export.png";

    [MenuItem("Tools/Preview Inventory While Playing")]
    public static void PreviewWhilePlaying()
    {
        if (!Application.isPlaying || InventoryUI.Instance == null)
        {
            Debug.LogWarning("Enter Play Mode before previewing Inventory UI.");
            return;
        }
        InventoryUI.Instance.SetOpen(true);
    }

    [MenuItem("Tools/Rebuild Inventory UI")]
    public static void Rebuild()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != "Assets/PixelLabMap/Generated/PixelLabMap.unity")
        {
            Debug.LogError("Open PixelLabMap before rebuilding Inventory UI.");
            return;
        }

        Sprite frame = LoadSprite("_3");
        Sprite slot = LoadSprite("_1");
        if (frame == null || slot == null)
        {
            Debug.LogError("Inventory sprites _3 and _1 were not found in the UI sheet.");
            return;
        }

        InventoryUI inventory = UnityEngine.Object.FindAnyObjectByType<InventoryUI>(FindObjectsInactive.Include);
        if (inventory == null)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name != "Inventory UI") continue;
                Undo.RecordObject(root, "Retire old inventory sprites");
                root.name = "Inventory UI (old sprites)";
                root.SetActive(false);
            }

            GameObject canvasObject = new GameObject("Inventory UI", typeof(RectTransform),
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(InventoryUI));
            Undo.RegisterCreatedObjectUndo(canvasObject, "Create Inventory UI");
            inventory = canvasObject.GetComponent<InventoryUI>();
        }

        Canvas canvas = inventory.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;
        CanvasScaler scaler = inventory.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform panelRect = MakeImage(inventory.transform, "Inventory Panel", frame,
            Vector2.zero, new Vector2(370, 405), Color.white);
        MakeText(panelRect, "Inventory Title", "INVENTORY", 18, TextAnchor.MiddleCenter,
            new Vector2(0, 177), new Vector2(250, 28), new Color(1f, 0.9f, 0.67f));
        MakeText(panelRect, "Close Hint", "I / ESC", 10, TextAnchor.MiddleRight,
            new Vector2(140, 177), new Vector2(50, 25), new Color(0.84f, 0.77f, 0.62f));

        InventoryUI.SlotView[] views = new InventoryUI.SlotView[25];
        for (int i = 0; i < views.Length; i++)
        {
            int col = i % 5;
            int row = i / 5;
            RectTransform cell = MakeImage(panelRect, "Slot " + (i + 1), slot,
                new Vector2((col - 2) * 65, 117 - row * 65), new Vector2(59, 59),
                new Color(0.72f, 0.67f, 0.59f));
            Image highlight = MakeImage(cell, "Equipped", null, Vector2.zero,
                new Vector2(47, 47), new Color(0.24f, 0.79f, 0.81f, 0.63f)).GetComponent<Image>();
            highlight.gameObject.SetActive(false);
            Image icon = MakeImage(cell, "Item Icon", null, new Vector2(0, 5),
                new Vector2(36, 36), Color.white).GetComponent<Image>();
            icon.preserveAspect = true;
            icon.gameObject.SetActive(false);
            Text name = MakeText(cell, "Item Name", "", 10, TextAnchor.MiddleCenter,
                new Vector2(0, -7), new Vector2(49, 28), Color.white);
            Text count = MakeText(cell, "Quantity", "", 11, TextAnchor.LowerRight,
                new Vector2(0, -2), new Vector2(45, 48), Color.white);
            views[i] = new InventoryUI.SlotView
            {
                icon = icon,
                nameLabel = name,
                quantityLabel = count,
                equippedHighlight = highlight
            };
        }

        inventory.Configure(panelRect.gameObject, views);
        panelRect.gameObject.SetActive(false);
        EditorUtility.SetDirty(inventory);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = inventory.gameObject;
        Debug.Log("Inventory UI rebuilt in the Hierarchy with 25 saved slots.");
    }

    private static Sprite LoadSprite(string suffix)
    {
        foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(SheetPath))
            if (asset is Sprite sprite && sprite.name.EndsWith(suffix, StringComparison.Ordinal))
                return sprite;
        return null;
    }

    private static RectTransform MakeImage(Transform parent, string name, Sprite sprite,
        Vector2 position, Vector2 size, Color color)
    {
        Transform existing = parent.Find(name);
        GameObject go = existing != null
            ? existing.gameObject
            : new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        if (existing == null) go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        Image image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
        image.color = color;
        image.raycastTarget = false;
        return rect;
    }

    private static Text MakeText(Transform parent, string name, string value, int fontSize,
        TextAnchor alignment, Vector2 position, Vector2 size, Color color)
    {
        Transform existing = parent.Find(name);
        GameObject go = existing != null
            ? existing.gameObject
            : new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        if (existing == null) go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        Text text = go.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = color;
        text.text = value;
        text.raycastTarget = false;
        return text;
    }
}
