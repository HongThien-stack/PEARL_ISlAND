using System;
using UnityEngine;

/// <summary>Shop art and products. The catalog asset lives in Resources for scene-independent loading.</summary>
public sealed class ShopCatalog : ScriptableObject
{
    [Serializable]
    public struct Product
    {
        public string name;
        public Sprite icon;
        [Min(0)] public int price;
    }

    public Sprite frame;
    public Sprite slot;
    public Sprite interactKey;
    public Sprite coinIcon;
    public Product[] farmSupplies;
    public Product[] items;

    public Sprite FindProductIcon(string productName)
    {
        if (string.IsNullOrEmpty(productName)) return null;
        if (farmSupplies != null)
            foreach (Product product in farmSupplies)
                if (product.name == productName) return product.icon;
        if (items != null)
            foreach (Product product in items)
                if (product.name == productName) return product.icon;
        return null;
    }
}
