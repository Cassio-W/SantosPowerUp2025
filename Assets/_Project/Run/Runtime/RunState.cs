using System;
using System.Collections.Generic;
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

        public void TriggerEvent(string eventId, int duration = 3)
        {
            if (string.IsNullOrEmpty(eventId)) return;

            var existing = activeEvents.Find(e => e.eventId == eventId);
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

            var existing = activePerks.Find(p => p.perkId == perkId);
            if (existing != null)
            {
                existing.remainingMonths = duration;
            }
            else
            {
                activePerks.Add(new ActivePerkState(perkId, duration));
            }
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

        public void AdvanceMonth()
        {
            if (!termination.IsOngoing) return;

            // Atualiza e remove eventos expirados
            for (int i = activeEvents.Count - 1; i >= 0; i--)
            {
                activeEvents[i].TickMonth();
                if (activeEvents[i].IsExpired)
                {
                    activeEvents.RemoveAt(i);
                }
            }

            // Atualiza perks temporários
            for (int i = activePerks.Count - 1; i >= 0; i--)
            {
                if (activePerks[i].remainingMonths > 0)
                {
                    activePerks[i].TickMonth();
                    if (activePerks[i].remainingMonths == 0)
                    {
                        activePerkIds.Remove(activePerks[i].perkId);
                        activePerks.RemoveAt(i);
                    }
                }
            }

            calendar.Advance();
            UpdateTermination();
        }

        public void ForceDefeat(string reason)
        {
            termination = RunTermination.CreateDefeat(reason);
        }

        public void ForceVictory(string reason = "Mandato Concluído com Sucesso!")
        {
            termination = RunTermination.CreateVictory(reason);
        }

        private void UpdateTermination()
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
