using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Attach this component to any NPC in the world to make them interactable with 'F'.
/// Triggers a Visual Novel dialogue sequence when approached and interacted with.
/// Can optionally assign or complete quests upon talking.
/// </summary>
public class DialogueNPC : MonoBehaviour, IInteractable
{
    [Header("NPC Profile")]
    [SerializeField] private string npcName = "Dân Làng";
    [SerializeField] private Color nameTagColor = new Color(1f, 0.85f, 0.4f, 1f);
    [SerializeField] private Sprite npcPortrait;

    [Header("Dialogue Content")]
    [TextArea(2, 4)]
    [SerializeField] private string[] dialogueLines = new string[]
    {
        "Chào cậu, ngày mới tốt lành trên hòn đảo Pearl Island xinh đẹp!",
        "Hòn đảo này có rất nhiều điều thú vị: cậu có thể chặt cây thu thập gỗ, trang bị cần câu cá, hay giúp chúng tôi dẹp bọn quái vật.",
        "Cậu có thể mở Bảng Nhiệm Vụ bất kỳ lúc nào bằng phím [Q] để xem hành trình của mình nhé!"
    };

    [Header("Quest Connection (Optional)")]
    [SerializeField] private bool giveQuestOnTalk = false;
    [SerializeField] private string questIdToGive = "";
    [SerializeField] private string questTitle = "";
    [TextArea(2, 3)]
    [SerializeField] private string questDesc = "";
    [SerializeField] private QuestType questTargetType = QuestType.CollectWood;
    [SerializeField] private int questTargetAmount = 5;

    [Header("Audio (Optional)")]
    [SerializeField] private AudioClip voiceGreeting;

    private bool hasGivenQuest = false;

    private void Awake()
    {
        // Ensure collider exists for player interaction detection

        // Ensure collider exists for player interaction detection
        Collider2D col = GetComponent<Collider2D>();
        if (col == null)
        {
            CircleCollider2D circle = gameObject.AddComponent<CircleCollider2D>();
            circle.radius = 1.2f;
            circle.isTrigger = true;
        }
    }

    public bool CanInteract(GameObject player)
    {
        return !VisualNovelDialogueUI.IsOpen;
    }

    public void Interact(GameObject player)
    {
        if (VisualNovelDialogueUI.Instance == null)
        {
            // Auto-instantiate UI if not in scene yet
            GameObject go = new GameObject("VisualNovelDialogueSystem");
            go.AddComponent<VisualNovelDialogueUI>();
        }

        // Trigger TalkToNPC quest progress if applicable
        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.AddProgress(QuestType.TalkToNPC, 1);
        }

        List<DialogueLine> lines = new List<DialogueLine>();
        for (int i = 0; i < dialogueLines.Length; i++)
        {
            DialogueLine dl = new DialogueLine(npcName, dialogueLines[i], npcPortrait);
            dl.nameColor = nameTagColor;
            if (i == 0 && voiceGreeting != null)
            {
                dl.voiceClip = voiceGreeting;
            }
            lines.Add(dl);
        }

        VisualNovelDialogueUI.Instance.ShowDialogue(lines, OnDialogueFinished);
    }

    private void OnDialogueFinished()
    {
        if (giveQuestOnTalk && !hasGivenQuest && !string.IsNullOrEmpty(questIdToGive))
        {
            hasGivenQuest = true;
            if (QuestManager.Instance != null)
            {
                Quest q = new Quest(
                    questIdToGive,
                    questTitle,
                    questDesc,
                    QuestCategory.Side,
                    questTargetType,
                    questTargetAmount
                );
                QuestManager.Instance.AddQuest(q);
            }
        }
    }
}
