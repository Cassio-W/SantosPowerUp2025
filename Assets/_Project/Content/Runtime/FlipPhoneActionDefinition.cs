using System;
using System.Collections.Generic;
using Mandato.Core;
using UnityEngine;

namespace Mandato.Content
{
    public enum FlipPhoneCooldownType
    {
        None,
        Turns,
        SingleUse
    }

    public enum FlipPhoneEffectType
    {
        StatImpact,
        PoliticalImpact,
        InjectCard,
        RemoveCard,
        RemoveNpcFromGame,
        GrantPerk,
        TriggerEvent,
        DismissCurrentProposal,
        SuspendNpc,
        PeekStatImpacts,
        PreventStatLoss
    }

    [Serializable]
    public class FlipPhoneEffect
    {
        public FlipPhoneEffectType effectType = FlipPhoneEffectType.StatImpact;
        public StatBlock statImpacts = new StatBlock(0, 0, 0, 0, 0);
        public int deltaPoliticalX = 0;
        public int deltaPoliticalY = 0;
        
        [Header("Alvo por Referência Direta (ScriptableObject)")]
        public CardDefinition targetCard;
        public PerkDefinition targetPerk;
        public NpcDefinition targetNpc;
        public RunEventDefinition targetEvent;

        [Header("Alvo por ID Legado / Fallback")]
        public string targetId = string.Empty; // cardId, npcId, perkId, eventId
        public int duration = 0;
        public bool injectOnTop = true;

        public string GetTargetId()
        {
            if (targetCard != null) return !string.IsNullOrEmpty(targetCard.id) ? targetCard.id : targetCard.name;
            if (targetPerk != null) return !string.IsNullOrEmpty(targetPerk.id) ? targetPerk.id : targetPerk.name;
            if (targetNpc != null) return !string.IsNullOrEmpty(targetNpc.id) ? targetNpc.id : targetNpc.name;
            if (targetEvent != null) return !string.IsNullOrEmpty(targetEvent.id) ? targetEvent.id : targetEvent.name;
            return targetId ?? string.Empty;
        }

        public FlipPhoneEffect() { }

        public static FlipPhoneEffect CreateStatImpact(StatBlock stats) => new FlipPhoneEffect { effectType = FlipPhoneEffectType.StatImpact, statImpacts = stats };
        public static FlipPhoneEffect CreatePoliticalImpact(int dx, int dy) => new FlipPhoneEffect { effectType = FlipPhoneEffectType.PoliticalImpact, deltaPoliticalX = dx, deltaPoliticalY = dy };
        public static FlipPhoneEffect CreateInjectCard(string cardId, bool onTop = true) => new FlipPhoneEffect { effectType = FlipPhoneEffectType.InjectCard, targetId = cardId, injectOnTop = onTop };
        public static FlipPhoneEffect CreateRemoveCard(string cardId) => new FlipPhoneEffect { effectType = FlipPhoneEffectType.RemoveCard, targetId = cardId };
        public static FlipPhoneEffect CreateRemoveNpc(string npcId) => new FlipPhoneEffect { effectType = FlipPhoneEffectType.RemoveNpcFromGame, targetId = npcId };
        public static FlipPhoneEffect CreateRemoveNpcFromGame(string npcId = "") => new FlipPhoneEffect { effectType = FlipPhoneEffectType.RemoveNpcFromGame, targetId = npcId };
        public static FlipPhoneEffect CreateGrantPerk(string perkId, int duration = 0) => new FlipPhoneEffect { effectType = FlipPhoneEffectType.GrantPerk, targetId = perkId, duration = duration };
        public static FlipPhoneEffect CreateTriggerEvent(string eventId, int duration = 3) => new FlipPhoneEffect { effectType = FlipPhoneEffectType.TriggerEvent, targetId = eventId, duration = duration };
        public static FlipPhoneEffect CreateDismissProposal() => new FlipPhoneEffect { effectType = FlipPhoneEffectType.DismissCurrentProposal };
        public static FlipPhoneEffect CreateSuspendNpc(int duration = 24, string npcId = "") => new FlipPhoneEffect { effectType = FlipPhoneEffectType.SuspendNpc, duration = duration, targetId = npcId };
        public static FlipPhoneEffect CreatePeekImpacts() => new FlipPhoneEffect { effectType = FlipPhoneEffectType.PeekStatImpacts };
        public static FlipPhoneEffect CreatePreventStatLoss() => new FlipPhoneEffect { effectType = FlipPhoneEffectType.PreventStatLoss };
    }

    [Serializable]
    public class FlipPhoneCondition
    {
        public int minMonth = 1;
        public int maxMonth = RunCalendar.DefaultTotalMonths;
        public bool checkStat = false;
        public StatId requiredStat = StatId.Economy;
        public int minStatValue = 0;
        public int maxStatValue = 100;

        [Header("Requisitos por Referência Direta (ScriptableObject)")]
        public PerkDefinition requiredPerk;
        public NpcDefinition requiredNpc;

        [Header("Requisitos Legados / Fallback")]
        public string requiredPerkId = string.Empty;
        public string requiredNpcPresent = string.Empty;
        public string requiredDecisionId = string.Empty;

        public string GetRequiredPerkId() => requiredPerk != null ? (!string.IsNullOrEmpty(requiredPerk.id) ? requiredPerk.id : requiredPerk.name) : requiredPerkId ?? string.Empty;
        public string GetRequiredNpcId() => requiredNpc != null ? (!string.IsNullOrEmpty(requiredNpc.id) ? requiredNpc.id : requiredNpc.name) : requiredNpcPresent ?? string.Empty;

        public bool IsMet(StatBlock stats, int currentMonth, IEnumerable<string> perks, IEnumerable<string> decisionHistory, string currentNpcId)
        {
            if (currentMonth < minMonth || currentMonth > maxMonth)
                return false;

            if (checkStat && stats != null)
            {
                int val = stats.Get(requiredStat);
                if (val < minStatValue || val > maxStatValue)
                    return false;
            }

            string perkToMatch = GetRequiredPerkId();
            if (!string.IsNullOrEmpty(perkToMatch))
            {
                bool hasPerk = false;
                if (perks != null)
                {
                    foreach (var p in perks)
                    {
                        if (string.Equals(p, perkToMatch, StringComparison.OrdinalIgnoreCase))
                        {
                            hasPerk = true;
                            break;
                        }
                    }
                }
                if (!hasPerk) return false;
            }

            string npcToMatch = GetRequiredNpcId();
            if (!string.IsNullOrEmpty(npcToMatch))
            {
                if (!string.Equals(currentNpcId, npcToMatch, StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            if (!string.IsNullOrEmpty(requiredDecisionId))
            {
                bool hasDecision = false;
                if (decisionHistory != null)
                {
                    foreach (var d in decisionHistory)
                    {
                        if (string.Equals(d, requiredDecisionId, StringComparison.OrdinalIgnoreCase))
                        {
                            hasDecision = true;
                            break;
                        }
                    }
                }
                if (!hasDecision) return false;
            }

            return true;
        }
    }

    [CreateAssetMenu(fileName = "NewFlipPhoneAction", menuName = "Mandato/Flip-Phone Action")]
    public class FlipPhoneActionDefinition : ScriptableObject
    {
        public string id = string.Empty;
        public string displayName = string.Empty;
        [TextArea(2, 5)] public string description = string.Empty;
        public Sprite icon;
        public string categoryTag = "Ações"; // ex: "Contatos", "Gabinete", "Especiais"

        [Header("Cooldown & Usabilidade")]
        public FlipPhoneCooldownType cooldownType = FlipPhoneCooldownType.None;
        public int cooldownTurns = 2;
        public bool unlockByDefault = true;

        [Header("Condições de Desbloqueio e Uso")]
        public List<FlipPhoneCondition> conditions = new List<FlipPhoneCondition>();

        [Header("Efeitos")]
        public List<FlipPhoneEffect> effects = new List<FlipPhoneEffect>();

        public bool AreConditionsMet(StatBlock stats, int currentMonth, IEnumerable<string> perks, IEnumerable<string> decisionHistory, string currentNpcId)
        {
            if (conditions == null || conditions.Count == 0) return true;

            for (int i = 0; i < conditions.Count; i++)
            {
                if (conditions[i] != null && !conditions[i].IsMet(stats, currentMonth, perks, decisionHistory, currentNpcId))
                    return false;
            }
            return true;
        }

        public static FlipPhoneActionDefinition CreateRuntimeInstance(
            string id,
            string displayName,
            string description,
            FlipPhoneCooldownType cooldownType = FlipPhoneCooldownType.None,
            int cooldownTurns = 0,
            bool unlockByDefault = true)
        {
            var action = CreateInstance<FlipPhoneActionDefinition>();
            action.id = id;
            action.displayName = displayName;
            action.description = description;
            action.cooldownType = cooldownType;
            action.cooldownTurns = cooldownTurns;
            action.unlockByDefault = unlockByDefault;
            return action;
        }
    }
}
