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

        public bool IsMet(StatBlock stats, int currentMonth)
        {
            if (currentMonth < minMonth || currentMonth > maxMonth)
                return false;

            if (checkStat && stats != null)
            {
                int val = stats.Get(requiredStat);
                if (val < minStatValue || val > maxStatValue)
                    return false;
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
        [Range(1, 1000)] public int baseWeight = 100;

        public ChoiceDefinition leftChoice = new ChoiceDefinition("Aceitar");
        public ChoiceDefinition rightChoice = new ChoiceDefinition("Recusar");

        public List<CardCondition> conditions = new List<CardCondition>();

        public ChoiceDefinition GetChoice(int index) => index == 0 ? leftChoice : rightChoice;

        public bool AreConditionsMet(StatBlock stats, int currentMonth)
        {
            if (conditions == null || conditions.Count == 0)
                return true;

            for (int i = 0; i < conditions.Count; i++)
            {
                if (conditions[i] != null && !conditions[i].IsMet(stats, currentMonth))
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

        public static CardDefinition CreateRuntimeInstance(
            string id,
            string title,
            string description,
            ChoiceDefinition left,
            ChoiceDefinition right,
            string npcId = "",
            string tag = "")
        {
            var card = CreateInstance<CardDefinition>();
            card.id = id;
            card.title = title;
            card.description = description;
            card.leftChoice = left ?? new ChoiceDefinition("Aceitar");
            card.rightChoice = right ?? new ChoiceDefinition("Recusar");
            card.npcId = npcId ?? string.Empty;
            card.categoryTag = tag ?? string.Empty;
            return card;
        }
    }
}
