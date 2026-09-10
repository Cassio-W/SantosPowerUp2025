using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mandato.Content
{
    [CreateAssetMenu(fileName = "NewNPC", menuName = "Mandato/NPC Definition")]
    public class NpcDefinition : ScriptableObject
    {
        public string id = string.Empty;
        public string displayName = string.Empty;
        public string role = string.Empty; // Ex: "Ministro da Fazenda", "Líder da Oposição"
        public string visualModelId = string.Empty; // ID de apresentação para carregar o modelo 3D

        [Range(-10, 10)] public int politicalBiasX = 0; // Inclinação ideológica base (-10 Esquerda a +10 Direita)
        [Range(-10, 10)] public int politicalBiasY = 0; // Inclinação de governança base (-10 Liberal a +10 Autoritário)

        [TextArea(2, 4)] public string bio = string.Empty;

        public static NpcDefinition CreateRuntimeInstance(
            string id,
            string displayName,
            string role = "",
            string visualModelId = "",
            int biasX = 0,
            int biasY = 0)
        {
            var npc = CreateInstance<NpcDefinition>();
            npc.id = id;
            npc.displayName = displayName;
            npc.role = role;
            npc.visualModelId = visualModelId;
            npc.politicalBiasX = biasX;
            npc.politicalBiasY = biasY;
            return npc;
        }
    }

    [Serializable]
    public class QuestStepDefinition
    {
        public int stepIndex = 0;
        public string description = string.Empty;
        public string triggerCardId = string.Empty; // ID da proposta associada a esta etapa
        public int requiredChoiceIndex = -1; // -1 se qualquer escolha avança, 0 = esq, 1 = dir
    }

    [CreateAssetMenu(fileName = "NewQuest", menuName = "Mandato/Quest Definition")]
    public class QuestDefinition : ScriptableObject
    {
        public string id = string.Empty;
        public string npcId = string.Empty;
        public string title = string.Empty;
        [TextArea(2, 4)] public string description = string.Empty;

        public List<QuestStepDefinition> steps = new List<QuestStepDefinition>();

        public string rewardPerkId = string.Empty;
        public string rewardEndingUnlockId = string.Empty;

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
