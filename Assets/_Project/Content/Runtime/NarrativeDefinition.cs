using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mandato.Content
{

    [Serializable]
    public class QuestStepDefinition
    {
        public int stepIndex = 0;
        public string description = string.Empty;

        [Header("Referência Direta da Carta (ScriptableObject)")]
        public CardDefinition triggerCard;

        [Header("ID Legado / Fallback")]
        public string triggerCardId = string.Empty; // ID da proposta associada a esta etapa
        public int requiredChoiceIndex = -1; // -1 se qualquer escolha avança, 0 = esq, 1 = dir

        public string GetTriggerCardId() => triggerCard != null ? (!string.IsNullOrEmpty(triggerCard.id) ? triggerCard.id : triggerCard.name) : triggerCardId ?? string.Empty;
    }

    [CreateAssetMenu(fileName = "NewQuest", menuName = "Mandato/Quest Definition")]
    public class QuestDefinition : ScriptableObject
    {
        public string id = string.Empty;

        [Header("NPC e Recompensa por Referência Direta (ScriptableObject)")]
        public NpcDefinition npc;
        public PerkDefinition rewardPerk;

        [Header("IDs Legados / Fallback")]
        public string npcId = string.Empty;
        public string rewardPerkId = string.Empty;

        public string title = string.Empty;
        [TextArea(2, 4)] public string description = string.Empty;

        public List<QuestStepDefinition> steps = new List<QuestStepDefinition>();

        public string GetNpcId() => npc != null ? (!string.IsNullOrEmpty(npc.id) ? npc.id : npc.name) : npcId ?? string.Empty;
        public string GetRewardPerkId() => rewardPerk != null ? (!string.IsNullOrEmpty(rewardPerk.id) ? rewardPerk.id : rewardPerk.name) : rewardPerkId ?? string.Empty;

        public static QuestDefinition CreateRuntimeInstance(
            string id,
            string npcId,
            string title,
            string description = "")
        {
            var quest = CreateInstance<QuestDefinition>();
            quest.id = id;
            quest.npcId = npcId;
            quest.title = title;
            quest.description = description;
            return quest;
        }
    }
}
