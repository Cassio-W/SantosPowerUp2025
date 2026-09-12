using System;
using Mandato.Core;
using UnityEngine;

namespace Mandato.Content
{
    [CreateAssetMenu(fileName = "NewPerk", menuName = "Mandato/Perk Definition")]
    public class PerkDefinition : ScriptableObject
    {
        public string id = string.Empty;
        public string title = string.Empty;
        [TextArea(2, 4)] public string description = string.Empty;
        public Sprite icon;

        public bool isPassive = true;
        public bool isOneTimeUse = false;
        public int durationMonths = 0; // 0 = permanente durante a run toda

        public StatBlock statDeltasPerMonth = new StatBlock(0, 0, 0, 0, 0);
        public bool corruptionImmunity = false;

        public static PerkDefinition CreateRuntimeInstance(
            string id,
            string title,
            string description,
            StatBlock monthlyDeltas = null,
            int duration = 0)
        {
            var perk = CreateInstance<PerkDefinition>();
            perk.id = id;
            perk.title = title;
            perk.description = description;
            perk.statDeltasPerMonth = monthlyDeltas ?? new StatBlock(0, 0, 0, 0, 0);
            perk.durationMonths = duration;
            return perk;
        }
    }

    [CreateAssetMenu(fileName = "NewRunEvent", menuName = "Mandato/Run Event Definition")]
    public class RunEventDefinition : ScriptableObject
    {
        public string id = string.Empty;
        public string title = string.Empty;
        [TextArea(2, 4)] public string description = string.Empty;

        public int durationMonths = 3; // Ex: 3 meses para Campanha Eleitoral, Seca, etc.
        public StatBlock statModifiersPerMonth = new StatBlock(0, 0, 0, 0, 0);

        public static RunEventDefinition CreateRuntimeInstance(
            string id,
            string title,
            string description,
            int duration = 3,
            StatBlock monthlyModifiers = null)
        {
            var ev = CreateInstance<RunEventDefinition>();
            ev.id = id;
            ev.title = title;
            ev.description = description;
            ev.durationMonths = duration;
            ev.statModifiersPerMonth = monthlyModifiers ?? new StatBlock(0, 0, 0, 0, 0);
            return ev;
        }
    }

    [Obsolete("Utilize FlipPhoneActionDefinition para todas as ações do telefone.")]
    [CreateAssetMenu(fileName = "NewPhoneAction", menuName = "Mandato/Phone Action Definition (Legacy)")]
    public class PhoneActionDefinition : ScriptableObject
    {
        public string id = string.Empty;
        public string actionName = string.Empty;
        [TextArea(2, 3)] public string description = string.Empty;

        public int politicalCost = 0;
        public int corruptionCost = 0;
        public StatBlock instantStatImpacts = new StatBlock(0, 0, 0, 0, 0);

        public static PhoneActionDefinition CreateRuntimeInstance(
            string id,
            string name,
            string description,
            int popCost = 0,
            int corruptCost = 0,
            StatBlock impacts = null)
        {
            var action = CreateInstance<PhoneActionDefinition>();
            action.id = id;
            action.actionName = name;
            action.description = description;
            action.politicalCost = popCost;
            action.corruptionCost = corruptCost;
            action.instantStatImpacts = impacts ?? new StatBlock(0, 0, 0, 0, 0);
            return action;
        }
    }
}
