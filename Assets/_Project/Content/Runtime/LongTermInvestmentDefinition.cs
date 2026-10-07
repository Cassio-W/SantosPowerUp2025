using System;
using System.Collections.Generic;
using Mandato.Core;
using UnityEngine;

namespace Mandato.Content
{
    /// <summary>
    /// Define um Investimento a Longo Prazo: um processo iniciado ao aceitar uma proposta especial
    /// que consome recursos mensalmente por um período determinado e, ao concluir, dispara uma
    /// lista de efeitos configurados externamente (caixa preta) — perks, eventos, prefabs, etc.
    ///
    /// A definição não sabe o que são os efeitos de conclusão; ela apenas armazena seus IDs.
    /// O sistema de resolução (MonthlyEffectsResolver) interpreta esses IDs usando os catálogos da run.
    /// </summary>
    [CreateAssetMenu(fileName = "NewInvestment", menuName = "Mandato/Long Term Investment Definition")]
    public class LongTermInvestmentDefinition : ScriptableObject
    {
        public string id = string.Empty;
        public string title = string.Empty;
        [TextArea(2, 5)] public string description = string.Empty;

        [Tooltip("Quantidade de meses que o investimento leva para ser concluído.")]
        [Range(1, 48)]
        public int durationMonths = 12;

        [Tooltip("Custo mensal em atributos enquanto o investimento está ativo. " +
                 "Valores negativos representam gastos (ex: -5 Economia por mês).")]
        public StatBlock costPerMonth = new StatBlock(0, 0, 0, 0, 0);

        [Header("Efeitos de Conclusão (Caixa Preta)")]
        [Tooltip("IDs de Perks concedidos ao concluir o investimento.")]
        public List<string> completionPerkIds = new List<string>();

        [Tooltip("Arraste os Perks concedidos ao concluir (alternativa a IDs por texto).")]
        public List<PerkDefinition> completionPerks = new List<PerkDefinition>();

        [Tooltip("IDs de Eventos de Run ativados ao concluir o investimento.")]
        public List<string> completionRunEventIds = new List<string>();

        [Tooltip("Arraste os RunEventDefinitions ativados ao concluir (alternativa a IDs por texto).")]
        public List<RunEventDefinition> completionRunEvents = new List<RunEventDefinition>();

        [Tooltip("ID de evento interativo agendado para ser apresentado na conclusão.")]
        public string completionScheduleEventId = string.Empty;

        [Tooltip("Impacto instantâneo nos atributos ao concluir o investimento.")]
        public StatBlock completionStatBonus = new StatBlock(0, 0, 0, 0, 0);

        [Tooltip("Prefab instanciado na cidade ao concluir o investimento (opcional, mesmo mecanismo das propostas de construção).")]
        public GameObject completionCityPropPrefab;

        /// <summary>
        /// Retorna todos os IDs de perks de conclusão (referências diretas + IDs por texto).
        /// </summary>
        public IEnumerable<string> GetCompletionPerkIds()
        {
            if (completionPerks != null)
            {
                foreach (var p in completionPerks)
                {
                    if (p != null)
                        yield return !string.IsNullOrEmpty(p.id) ? p.id : p.name;
                }
            }
            if (completionPerkIds != null)
            {
                foreach (var id in completionPerkIds)
                {
                    if (!string.IsNullOrEmpty(id))
                        yield return id;
                }
            }
        }

        /// <summary>
        /// Retorna todos os IDs de eventos de run de conclusão (referências diretas + IDs por texto).
        /// </summary>
        public IEnumerable<string> GetCompletionRunEventIds()
        {
            if (completionRunEvents != null)
            {
                foreach (var e in completionRunEvents)
                {
                    if (e != null)
                        yield return !string.IsNullOrEmpty(e.id) ? e.id : e.name;
                }
            }
            if (completionRunEventIds != null)
            {
                foreach (var id in completionRunEventIds)
                {
                    if (!string.IsNullOrEmpty(id))
                        yield return id;
                }
            }
        }

        public bool HasCompletionStatBonus =>
            completionStatBonus != null && (
                completionStatBonus.climaticChanges != 0 ||
                completionStatBonus.economy != 0 ||
                completionStatBonus.internationalRelations != 0 ||
                completionStatBonus.popularApproval != 0 ||
                completionStatBonus.corruption != 0);

        public bool HasMonthlyCost =>
            costPerMonth != null && (
                costPerMonth.climaticChanges != 0 ||
                costPerMonth.economy != 0 ||
                costPerMonth.internationalRelations != 0 ||
                costPerMonth.popularApproval != 0 ||
                costPerMonth.corruption != 0);

        public static LongTermInvestmentDefinition CreateRuntimeInstance(
            string id,
            string title,
            string description,
            int durationMonths,
            StatBlock costPerMonth = null,
            StatBlock completionBonus = null)
        {
            var inv = CreateInstance<LongTermInvestmentDefinition>();
            inv.id = id;
            inv.title = title;
            inv.description = description;
            inv.durationMonths = Math.Max(1, durationMonths);
            inv.costPerMonth = costPerMonth ?? new StatBlock(0, 0, 0, 0, 0);
            inv.completionStatBonus = completionBonus ?? new StatBlock(0, 0, 0, 0, 0);
            return inv;
        }
    }
}
