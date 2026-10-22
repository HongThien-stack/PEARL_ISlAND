using System;
using UnityEngine;

/// <summary>
/// Defines categories for organizing quests.
/// </summary>
public enum QuestCategory
{
    Main,
    Side,
    Daily
}

/// <summary>
/// Types of target objectives that trigger quest progress.
/// </summary>
public enum QuestType
{
    ChopWood,
    CollectWood,
    CatchFish,
    DefeatEnemy,
    TalkToNPC,
    Custom
}

/// <summary>
/// Status of a quest in the player's quest log.
/// </summary>
public enum QuestStatus
{
    NotStarted,
    InProgress,
    Completed,
    Claimed
}

/// <summary>
/// Rewards granted upon completing and claiming a quest.
/// </summary>
[Serializable]
public class QuestReward
{
    public int gold = 100;
    public int exp = 50;
    public string itemName = "";
    public int itemAmount = 1;
    public Sprite itemIcon;
}

/// <summary>
/// Data structure representing a single quest.
/// </summary>
[Serializable]
public class Quest
{
    public string id;
    public string title;
    [TextArea(2, 5)]
    public string description;
    public QuestCategory category = QuestCategory.Main;
    public QuestType targetType = QuestType.Custom;
    public int requiredAmount = 1;
    public int currentAmount = 0;
    public QuestStatus status = QuestStatus.InProgress;
    public QuestReward reward = new QuestReward();
    public Sprite icon;

    public bool IsCompleted => currentAmount >= requiredAmount;
    public bool IsClaimed => status == QuestStatus.Claimed;
    public float ProgressNormalized => requiredAmount > 0 ? Mathf.Clamp01((float)currentAmount / requiredAmount) : 0f;
    public string ProgressText => $"{Mathf.Min(currentAmount, requiredAmount)}/{requiredAmount}";

    public Quest() { }

    public Quest(string id, string title, string description, QuestCategory category, QuestType targetType, int requiredAmount, QuestReward reward = null)
    {
        this.id = id;
        this.title = title;
        this.description = description;
        this.category = category;
        this.targetType = targetType;
        this.requiredAmount = requiredAmount;
        this.currentAmount = 0;
        this.status = QuestStatus.InProgress;
        this.reward = reward ?? new QuestReward();
    }
}
