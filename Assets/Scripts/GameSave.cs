using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>Saves the player's persistent progress to the device.</summary>
public static class GameSave
{
    [Serializable]
    public sealed class ItemData
    {
        public int slot;
        public string name;
        public int quantity;
        public bool isFish;
    }

    [Serializable]
    private sealed class QuestData
    {
        public string id;
        public int amount;
        public QuestStatus status;
    }

    [Serializable]
    private sealed class SaveData
    {
        public int version = 1;
        public Vector3 playerPosition;
        public float health;
        public int coins;
        public int wood;
        public int fish;
        public int day;
        public float hour;
        public List<QuestData> quests = new List<QuestData>();
        public List<ItemData> items = new List<ItemData>();
    }

    private static string SavePath => Path.Combine(Application.persistentDataPath, "savegame.json");
    public static bool HasSave => File.Exists(SavePath);
    public static bool PendingLoad { get; private set; }

    public static void RequestLoad() => PendingLoad = HasSave;

    public static bool BeginNewGame()
    {
        PendingLoad = false;
        if (!HasSave) return true;

        try
        {
            string previousSave = SavePath + ".previous";
            if (File.Exists(previousSave)) File.Delete(previousSave);
            File.Move(SavePath, previousSave);
            return true;
        }
        catch (Exception error)
        {
            Debug.LogError($"[GameSave] Could not start a new game: {error}");
            return false;
        }
    }

    public static bool SaveCurrent()
    {
        GameObject player = GameObject.FindWithTag("Player");
        if (player == null) return false;
        Health health = player.GetComponent<Health>();
        if (health != null && health.IsDead) return false;

        SaveData data = new SaveData
        {
            playerPosition = player.transform.position,
            health = health != null ? health.CurrentHealth : 100f,
            coins = CoinWallet.Balance,
            wood = WoodItem.totalWoodCollected,
            fish = player.GetComponent<PlayerFishing>()?.FishCount ?? 0,
            day = DayNightClockSystem.Instance != null ? DayNightClockSystem.Instance.CurrentDay : 1,
            hour = DayNightClockSystem.Instance != null ? DayNightClockSystem.Instance.TimeOfDay : 6f
        };
        if (QuestManager.Instance != null)
            foreach (Quest quest in QuestManager.Instance.AllQuests)
                data.quests.Add(new QuestData { id = quest.id, amount = quest.currentAmount, status = quest.status });
        if (InventoryUI.Instance != null)
            data.items = InventoryUI.Instance.CaptureItems();

        try
        {
            Directory.CreateDirectory(Application.persistentDataPath);
            string temporary = SavePath + ".tmp";
            File.WriteAllText(temporary, JsonUtility.ToJson(data, true));
            if (File.Exists(SavePath)) File.Replace(temporary, SavePath, SavePath + ".bak");
            else File.Move(temporary, SavePath);
            return true;
        }
        catch (Exception error)
        {
            Debug.LogError($"[GameSave] Save failed: {error}");
            return false;
        }
    }

    public static bool LoadPending()
    {
        if (!PendingLoad) return false;
        PendingLoad = false;
        if (!HasSave) return false;

        try
        {
            SaveData data = JsonUtility.FromJson<SaveData>(File.ReadAllText(SavePath));
            if (data == null || data.version != 1) return false;
            GameObject player = GameObject.FindWithTag("Player");
            if (player == null) return false;

            Rigidbody2D body = player.GetComponent<Rigidbody2D>();
            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
                body.position = data.playerPosition;
            }
            player.transform.position = data.playerPosition;
            player.GetComponent<Health>()?.Restore(data.health);
            player.GetComponent<PlayerFishing>()?.RestoreFishCount(data.fish);
            CoinWallet.Restore(data.coins);
            WoodItem.totalWoodCollected = Mathf.Max(0, data.wood);
            DayNightClockSystem.Instance?.RestoreDayAndTime(data.day, data.hour);

            if (QuestManager.Instance != null && data.quests != null)
            {
                foreach (QuestData saved in data.quests)
                {
                    Quest quest = QuestManager.Instance.GetQuest(saved.id);
                    if (quest == null) continue;
                    quest.currentAmount = Mathf.Max(0, saved.amount);
                    quest.status = saved.status;
                }
                QuestManager.Instance.RefreshAfterLoad();
            }
            InventoryUI.Instance?.RestoreItems(data.items);
            return true;
        }
        catch (Exception error)
        {
            Debug.LogError($"[GameSave] Load failed: {error}");
            return false;
        }
    }
}
