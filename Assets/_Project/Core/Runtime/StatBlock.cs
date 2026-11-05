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
        public const int MinDelta = -100;
        public const int MaxDelta = 100;
        public const int DefaultInitialValue = 50;
        public const int DefaultInitialCorruption = 0;

        [Range(MinDelta, MaxDelta)]
        public int climaticChanges = DefaultInitialValue;

        [Range(MinDelta, MaxDelta)]
        public int internationalRelations = DefaultInitialValue;

        [Range(MinDelta, MaxDelta)]
        public int popularApproval = DefaultInitialValue;

        [Range(MinDelta, MaxDelta)]
        public int economy = DefaultInitialValue;

        [Range(MinDelta, MaxDelta)]
        public int corruption = DefaultInitialCorruption;

        [HideInInspector] public int maxClimaticChangesBonus = 0;
        [HideInInspector] public int maxInternationalRelationsBonus = 0;
        [HideInInspector] public int maxPopularApprovalBonus = 0;
        [HideInInspector] public int maxEconomyBonus = 0;
        [HideInInspector] public int maxCorruptionBonus = 0;

        public StatBlock()
        {
            ResetToDefaults();
        }

        public StatBlock(int climate, int relations, int approval, int eco, int corrupt, bool clamp = false)
        {
            climaticChanges = clamp ? Clamp(climate, MaxValue + maxClimaticChangesBonus) : climate;
            internationalRelations = clamp ? Clamp(relations, MaxValue + maxInternationalRelationsBonus) : relations;
            popularApproval = clamp ? Clamp(approval, MaxValue + maxPopularApprovalBonus) : approval;
            economy = clamp ? Clamp(eco, MaxValue + maxEconomyBonus) : eco;
            corruption = clamp ? Clamp(corrupt, MaxValue + maxCorruptionBonus) : corrupt;
        }

        public void ResetToDefaults()
        {
            climaticChanges = DefaultInitialValue;
            internationalRelations = DefaultInitialValue;
            popularApproval = DefaultInitialValue;
            economy = DefaultInitialValue;
            corruption = DefaultInitialCorruption;
            ResetMaxBonuses();
        }

        public void ResetMaxBonuses()
        {
            maxClimaticChangesBonus = 0;
            maxInternationalRelationsBonus = 0;
            maxPopularApprovalBonus = 0;
            maxEconomyBonus = 0;
            maxCorruptionBonus = 0;
        }

        public int GetMaxBonus(StatId id)
        {
            return id switch
            {
                StatId.ClimaticChanges => maxClimaticChangesBonus,
                StatId.InternationalRelations => maxInternationalRelationsBonus,
                StatId.PopularApproval => maxPopularApprovalBonus,
                StatId.Economy => maxEconomyBonus,
                StatId.Corruption => maxCorruptionBonus,
                _ => 0
            };
        }

        public void SetMaxBonus(StatId id, int bonus)
        {
            int safeBonus = Math.Max(0, bonus);
            switch (id)
            {
                case StatId.ClimaticChanges:
                    maxClimaticChangesBonus = safeBonus;
                    climaticChanges = Clamp(climaticChanges, GetMax(StatId.ClimaticChanges));
                    break;
                case StatId.InternationalRelations:
                    maxInternationalRelationsBonus = safeBonus;
                    internationalRelations = Clamp(internationalRelations, GetMax(StatId.InternationalRelations));
                    break;
                case StatId.PopularApproval:
                    maxPopularApprovalBonus = safeBonus;
                    popularApproval = Clamp(popularApproval, GetMax(StatId.PopularApproval));
                    break;
                case StatId.Economy:
                    maxEconomyBonus = safeBonus;
                    economy = Clamp(economy, GetMax(StatId.Economy));
                    break;
                case StatId.Corruption:
                    maxCorruptionBonus = safeBonus;
                    corruption = Clamp(corruption, GetMax(StatId.Corruption));
                    break;
            }
        }

        public int GetMax(StatId id)
        {
            return MaxValue + Math.Max(0, GetMaxBonus(id));
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
            int clamped = Clamp(value, GetMax(id));
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
                climaticChanges = Clamp(climaticChanges + impacts.climaticChanges, GetMax(StatId.ClimaticChanges));
                internationalRelations = Clamp(internationalRelations + impacts.internationalRelations + corruptionBonus, GetMax(StatId.InternationalRelations));
                popularApproval = Clamp(popularApproval + impacts.popularApproval + corruptionBonus, GetMax(StatId.PopularApproval));
                economy = Clamp(economy + impacts.economy, GetMax(StatId.Economy));
                corruption = Clamp(corruption + impacts.corruption, GetMax(StatId.Corruption));
            }
            else
            {
                climaticChanges = Clamp(climaticChanges + impacts.climaticChanges, GetMax(StatId.ClimaticChanges));
                internationalRelations = Clamp(internationalRelations + impacts.internationalRelations, GetMax(StatId.InternationalRelations));
                popularApproval = Clamp(popularApproval + impacts.popularApproval, GetMax(StatId.PopularApproval));
                economy = Clamp(economy + impacts.economy, GetMax(StatId.Economy));
                corruption = Clamp(corruption + impacts.corruption, GetMax(StatId.Corruption));
            }
        }

        public StatBlock Clone()
        {
            var copy = new StatBlock(climaticChanges, internationalRelations, popularApproval, economy, corruption)
            {
                maxClimaticChangesBonus = maxClimaticChangesBonus,
                maxInternationalRelationsBonus = maxInternationalRelationsBonus,
                maxPopularApprovalBonus = maxPopularApprovalBonus,
                maxEconomyBonus = maxEconomyBonus,
                maxCorruptionBonus = maxCorruptionBonus
            };
            return copy;
        }

        public static int Clamp(int value, int max = MaxValue)
        {
            if (value < MinValue) return MinValue;
            if (value > max) return max;
            return value;
        }
    }
}
