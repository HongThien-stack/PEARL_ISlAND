using System;
using UnityEngine;

/// <summary>References the fish sprites supplied in the project's Cá folder.</summary>
[CreateAssetMenu(menuName = "Pearl Island/Fish Catalog")]
public sealed class FishCatalog : ScriptableObject
{
    [Serializable]
    public struct Fish
    {
        public string name;
        public Sprite icon;
    }

    [SerializeField] private Fish[] fish;

    public Fish RandomFish()
    {
        if (fish == null || fish.Length == 0) return default;

        int first = UnityEngine.Random.Range(0, fish.Length);
        for (int offset = 0; offset < fish.Length; offset++)
        {
            Fish candidate = fish[(first + offset) % fish.Length];
            if (!string.IsNullOrWhiteSpace(candidate.name) && candidate.icon != null)
                return candidate;
        }
        return default;
    }

    public Sprite FindIcon(string species)
    {
        if (fish == null) return null;
        foreach (Fish entry in fish)
            if (entry.name == species) return entry.icon;
        return null;
    }
}
