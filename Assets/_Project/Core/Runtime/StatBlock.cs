using System;
using UnityEngine;

namespace Mandato.Core
{
    public enum StatId
    {
        ClimaticChanges,
        InternationalRelations,
        PopularApproval,
        Economy,
        Corruption
    }

    [Serializable]
    public class StatBlock
    {
        public const int MinValue = 0;
        public const int MaxValue = 100;
        public const int DefaultInitialValue = 50;
        public const int DefaultInitialCorruption = 0;

        [Range(MinValue, MaxValue)]
        public int climaticChanges = DefaultInitialValue;

        [Range(MinValue, MaxValue)]
        public int internationalRelations = DefaultInitialValue;

        [Range(MinValue, MaxValue)]
        public int popularApproval = DefaultInitialValue;

        [Range(MinValue, MaxValue)]
        public int economy = DefaultInitialValue;

        [Range(MinValue, MaxValue)]
        public int corruption = DefaultInitialCorruption;

        public StatBlock()
        {
            ResetToDefaults();
        }

        public StatBlock(int climate, int relations, int approval, int eco, int corrupt)
        {
            climaticChanges = Clamp(climate);
            internationalRelations = Clamp(relations);
            popularApproval = Clamp(approval);
            economy = Clamp(eco);
            corruption = Clamp(corrupt);
        }

        public void ResetToDefaults()
        {
            climaticChanges = DefaultInitialValue;
            internationalRelations = DefaultInitialValue;
            popularApproval = DefaultInitialValue;
            economy = DefaultInitialValue;
            corruption = DefaultInitialCorruption;
        }

        public int Get(StatId id)
        {
            return id switch
            {
                StatId.ClimaticChanges => climaticChanges,
                StatId.InternationalRelations => internationalRelations,
                StatId.PopularApproval => popularApproval,
                StatId.Economy => economy,
                StatId.Corruption => corruption,
                _ => 0
            };
        }

        public void Set(StatId id, int value)
        {
            int clamped = Clamp(value);
            switch (id)
            {
                case StatId.ClimaticChanges:
                    climaticChanges = clamped;
                    break;
                case StatId.InternationalRelations:
                    internationalRelations = clamped;
                    break;
                case StatId.PopularApproval:
                    popularApproval = clamped;
                    break;
                case StatId.Economy:
                    economy = clamped;
                    break;
                case StatId.Corruption:
                    corruption = clamped;
                    break;
            }
        }

        public void ApplyDelta(StatId id, int delta)
        {
            Set(id, Get(id) + delta);
        }

        public void ApplyImpacts(StatBlock impacts, bool hasCorruptionMods = false)
        {
            if (impacts == null) return;

            if (hasCorruptionMods)
            {
                int corruptionBonus = Mathf.RoundToInt(impacts.corruption * 0.2f);
                climaticChanges = Clamp(climaticChanges + impacts.climaticChanges);
                internationalRelations = Clamp(internationalRelations + impacts.internationalRelations + corruptionBonus);
                popularApproval = Clamp(popularApproval + impacts.popularApproval + corruptionBonus);
                economy = Clamp(economy + impacts.economy);
                corruption = Clamp(corruption + impacts.corruption);
            }
            else
            {
                climaticChanges = Clamp(climaticChanges + impacts.climaticChanges);
                internationalRelations = Clamp(internationalRelations + impacts.internationalRelations);
                popularApproval = Clamp(popularApproval + impacts.popularApproval);
                economy = Clamp(economy + impacts.economy);
                corruption = Clamp(corruption + impacts.corruption);
            }
        }

        public StatBlock Clone()
        {
            return new StatBlock(climaticChanges, internationalRelations, popularApproval, economy, corruption);
        }

        public static int Clamp(int value)
        {
            if (value < MinValue) return MinValue;
            if (value > MaxValue) return MaxValue;
            return value;
        }
    }
}
