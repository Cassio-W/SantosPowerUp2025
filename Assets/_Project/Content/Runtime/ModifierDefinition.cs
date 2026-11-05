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

        // Resgate Emergencial contra Derrota por Atributo
        public bool isEmergencyRescue = false;
        public bool rescueAnyDepletedStat = false;
        public StatId rescueStat = StatId.ClimaticChanges;
        public int rescueRestoreValue = 35;
        public int rechargeMonths = 0; // > 0 = entra em recarga por N meses ao invés de ser removido permanentemente
        public string rescueMessage = string.Empty;

        [Header("Efeito Mensal Passivo")]
        public StatBlock statDeltasPerMonth = new StatBlock(0, 0, 0, 0, 0);

        [Header("Condição para Efeito Mensal")]
        public bool hasMonthlyCondition = false;
        public StatId conditionStat = StatId.InternationalRelations;
        public int conditionMinStatValue = 51;

        [Header("Modificadores de Limite Máximo de Atributos")]
        public StatBlock maxStatBonuses = new StatBlock(0, 0, 0, 0, 0);

        [Header("Bônus ao Concluir Duração (Ex: Investimento à Longo Prazo)")]
        public StatBlock expirationStatBonus = new StatBlock(0, 0, 0, 0, 0);

        [Header("Variação Aleatória Mensal (Ex: Criptomoeda do Real)")]
        public bool hasRandomMonthlySwing = false;
        public StatId randomSwingStat = StatId.Economy;
        public int randomSwingMagnitude = 3;

        [Header("Gatilho Sazonal no Calendário (Ex: Comunicador Nato)")]
        public bool boostLowestStatOnMonthOfYear = false;
        public int triggerMonthOfYear = 6;
        public int lowestStatBoostAmount = 30;

        [Header("Regras Especiais de Gameplay")]
        [Tooltip("Sempre que perder Popularidade, metade do valor perdido é obtido em Relação Internacional (O Grande Irmão).")]
        public bool convertPopularityLossToRelations = false;
        [Tooltip("Redução percentual (0..1) no custo de Corrupção de consumíveis/contatos do celular (Aliança com o Legislativo = 0.20).")]
        public float consumableCorruptionDiscount = 0f;
        [Tooltip("Chance (0..1) de não acontecer mudança alguma ao responder uma proposta (Sempre Aposte em Nada = 0.10).")]
        public float ignoreProposalImpactsChance = 0f;

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

        public static PerkDefinition CreateRescuePerk(
            string id,
            string title,
            string description,
            StatId rescueStat,
            int rescueRestoreValue = 35,
            Sprite icon = null)
        {
            var perk = CreateInstance<PerkDefinition>();
            perk.id = id;
            perk.title = title;
            perk.description = description;
            perk.isPassive = false;
            perk.isOneTimeUse = true;
            perk.isEmergencyRescue = true;
            perk.rescueStat = rescueStat;
            perk.rescueRestoreValue = rescueRestoreValue;
            perk.icon = icon;
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
