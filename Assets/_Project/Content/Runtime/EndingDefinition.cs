using System;
using UnityEngine;

namespace Mandato.Content
{
    [CreateAssetMenu(fileName = "NewEnding", menuName = "Mandato/Ending Definition")]
    public class EndingDefinition : ScriptableObject
    {
        public string id = string.Empty;
        public string title = string.Empty;
        [TextArea(3, 6)] public string epilogueText = string.Empty;
        public Sprite icon;

        public bool requiredVictory = true; // True para finais de 4 anos, False para finais de derrota prematura
        public string requiredQuadrant = string.Empty; // Ex: "Esquerda Autoritária", "Direita Liberal", "Centro" (ou vazio)

        public int minEconomy = -1;
        public int minClimate = -1;
        public int minPopularApproval = -1;
        public int minCorruption = -1;
        public string requiredCompletedQuestId = string.Empty;

        public int priority = 0; // Finais mais específicos têm prioridade maior

        public static EndingDefinition CreateRuntimeInstance(
            string id,
            string title,
            string epilogue,
            bool victory = true,
            string quadrant = "",
            int priority = 0)
        {
            var ending = CreateInstance<EndingDefinition>();
            ending.id = id;
            ending.title = title;
            ending.epilogueText = epilogue;
            ending.requiredVictory = victory;
            ending.requiredQuadrant = quadrant;
            ending.priority = priority;
            return ending;
        }
    }
}
