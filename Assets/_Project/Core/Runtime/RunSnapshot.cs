using System;

namespace Mandato.Core
{
    [Serializable]
    public class RunSnapshot
    {
        public StatBlock Stats { get; }
        public PoliticalAxis PoliticalAxis { get; }
        public int MonthIndex { get; }
        public string DisplayDate { get; }
        public bool IsOngoing { get; }
        public bool IsDefeat { get; }
        public bool IsVictory { get; }
        public string TerminationReason { get; }

        public RunSnapshot(
            StatBlock stats,
            PoliticalAxis politicalAxis,
            int monthIndex,
            string displayDate,
            bool isOngoing,
            bool isDefeat,
            bool isVictory,
            string terminationReason)
        {
            Stats = stats?.Clone() ?? new StatBlock();
            PoliticalAxis = politicalAxis?.Clone() ?? new PoliticalAxis();
            MonthIndex = monthIndex;
            DisplayDate = displayDate ?? string.Empty;
            IsOngoing = isOngoing;
            IsDefeat = isDefeat;
            IsVictory = isVictory;
            TerminationReason = terminationReason ?? string.Empty;
        }
    }
}
