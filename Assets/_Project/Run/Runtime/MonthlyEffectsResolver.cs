using System;
using System.Collections.Generic;
using Mandato.Content;
using Mandato.Core;

namespace Mandato.Run
{
    [Serializable]
    public class MonthlyEffectsReport
    {
        public bool isSuccess = true;
        public StatBlock statsBefore = new StatBlock(0, 0, 0, 0, 0);
        public StatBlock statsAfter = new StatBlock(0, 0, 0, 0, 0);
        public StatBlock impactsApplied = new StatBlock(0, 0, 0, 0, 0);

        public List<string> activePerkIdsApplied = new List<string>();
        public List<string> activeEventIdsApplied = new List<string>();
        public List<string> expiredPerkIds = new List<string>();
        public List<string> expiredEventIds = new List<string>();
        public List<string> triggeredEventIds = new List<string>();

        public int advancedToMonthIndex = 1;
        public string advancedToDisplayDate = string.Empty;
        public RunTermination resultingTermination = RunTermination.Ongoing;

        public bool HasAnyImpact => impactsApplied.climaticChanges != 0 ||
                                    impactsApplied.economy != 0 ||
                                    impactsApplied.internationalRelations != 0 ||
                                    impactsApplied.popularApproval != 0 ||
                                    impactsApplied.corruption != 0;
    }

    public static class MonthlyEffectsResolver
    {
        public const string CampaignEventId = "CampanhaEleitoral";

        /// <summary>
        /// Ordem estrita de resolução do ciclo mensal:
        /// 1. Efeitos Mensais de Perks Ativos
        /// 2. Efeitos Mensais de Eventos Ativos
        /// 3. Aplicação acumulada e verificação de integridade dos Atributos
        /// 4. Gatilhos automáticos de eventos do calendário (ex: Campanha Eleitoral no Ano 4)
        /// 5. Decremento de duração e expiração de Perks temporários
        /// 6. Decremento de duração e expiração de Eventos temporários
        /// 7. Decremento de Cooldowns de Ações do Flip-Phone
        /// 8. Avanço do Calendário
        /// 9. Avaliação das Regras Terminais (Vitória / Derrota)
        /// </summary>
        public static MonthlyEffectsReport ResolveMonth(
            RunState runState,
            IReadOnlyDictionary<string, PerkDefinition> perkCatalog = null,
            IReadOnlyDictionary<string, RunEventDefinition> eventCatalog = null)
        {
            if (runState == null)
            {
                return new MonthlyEffectsReport { isSuccess = false, resultingTermination = RunTermination.CreateDefeat("Estado nulo.") };
            }

            var report = new MonthlyEffectsReport
            {
                statsBefore = runState.stats.Clone()
            };

            if (!runState.termination.IsOngoing)
            {
                report.statsAfter = runState.stats.Clone();
                report.advancedToMonthIndex = runState.calendar.currentMonthIndex;
                report.advancedToDisplayDate = runState.calendar.DisplayDate;
                report.resultingTermination = runState.termination;
                return report;
            }

            // 1. Efeitos Mensais de Perks Ativos
            if (runState.activePerks != null && perkCatalog != null)
            {
                foreach (var perkState in runState.activePerks)
                {
                    if (perkState == null || string.IsNullOrEmpty(perkState.perkId)) continue;

                    if (perkCatalog.TryGetValue(perkState.perkId, out var def) && def != null && def.statDeltasPerMonth != null)
                    {
                        report.impactsApplied.climaticChanges += def.statDeltasPerMonth.climaticChanges;
                        report.impactsApplied.economy += def.statDeltasPerMonth.economy;
                        report.impactsApplied.internationalRelations += def.statDeltasPerMonth.internationalRelations;
                        report.impactsApplied.popularApproval += def.statDeltasPerMonth.popularApproval;
                        report.impactsApplied.corruption += def.statDeltasPerMonth.corruption;
                        report.activePerkIdsApplied.Add(perkState.perkId);
                    }
                }
            }

            // 2. Efeitos Mensais de Eventos Ativos
            if (runState.activeEvents != null && eventCatalog != null)
            {
                foreach (var eventState in runState.activeEvents)
                {
                    if (eventState == null || string.IsNullOrEmpty(eventState.eventId)) continue;

                    if (eventCatalog.TryGetValue(eventState.eventId, out var def) && def != null && def.statModifiersPerMonth != null)
                    {
                        report.impactsApplied.climaticChanges += def.statModifiersPerMonth.climaticChanges;
                        report.impactsApplied.economy += def.statModifiersPerMonth.economy;
                        report.impactsApplied.internationalRelations += def.statModifiersPerMonth.internationalRelations;
                        report.impactsApplied.popularApproval += def.statModifiersPerMonth.popularApproval;
                        report.impactsApplied.corruption += def.statModifiersPerMonth.corruption;
                        report.activeEventIdsApplied.Add(eventState.eventId);
                    }
                }
            }

            // 3. Aplica impactos acumulados
            if (report.HasAnyImpact)
            {
                runState.ApplyStatImpacts(report.impactsApplied);
            }

            // 4. Gatilhos de calendário (Ex.: Campanha Eleitoral no 4º ano, a partir do mês 37)
            if (runState.calendar.currentMonthIndex == 37)
            {
                if (!runState.activeEvents.Exists(e => string.Equals(e.eventId, CampaignEventId, StringComparison.OrdinalIgnoreCase)))
                {
                    runState.TriggerEvent(CampaignEventId, duration: 12);
                    report.triggeredEventIds.Add(CampaignEventId);
                }
            }

            // 5. Expiração de Perks temporários
            for (int i = runState.activePerks.Count - 1; i >= 0; i--)
            {
                var p = runState.activePerks[i];
                if (p.remainingMonths > 0)
                {
                    p.remainingMonths--;
                    if (p.remainingMonths == 0)
                    {
                        report.expiredPerkIds.Add(p.perkId);
                        runState.activePerkIds.Remove(p.perkId);
                        runState.activePerks.RemoveAt(i);
                    }
                }
            }

            // 6. Expiração de Eventos temporários
            for (int i = runState.activeEvents.Count - 1; i >= 0; i--)
            {
                var ev = runState.activeEvents[i];
                ev.remainingMonths--;
                if (ev.remainingMonths <= 0)
                {
                    report.expiredEventIds.Add(ev.eventId);
                    runState.activeEvents.RemoveAt(i);
                }
            }

            // 7. Cooldowns de Ações do Flip-Phone
            runState.TickActionCooldowns();

            // 8. Avanço do Calendário
            runState.calendar.Advance();

            // 9. Avaliação e Aplicação de Resgate Emergencial de Perks
            runState.CheckAndApplyEmergencyRescue(perkCatalog);

            // 10. Avaliação das Regras Terminais
            runState.UpdateTermination();

            report.statsAfter = runState.stats.Clone();
            report.advancedToMonthIndex = runState.calendar.currentMonthIndex;
            report.advancedToDisplayDate = runState.calendar.DisplayDate;
            report.resultingTermination = runState.termination;

            return report;
        }
    }
}
