using System;
using System.Collections.Generic;
using Mandato.Content;
using Mandato.Core;

namespace Mandato.Run
{
    [Serializable]
    public class RunState
    {
        public StatBlock stats = new StatBlock();
        public PoliticalAxis politicalAxis = new PoliticalAxis();
        public RunCalendar calendar = new RunCalendar();
        public RunTermination termination = RunTermination.Ongoing;

        public List<string> activePerkIds = new List<string>();
        public List<string> decisionHistory = new List<string>();
        public Dictionary<string, NpcRunState> npcStates = new Dictionary<string, NpcRunState>();
        public Dictionary<string, QuestRunState> questStates = new Dictionary<string, QuestRunState>();
        public List<ActiveEventState> activeEvents = new List<ActiveEventState>();
        public List<ActivePerkState> activePerks = new List<ActivePerkState>();
        public List<string> unlockedActionIds = new List<string>();
        public Dictionary<string, int> actionCooldowns = new Dictionary<string, int>();
        public List<string> consumedSingleUseActions = new List<string>();
        public int seed;

        public RunState(int seed = 0)
        {
            this.seed = seed;
            Reset();
        }

        public void Reset()
        {
            stats.ResetToDefaults();
            politicalAxis = new PoliticalAxis();
            calendar.Reset();
            termination = RunTermination.Ongoing;
            activePerkIds.Clear();
            decisionHistory.Clear();
            npcStates.Clear();
            questStates.Clear();
            activeEvents.Clear();
            activePerks.Clear();
            unlockedActionIds.Clear();
            actionCooldowns.Clear();
            consumedSingleUseActions.Clear();
        }

        public NpcRunState GetOrCreateNpcState(string npcId)
        {
            if (string.IsNullOrEmpty(npcId)) return null;

            if (!npcStates.TryGetValue(npcId, out var state))
            {
                state = new NpcRunState(npcId);
                npcStates[npcId] = state;
            }
            return state;
        }

        public int GetNpcRelation(string npcId)
        {
            if (string.IsNullOrEmpty(npcId)) return 0;
            return npcStates.TryGetValue(npcId, out var state) ? state.relationScore : 0;
        }

        public QuestRunState GetOrCreateQuestState(string questId)
        {
            if (string.IsNullOrEmpty(questId)) return null;

            if (!questStates.TryGetValue(questId, out var state))
            {
                state = new QuestRunState(questId);
                questStates[questId] = state;
            }
            return state;
        }

        public (int step, bool completed, bool failed) GetQuestState(string questId)
        {
            if (string.IsNullOrEmpty(questId) || !questStates.TryGetValue(questId, out var q))
            {
                return (0, false, false);
            }
            return (q.currentStepIndex, q.isCompleted, q.isFailed);
        }

        public bool IsQuestCompleted(string questId)
        {
            if (string.IsNullOrEmpty(questId)) return false;
            return questStates.TryGetValue(questId, out var q) && q.isCompleted;
        }

        public void TriggerEvent(string eventId, int duration = 3)
        {
            if (string.IsNullOrEmpty(eventId)) return;

            var existing = activeEvents.Find(e => string.Equals(e.eventId, eventId, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                existing.remainingMonths = duration;
            }
            else
            {
                activeEvents.Add(new ActiveEventState(eventId, duration));
            }
        }

        public void GrantPerk(string perkId, int duration = 0)
        {
            if (string.IsNullOrEmpty(perkId)) return;

            if (!activePerkIds.Contains(perkId))
            {
                activePerkIds.Add(perkId);
            }

            var existing = activePerks.Find(p => string.Equals(p.perkId, perkId, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                existing.remainingMonths = duration;
            }
            else
            {
                activePerks.Add(new ActivePerkState(perkId, duration));
            }
        }

        public void RemovePerk(string perkId)
        {
            if (string.IsNullOrEmpty(perkId)) return;

            activePerkIds.Remove(perkId);
            activePerks.RemoveAll(p => string.Equals(p.perkId, perkId, StringComparison.OrdinalIgnoreCase));
        }

        public List<string> CheckAndApplyEmergencyRescue(IReadOnlyDictionary<string, PerkDefinition> perkCatalog = null)
        {
            var rescuedPerkIds = new List<string>();
            if (activePerkIds == null || activePerkIds.Count == 0) return rescuedPerkIds;

            // Itera sobre cópia para permitir remoção segura de perks consumidos
            var activeList = new List<string>(activePerkIds);

            foreach (var perkId in activeList)
            {
                if (string.IsNullOrEmpty(perkId)) continue;

                PerkDefinition def = null;
                if (perkCatalog != null)
                {
                    perkCatalog.TryGetValue(perkId, out def);
                }

                bool isClimateRescue = (def != null && def.isEmergencyRescue && def.rescueStat == StatId.ClimaticChanges) ||
                                       string.Equals(perkId, "ReservaFlorestal", StringComparison.OrdinalIgnoreCase);

                bool isRelationsRescue = (def != null && def.isEmergencyRescue && def.rescueStat == StatId.InternationalRelations) ||
                                         string.Equals(perkId, "AliancaEUA", StringComparison.OrdinalIgnoreCase);

                bool isEconomyRescue = (def != null && def.isEmergencyRescue && def.rescueStat == StatId.Economy);
                bool isPopularityRescue = (def != null && def.isEmergencyRescue && def.rescueStat == StatId.PopularApproval);

                if (isClimateRescue && stats.climaticChanges <= StatBlock.MinValue)
                {
                    int restoreVal = (def != null && def.rescueRestoreValue > 0) ? def.rescueRestoreValue : 35;
                    stats.climaticChanges = restoreVal;
                    RemovePerk(perkId);
                    rescuedPerkIds.Add(perkId);
                }
                else if (isRelationsRescue && stats.internationalRelations <= StatBlock.MinValue)
                {
                    int restoreVal = (def != null && def.rescueRestoreValue > 0) ? def.rescueRestoreValue : 30;
                    stats.internationalRelations = restoreVal;
                    RemovePerk(perkId);
                    rescuedPerkIds.Add(perkId);
                }
                else if (isEconomyRescue && stats.economy <= StatBlock.MinValue)
                {
                    int restoreVal = (def != null && def.rescueRestoreValue > 0) ? def.rescueRestoreValue : 30;
                    stats.economy = restoreVal;
                    RemovePerk(perkId);
                    rescuedPerkIds.Add(perkId);
                }
                else if (isPopularityRescue && stats.popularApproval <= StatBlock.MinValue)
                {
                    int restoreVal = (def != null && def.rescueRestoreValue > 0) ? def.rescueRestoreValue : 30;
                    stats.popularApproval = restoreVal;
                    RemovePerk(perkId);
                    rescuedPerkIds.Add(perkId);
                }
            }

            if (rescuedPerkIds.Count > 0)
            {
                UpdateTermination();
            }

            return rescuedPerkIds;
        }

        public void ApplyStatDelta(StatId id, int delta)
        {
            if (!termination.IsOngoing) return;

            stats.ApplyDelta(id, delta);
            UpdateTermination();
        }

        public void ApplyStatImpacts(StatBlock impacts, bool hasCorruptionMods = false)
        {
            if (!termination.IsOngoing || impacts == null) return;

            stats.ApplyImpacts(impacts, hasCorruptionMods);
            UpdateTermination();
        }

        public void ApplyPoliticalDelta(int deltaX, int deltaY)
        {
            if (!termination.IsOngoing) return;

            politicalAxis.ApplyDelta(deltaX, deltaY);
        }

        public void LockPoliticalAxis() => politicalAxis.Lock();
        public void UnlockPoliticalAxis() => politicalAxis.Unlock();

        public void UnlockAction(string actionId)
        {
            if (!string.IsNullOrEmpty(actionId) && !unlockedActionIds.Contains(actionId))
            {
                unlockedActionIds.Add(actionId);
            }
        }

        public void LockAction(string actionId)
        {
            if (!string.IsNullOrEmpty(actionId))
            {
                unlockedActionIds.Remove(actionId);
            }
        }

        public bool IsActionUnlocked(string actionId)
        {
            return !string.IsNullOrEmpty(actionId) && unlockedActionIds.Contains(actionId);
        }

        public bool IsActionConsumed(string actionId)
        {
            return !string.IsNullOrEmpty(actionId) && consumedSingleUseActions.Contains(actionId);
        }

        public bool IsActionOnCooldown(string actionId)
        {
            if (string.IsNullOrEmpty(actionId)) return false;
            return actionCooldowns.TryGetValue(actionId, out int turns) && turns > 0;
        }

        public int GetActionCooldown(string actionId)
        {
            if (string.IsNullOrEmpty(actionId)) return 0;
            return actionCooldowns.TryGetValue(actionId, out int turns) ? turns : 0;
        }

        public void RecordActionUsed(string actionId, FlipPhoneCooldownType cooldownType, int cooldownTurns)
        {
            if (string.IsNullOrEmpty(actionId)) return;

            if (cooldownType == FlipPhoneCooldownType.SingleUse)
            {
                if (!consumedSingleUseActions.Contains(actionId))
                    consumedSingleUseActions.Add(actionId);
            }
            else if (cooldownType == FlipPhoneCooldownType.Turns && cooldownTurns > 0)
            {
                actionCooldowns[actionId] = cooldownTurns;
            }
        }

        public void TickActionCooldowns()
        {
            if (actionCooldowns.Count == 0) return;

            var keys = new List<string>(actionCooldowns.Keys);
            foreach (var key in keys)
            {
                if (actionCooldowns[key] > 0)
                {
                    actionCooldowns[key]--;
                    if (actionCooldowns[key] <= 0)
                    {
                        actionCooldowns.Remove(key);
                    }
                }
            }
        }

        public MonthlyEffectsReport AdvanceMonth(
            IReadOnlyDictionary<string, PerkDefinition> perkCatalog = null,
            IReadOnlyDictionary<string, RunEventDefinition> eventCatalog = null)
        {
            return MonthlyEffectsResolver.ResolveMonth(this, perkCatalog, eventCatalog);
        }

        public void ForceDefeat(string reason)
        {
            termination = RunTermination.CreateDefeat(reason);
        }

        public void ForceVictory(string reason = "Mandato Concluído com Sucesso!")
        {
            termination = RunTermination.CreateVictory(reason);
        }

        public void UpdateTermination()
        {
            termination = RunRules.Evaluate(stats, calendar);
        }

        public RunSnapshot GetSnapshot()
        {
            return new RunSnapshot(
                stats,
                politicalAxis,
                calendar.currentMonthIndex,
                calendar.DisplayDate,
                termination.IsOngoing,
                termination.IsDefeat,
                termination.IsVictory,
                termination.reason
            );
        }
    }
}
