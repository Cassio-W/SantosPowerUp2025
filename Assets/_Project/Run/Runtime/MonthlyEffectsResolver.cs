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

        // Investimentos a Longo Prazo
        public List<string> completedInvestmentIds = new List<string>();
        public List<string> cancelledInvestmentIds = new List<string>();
        public List<string> investmentGrantedPerkIds = new List<string>();
        public List<string> investmentTriggeredEventIds = new List<string>();

        /// <summary>Resultado da rodada mensal da guerra. Null se não havia guerra ativa.</summary>
        public WarMonthReport war;

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
        /// 7c. Rodada mensal do Evento de Guerra (WarRules.ResolveMonthEnd)
        /// 8. Avanço do Calendário
        /// 9. Avaliação das Regras Terminais (Vitória / Derrota)
        /// </summary>
        public static MonthlyEffectsReport ResolveMonth(
            RunState runState,
            IReadOnlyDictionary<string, PerkDefinition> perkCatalog = null,
            IReadOnlyDictionary<string, RunEventDefinition> eventCatalog = null,
            IReadOnlyDictionary<string, LongTermInvestmentDefinition> investmentCatalog = null)
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
            if (runState.activePerks != null)
            {
                foreach (var perkState in runState.activePerks)
                {
                    if (perkState == null || string.IsNullOrEmpty(perkState.perkId)) continue;

                    PerkDefinition def = null;
                    if (perkCatalog != null)
                    {
                        perkCatalog.TryGetValue(perkState.perkId, out def);
                    }

                    bool appliedThisPerk = false;

                    // 1a. Condição para efeito mensal (Ex: Tratado com o Oriente: Rel. Int > 50; Movimento Ambientalista: Clima > 50)
                    bool conditionMet = true;
                    if (def != null && def.hasMonthlyCondition)
                    {
                        conditionMet = runState.stats.Get(def.conditionStat) >= def.conditionMinStatValue;
                    }
                    else if (def == null)
                    {
                        if (string.Equals(perkState.perkId, "TratadoInternacional", StringComparison.OrdinalIgnoreCase))
                        {
                            if (runState.stats.internationalRelations > 50)
                            {
                                report.impactsApplied.popularApproval += 1;
                                report.impactsApplied.economy += 1;
                                appliedThisPerk = true;
                            }
                            conditionMet = false;
                        }
                        else if (string.Equals(perkState.perkId, "MovimentoAmbientalista", StringComparison.OrdinalIgnoreCase))
                        {
                            if (runState.stats.climaticChanges > 50)
                            {
                                report.impactsApplied.popularApproval += 1;
                                report.impactsApplied.corruption += 1;
                                appliedThisPerk = true;
                            }
                            conditionMet = false;
                        }
                    }

                    // 1b. Deltas mensais fixos (se a condição mensal for satisfeita)
                    if (def != null && def.statDeltasPerMonth != null && conditionMet)
                    {
                        report.impactsApplied.climaticChanges += def.statDeltasPerMonth.climaticChanges;
                        report.impactsApplied.economy += def.statDeltasPerMonth.economy;
                        report.impactsApplied.internationalRelations += def.statDeltasPerMonth.internationalRelations;
                        report.impactsApplied.popularApproval += def.statDeltasPerMonth.popularApproval;
                        report.impactsApplied.corruption += def.statDeltasPerMonth.corruption;
                        appliedThisPerk = true;
                    }

                    // 1c. Variação aleatória mensal (Ex: Criptomoeda do Real -> +3 ou -3 de Economia com 50% de chance)
                    bool hasRandomSwing = (def != null && def.hasRandomMonthlySwing && def.randomSwingMagnitude > 0) ||
                                          (def == null && string.Equals(perkState.perkId, "Cripto", StringComparison.OrdinalIgnoreCase));
                    if (hasRandomSwing)
                    {
                        int mag = (def != null && def.randomSwingMagnitude > 0) ? def.randomSwingMagnitude : 3;
                        StatId targetStat = def != null ? def.randomSwingStat : StatId.Economy;
                        int swingSeed = (runState.seed != 0 ? runState.seed : Environment.TickCount)
                                        ^ (runState.calendar.currentMonthIndex * 31337);
                        int delta = (new Random(swingSeed).Next(0, 2) == 0) ? mag : -mag;

                        switch (targetStat)
                        {
                            case StatId.ClimaticChanges: report.impactsApplied.climaticChanges += delta; break;
                            case StatId.InternationalRelations: report.impactsApplied.internationalRelations += delta; break;
                            case StatId.PopularApproval: report.impactsApplied.popularApproval += delta; break;
                            case StatId.Economy: report.impactsApplied.economy += delta; break;
                            case StatId.Corruption: report.impactsApplied.corruption += delta; break;
                        }
                        appliedThisPerk = true;
                    }

                    // 1d. Gatilho sazonal no 6º mês de cada ano (Ex: Comunicador Nato -> menor atributo não-corrupção +30)
                    bool hasLowestStatBoost = (def != null && def.boostLowestStatOnMonthOfYear) ||
                                              (def == null && string.Equals(perkState.perkId, "ComunicadorNato", StringComparison.OrdinalIgnoreCase));
                    if (hasLowestStatBoost)
                    {
                        int triggerMonth = (def != null && def.triggerMonthOfYear > 0) ? def.triggerMonthOfYear : 6;
                        int boostAmount = (def != null && def.lowestStatBoostAmount > 0) ? def.lowestStatBoostAmount : 30;

                        if (runState.calendar.MonthInYear == triggerMonth)
                        {
                            StatId lowestStat = GetLowestNonCorruptionStat(runState.stats);
                            switch (lowestStat)
                            {
                                case StatId.ClimaticChanges: report.impactsApplied.climaticChanges += boostAmount; break;
                                case StatId.InternationalRelations: report.impactsApplied.internationalRelations += boostAmount; break;
                                case StatId.PopularApproval: report.impactsApplied.popularApproval += boostAmount; break;
                                case StatId.Economy: report.impactsApplied.economy += boostAmount; break;
                            }
                            appliedThisPerk = true;
                        }
                    }

                    if (appliedThisPerk && !report.activePerkIdsApplied.Contains(perkState.perkId))
                    {
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
                runState.ApplyStatImpacts(report.impactsApplied, false, perkCatalog);
            }

            // 4. Gatilhos de calendário (Ex.: Campanha Eleitoral no 4º ano, a partir do mês 37)
            if (runState.calendar.currentMonthIndex == 37)
            {
                if (!runState.activeEvents.Exists(e => string.Equals(e.eventId, CampaignEventId, StringComparison.OrdinalIgnoreCase)))
                {
                    runState.TriggerEvent(CampaignEventId, duration: 12);
                    runState.LockPoliticalAxis();
                    report.triggeredEventIds.Add(CampaignEventId);
                }
            }

            // 5. Expiração de Perks temporários e Recarga de Perks (Ex: Imorrível recarrega a cada 18 meses; Investimento à longo prazo dá +25 Economia após 12 meses)
            for (int i = runState.activePerks.Count - 1; i >= 0; i--)
            {
                var p = runState.activePerks[i];
                if (p == null) continue;

                if (p.cooldownMonths > 0)
                {
                    p.cooldownMonths--;
                }

                if (p.remainingMonths > 0)
                {
                    p.remainingMonths--;
                    if (p.remainingMonths == 0)
                    {
                        PerkDefinition expDef = null;
                        if (perkCatalog != null)
                        {
                            perkCatalog.TryGetValue(p.perkId, out expDef);
                        }

                        if (expDef != null && expDef.expirationStatBonus != null &&
                            (expDef.expirationStatBonus.climaticChanges != 0 ||
                             expDef.expirationStatBonus.economy != 0 ||
                             expDef.expirationStatBonus.internationalRelations != 0 ||
                             expDef.expirationStatBonus.popularApproval != 0 ||
                             expDef.expirationStatBonus.corruption != 0))
                        {
                            runState.ApplyStatImpacts(expDef.expirationStatBonus, false, perkCatalog);
                            report.impactsApplied.climaticChanges += expDef.expirationStatBonus.climaticChanges;
                            report.impactsApplied.economy += expDef.expirationStatBonus.economy;
                            report.impactsApplied.internationalRelations += expDef.expirationStatBonus.internationalRelations;
                            report.impactsApplied.popularApproval += expDef.expirationStatBonus.popularApproval;
                            report.impactsApplied.corruption += expDef.expirationStatBonus.corruption;
                        }
                        else if (expDef == null && string.Equals(p.perkId, "InvestimentoUsina", StringComparison.OrdinalIgnoreCase))
                        {
                            var bonus = new StatBlock(0, 0, 0, 25, 0);
                            runState.ApplyStatImpacts(bonus, false, perkCatalog);
                            report.impactsApplied.economy += 25;
                        }

                        report.expiredPerkIds.Add(p.perkId);
                        runState.activePerkIds.Remove(p.perkId);
                        runState.activePerkIds.RemoveAll(id => string.Equals(id, p.perkId, StringComparison.OrdinalIgnoreCase));
                        runState.activePerks.RemoveAt(i);
                        runState.RefreshMaxStatBonuses(perkCatalog);
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

            // 7b. Tick de Investimentos a Longo Prazo
            if (runState.activeInvestments != null && runState.activeInvestments.Count > 0)
            {
                for (int i = runState.activeInvestments.Count - 1; i >= 0; i--)
                {
                    var inv = runState.activeInvestments[i];
                    if (inv == null) continue;

                    if (inv.isCancelled)
                    {
                        report.cancelledInvestmentIds.Add(inv.investmentId);
                        runState.activeInvestments.RemoveAt(i);
                        continue;
                    }

                    // Cobra o custo mensal antes de decrementar
                    LongTermInvestmentDefinition invDef = null;
                    if (investmentCatalog != null)
                        investmentCatalog.TryGetValue(inv.investmentId, out invDef);

                    if (invDef != null && invDef.HasMonthlyCost)
                    {
                        runState.ApplyStatImpacts(invDef.costPerMonth);
                    }

                    // Decrementa o contador; TickMonth() retorna true se concluiu agora
                    bool justCompleted = inv.TickMonth();

                    if (justCompleted)
                    {
                        report.completedInvestmentIds.Add(inv.investmentId);
                        runState.activeInvestments.RemoveAt(i);

                        if (invDef != null)
                        {
                            // Bônus instantâneo de atributos
                            if (invDef.HasCompletionStatBonus)
                                runState.ApplyStatImpacts(invDef.completionStatBonus);

                            // Perks de conclusão
                            foreach (string perkId in invDef.GetCompletionPerkIds())
                            {
                                if (string.IsNullOrEmpty(perkId)) continue;
                                int dur = 0;
                                if (perkCatalog != null && perkCatalog.TryGetValue(perkId, out var pDef) && pDef != null)
                                    dur = pDef.durationMonths;
                                runState.GrantPerk(perkId, dur);
                                report.investmentGrantedPerkIds.Add(perkId);
                            }

                            // Eventos de run de conclusão
                            foreach (string evId in invDef.GetCompletionRunEventIds())
                            {
                                if (string.IsNullOrEmpty(evId)) continue;
                                int dur = 3;
                                if (eventCatalog != null && eventCatalog.TryGetValue(evId, out var eDef) && eDef != null)
                                    dur = eDef.durationMonths;
                                runState.TriggerEvent(evId, dur);
                                report.investmentTriggeredEventIds.Add(evId);
                            }

                            // Evento interativo agendado
                            if (!string.IsNullOrEmpty(invDef.completionScheduleEventId))
                                runState.ScheduleEvent(invDef.completionScheduleEventId);
                        }
                    }
                }
            }

            // 7c. Rodada mensal do Evento de Guerra (início da guerra agendada ou resolução mensal)
            if (runState.pendingWarLevel != WarLevel.None)
            {
                WarRules.StartWar(runState, runState.pendingWarLevel, runState.pendingWarTargetId);
                runState.pendingWarLevel = WarLevel.None;
                runState.pendingWarTargetId = string.Empty;
            }
            else
            {
                report.war = WarRules.ResolveMonthEnd(runState);
            }

            // 8. Decremento de Suspensão de NPCs
            if (runState.npcStates != null)
            {
                foreach (var npc in runState.npcStates.Values)
                {
                    if (npc != null && npc.suspendedMonths > 0)
                    {
                        npc.suspendedMonths--;
                    }
                }
            }

            // 9. Reset de Vantagens Mensais Temporárias
            runState.preventStatLossThisMonth = false;
            runState.isPreviewAttributesActive = false;

            // 10. Avanço do Calendário
            runState.calendar.Advance();

            // 11. Avaliação e Aplicação de Resgate Emergencial de Perks
            runState.CheckAndApplyEmergencyRescue(perkCatalog);

            // 12. Avaliação das Regras Terminais
            runState.UpdateTermination();

            report.statsAfter = runState.stats.Clone();
            report.advancedToMonthIndex = runState.calendar.currentMonthIndex;
            report.advancedToDisplayDate = runState.calendar.DisplayDate;
            report.resultingTermination = runState.termination;

            return report;
        }

        public static StatId GetLowestNonCorruptionStat(StatBlock stats)
        {
            if (stats == null) return StatId.ClimaticChanges;

            StatId lowest = StatId.ClimaticChanges;
            int minVal = stats.climaticChanges;

            if (stats.economy < minVal)
            {
                minVal = stats.economy;
                lowest = StatId.Economy;
            }
            if (stats.popularApproval < minVal)
            {
                minVal = stats.popularApproval;
                lowest = StatId.PopularApproval;
            }
            if (stats.internationalRelations < minVal)
            {
                minVal = stats.internationalRelations;
                lowest = StatId.InternationalRelations;
            }

            return lowest;
        }
    }
}
