using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Mandato.Core;
using UnityEngine;

namespace Mandato.Content
{
    [Serializable]
    public class ChoiceDefinition
    {
        public string label = "Confirmar";
        public StatBlock statImpacts = new StatBlock(0, 0, 0, 0, 0);
        public int deltaPoliticalX = 0;
        public int deltaPoliticalY = 0;
        public bool hasCorruptionMods = false;

        public List<string> injectCardIds = new List<string>();
        public List<string> removeCardIds = new List<string>();
        public string grantPerkId = string.Empty;
        public string presentationCue = string.Empty;

        public ChoiceDefinition() { }

        public ChoiceDefinition(string label)
        {
            this.label = label ?? string.Empty;
            this.statImpacts = new StatBlock(0, 0, 0, 0, 0);
        }

        public ChoiceDefinition(string label, StatBlock impacts, bool corruptionMods = false)
        {
            this.label = label ?? string.Empty;
            this.statImpacts = impacts ?? new StatBlock(0, 0, 0, 0, 0);
            this.hasCorruptionMods = corruptionMods;
        }
    }

    [Serializable]
    public class CardCondition
    {
        public int minMonth = 1;
        public int maxMonth = RunCalendar.DefaultTotalMonths;
        public bool checkStat = false;
        public StatId requiredStat = StatId.Economy;
        public int minStatValue = 0;
        public int maxStatValue = 100;
        public string requiredPerkId = string.Empty;

        // Condições Narrativas & Quests
        public string requiredQuestId = string.Empty;
        public int requiredQuestStepIndex = -1; // -1 se qualquer etapa ativa
        public bool requireQuestCompleted = false;

        // Condições de Afinidade com NPC
        public string targetNpcId = string.Empty;
        public bool checkNpcRelation = false;
        public int minNpcRelation = -100;
        public int maxNpcRelation = 100;

        // Condições de Eixo Político
        public string requiredPoliticalQuadrant = string.Empty;
        public bool checkPoliticalRange = false;
        public int minPoliticalX = -10;
        public int maxPoliticalX = 10;
        public int minPoliticalY = -10;
        public int maxPoliticalY = 10;

        public bool IsMet(
            StatBlock stats,
            int currentMonth,
            IEnumerable<string> activePerkIds = null,
            PoliticalAxis politicalAxis = null,
            Func<string, int> getNpcRelation = null,
            Func<string, (int step, bool completed, bool failed)> getQuestState = null)
        {
            if (currentMonth < minMonth || currentMonth > maxMonth)
                return false;

            if (checkStat && stats != null)
            {
                int val = stats.Get(requiredStat);
                if (val < minStatValue || val > maxStatValue)
                    return false;
            }

            if (!string.IsNullOrEmpty(requiredPerkId))
            {
                bool hasPerk = false;
                if (activePerkIds != null)
                {
                    foreach (var p in activePerkIds)
                    {
                        if (string.Equals(p, requiredPerkId, StringComparison.OrdinalIgnoreCase))
                        {
                            hasPerk = true;
                            break;
                        }
                    }
                }
                if (!hasPerk) return false;
            }

            // Validação de Eixo Político
            if (politicalAxis != null)
            {
                if (!string.IsNullOrEmpty(requiredPoliticalQuadrant))
                {
                    if (!string.Equals(politicalAxis.Quadrant, requiredPoliticalQuadrant, StringComparison.OrdinalIgnoreCase))
                        return false;
                }

                if (checkPoliticalRange)
                {
                    if (!politicalAxis.IsInRange(minPoliticalX, maxPoliticalX, minPoliticalY, maxPoliticalY))
                        return false;
                }
            }

            // Validação de Relação com NPC
            if (checkNpcRelation && !string.IsNullOrEmpty(targetNpcId) && getNpcRelation != null)
            {
                int rel = getNpcRelation(targetNpcId);
                if (rel < minNpcRelation || rel > maxNpcRelation)
                    return false;
            }

            // Validação de Quest
            if (!string.IsNullOrEmpty(requiredQuestId) && getQuestState != null)
            {
                var qState = getQuestState(requiredQuestId);
                if (requireQuestCompleted && !qState.completed)
                    return false;

                if (!requireQuestCompleted)
                {
                    if (qState.failed) return false;
                    if (requiredQuestStepIndex >= 0 && qState.step != requiredQuestStepIndex)
                        return false;
                }
            }

            return true;
        }
    }

    [CreateAssetMenu(fileName = "NewCard", menuName = "Mandato/Card Definition")]
    public class CardDefinition : ScriptableObject
    {
        public string id = string.Empty;
        public string title = string.Empty;
        [TextArea(3, 8)] public string description = string.Empty;

        public string npcId = string.Empty;
        public GameObject npcPrefab;
        public ScriptableObject sourceLegacyAsset;
        public string categoryTag = string.Empty;
        public bool isTutorial = false;
        [Range(1, 1000)] public int baseWeight = 100;

        public ChoiceDefinition leftChoice = new ChoiceDefinition("Aceitar");
        public ChoiceDefinition rightChoice = new ChoiceDefinition("Recusar");

        public List<CardCondition> conditions = new List<CardCondition>();

        public ChoiceDefinition GetChoice(int index) => index == 0 ? leftChoice : rightChoice;

        public bool AreConditionsMet(
            StatBlock stats,
            int currentMonth,
            IEnumerable<string> activePerkIds = null,
            PoliticalAxis politicalAxis = null,
            Func<string, int> getNpcRelation = null,
            Func<string, (int step, bool completed, bool failed)> getQuestState = null)
        {
            if (conditions == null || conditions.Count == 0)
                return true;

            for (int i = 0; i < conditions.Count; i++)
            {
                if (conditions[i] != null && !conditions[i].IsMet(stats, currentMonth, activePerkIds, politicalAxis, getNpcRelation, getQuestState))
                    return false;
            }
            return true;
        }

        public string FormattedDescription => FormatSentenceBreaks(description);

        private static readonly HashSet<string> Abbreviations = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "sr", "sra", "dr", "dra", "etc", "ex", "art", "nº", "gov"
        };

        private static readonly Regex SentenceBreakRegex = new Regex(@"(?<!\.)\.(?![\.\d])([""''”’]?)\s*(?=\S)", RegexOptions.Compiled);

        public static string FormatSentenceBreaks(string text)
        {
            if (string.IsNullOrEmpty(text)) return text ?? string.Empty;

            return SentenceBreakRegex.Replace(text, match =>
            {
                int start = match.Index;
                string prefix = text.Substring(0, start);
                var wordMatch = Regex.Match(prefix, @"([a-zA-ZáéíóúÁÉÍÓÚãõÃÕâêôÂÊÔçÇ]+)$");
                if (wordMatch.Success && Abbreviations.Contains(wordMatch.Value))
                {
                    return match.Value;
                }

                string quote = match.Groups[1].Value;
                return "." + quote + "\n";
            }).TrimEnd('\r', '\n');
        }

        public const string NeutralRoutineCardId = "card_routine_dispatch";

        public static CardDefinition CreateNeutralRoutineCard()
        {
            return CreateRuntimeInstance(
                id: NeutralRoutineCardId,
                title: "Despacho de Rotina",
                description: "Nenhuma proposta extraordinária aguarda decisão este mês. O expediente segue a rotina administrativa normal.",
                left: new ChoiceDefinition("Despachar"),
                right: new ChoiceDefinition("Arquivar"),
                npcId: string.Empty,
                tag: "Rotina",
                isTutorial: false
            );
        }

        public static CardDefinition CreateRuntimeInstance(
            string id,
            string title,
            string description,
            ChoiceDefinition left,
            ChoiceDefinition right,
            string npcId = "",
            string tag = "",
            bool isTutorial = false)
        {
            var card = CreateInstance<CardDefinition>();
            card.id = id;
            card.title = title;
            card.description = description;
            card.leftChoice = left ?? new ChoiceDefinition("Aceitar");
            card.rightChoice = right ?? new ChoiceDefinition("Recusar");
            card.npcId = npcId ?? string.Empty;
            card.categoryTag = tag ?? string.Empty;
            card.isTutorial = isTutorial;
            return card;
        }
    }
}
