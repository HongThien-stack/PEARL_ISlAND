using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central manager for the Quest system.
/// Handles active quests, progress tracking, objective triggers, and rewards.
/// </summary>
public class QuestManager : MonoBehaviour
{
    public static QuestManager Instance { get; private set; }

    [Header("Starter Quests")]
    [SerializeField] private bool loadDefaultQuestsOnStart = true;
    [SerializeField] private List<Quest> quests = new List<Quest>();

    public event Action<Quest> OnQuestAdded;
    public event Action<Quest> OnQuestUpdated;
    public event Action<Quest> OnQuestCompleted;
    public event Action<Quest> OnQuestClaimed;
    public event Action OnQuestListChanged;

    public IReadOnlyList<Quest> AllQuests => quests;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        if (loadDefaultQuestsOnStart && quests.Count == 0)
        {
            PopulateDefaultQuests();
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void PopulateDefaultQuests()
    {
        // 1. Chặt củi đầu mùa
        Quest q1 = new Quest(
            "quest_chop_wood",
            "Thu Thập Gỗ Xây Dựng",
            "Hãy dùng rìu để chặt những cây quanh đảo và thu thập 5 khúc gỗ để chuẩn bị dựng rào chắn bảo vệ ngôi làng.",
            QuestCategory.Main,
            QuestType.CollectWood,
            5,
            new QuestReward { gold = 150, exp = 100, itemName = "Gỗ Quý", itemAmount = 2 }
        );

        // 2. Câu cá ngoài khơi
        Quest q2 = new Quest(
            "quest_catch_fish",
            "Bữa Tối Ngoài Khơi",
            "Trang bị cần câu và câu 3 con cá tươi từ bờ biển đảo Pearl Island để mang về cho đầu bếp nhà hàng.",
            QuestCategory.Side,
            QuestType.CatchFish,
            3,
            new QuestReward { gold = 200, exp = 120, itemName = "Mồi Câu Đặc Biệt", itemAmount = 5 }
        );

        // 3. Tiêu diệt Goblin
        Quest q3 = new Quest(
            "quest_defeat_goblins",
            "Trừ Khử Đám Goblin",
            "Bọn Goblin quấy phá ngoài bìa rừng đang đe dọa người dân. Hãy tiêu diệt 2 tên Goblin để lập lại trật tự.",
            QuestCategory.Daily,
            QuestType.DefeatEnemy,
            2,
            new QuestReward { gold = 300, exp = 250, itemName = "Ngọc Biển Xanh", itemAmount = 1 }
        );

        // 4. Gặp gỡ NPC
        Quest q4 = new Quest(
            "quest_talk_npc",
            "Chào Hỏi Dân Làng",
            "Đi dạo quanh thị trấn và trò chuyện với một người dân trên đảo (bằng phím F) để nắm bắt thông tin mới nhất.",
            QuestCategory.Main,
            QuestType.TalkToNPC,
            1,
            new QuestReward { gold = 100, exp = 50 }
        );

        quests.Add(q1);
        quests.Add(q2);
        quests.Add(q3);
        quests.Add(q4);
    }

    /// <summary>
    /// Adds a new quest to the quest log.
    /// </summary>
    public void AddQuest(Quest quest)
    {
        if (quest == null || string.IsNullOrEmpty(quest.id)) return;

        Quest existing = quests.Find(q => q.id == quest.id);
        if (existing != null)
        {
            Debug.LogWarning($"[QuestManager] Nhiệm vụ {quest.id} đã tồn tại trong danh sách.");
            return;
        }

        quests.Add(quest);
        OnQuestAdded?.Invoke(quest);
        OnQuestListChanged?.Invoke();
        Debug.Log($"[QuestManager] 📜 Đã nhận nhiệm vụ mới: {quest.title}");
    }

    /// <summary>
    /// Increments progress for all active quests matching the specified target objective type.
    /// </summary>
    public void AddProgress(QuestType targetType, int amount = 1)
    {
        if (amount <= 0) return;

        bool changed = false;
        for (int i = 0; i < quests.Count; i++)
        {
            Quest q = quests[i];
            if (q.targetType == targetType && q.status == QuestStatus.InProgress)
            {
                q.currentAmount += amount;
                changed = true;

                Debug.Log($"[QuestManager] 🎯 Tiến độ nhiệm vụ [{q.title}]: {q.ProgressText}");

                if (q.IsCompleted && q.status != QuestStatus.Completed)
                {
                    q.status = QuestStatus.Completed;
                    OnQuestCompleted?.Invoke(q);
                    Debug.Log($"[QuestManager] ✨ ĐÃ HOÀN THÀNH NHIỆM VỤ: {q.title}!");
                }
                else
                {
                    OnQuestUpdated?.Invoke(q);
                }
            }
        }

        if (changed)
        {
            OnQuestListChanged?.Invoke();
        }
    }

    /// <summary>
    /// Increments progress for a specific quest by ID.
    /// </summary>
    public void AddProgress(string questId, int amount = 1)
    {
        if (string.IsNullOrEmpty(questId) || amount <= 0) return;

        Quest q = quests.Find(x => x.id == questId);
        if (q != null && q.status == QuestStatus.InProgress)
        {
            q.currentAmount += amount;
            Debug.Log($"[QuestManager] 🎯 Tiến độ nhiệm vụ [{q.title}]: {q.ProgressText}");

            if (q.IsCompleted && q.status != QuestStatus.Completed)
            {
                q.status = QuestStatus.Completed;
                OnQuestCompleted?.Invoke(q);
                Debug.Log($"[QuestManager] ✨ ĐÃ HOÀN THÀNH NHIỆM VỤ: {q.title}!");
            }
            else
            {
                OnQuestUpdated?.Invoke(q);
            }

            OnQuestListChanged?.Invoke();
        }
    }

    /// <summary>
    /// Claims the reward for a completed quest.
    /// </summary>
    public bool ClaimReward(string questId)
    {
        Quest q = quests.Find(x => x.id == questId);
        if (q == null) return false;

        if (!q.IsCompleted || q.status == QuestStatus.Claimed)
        {
            return false;
        }

        q.status = QuestStatus.Claimed;
        CoinWallet.Add(q.reward.gold);

        Debug.Log($"[QuestManager] 🎁 ĐÃ NHẬN THƯỞNG: {q.title}! (+{q.reward.gold} Vàng, +{q.reward.exp} EXP)");

        // If InventoryUI exists and quest gives an item, add to inventory
        if (InventoryUI.Instance != null && !string.IsNullOrEmpty(q.reward.itemName))
        {
            InventoryUI.Instance.AddItem(q.reward.itemName, q.reward.itemIcon, q.reward.itemAmount);
        }

        OnQuestClaimed?.Invoke(q);
        OnQuestListChanged?.Invoke();
        return true;
    }

    public Quest GetQuest(string questId)
    {
        return quests.Find(x => x.id == questId);
    }
}
