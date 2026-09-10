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
