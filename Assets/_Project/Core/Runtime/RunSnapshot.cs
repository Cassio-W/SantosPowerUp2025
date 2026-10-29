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
        public WarSnapshot War { get; }

        public RunSnapshot(
            StatBlock stats = null,
            PoliticalAxis politicalAxis = null,
            int monthIndex = 0,
            string displayDate = "",
            bool isOngoing = true,
            bool isDefeat = false,
            bool isVictory = false,
            string terminationReason = "",
            WarSnapshot war = null)
        {
            War = war ?? WarSnapshot.Inactive;
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
