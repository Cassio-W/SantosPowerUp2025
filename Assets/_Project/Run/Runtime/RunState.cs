using System;
using System.Collections.Generic;
using Mandato.Content;
using Mandato.Core;

namespace Mandato.Run
{
    [Serializable]
    public class MonthDecisionRecord
    {
        public int monthIndex = 1;
        public string cardId = string.Empty;
        public string title = string.Empty;
        public string npcName = string.Empty;
        public bool isApproved = true;
        public string choiceLabel = string.Empty;
        public string stat1Text = string.Empty;
        public bool stat1Positive = true;
        public string stat2Text = string.Empty;
        public bool stat2Positive = true;
        public string perkText = string.Empty;
        public bool isEvent = false;
        public string eventId = string.Empty;
        public string eventDetails = string.Empty;
    }

    [Serializable]
    public class RunState
    {
        public StatBlock stats = new StatBlock();
        public PoliticalAxis politicalAxis = new PoliticalAxis();
        public RunCalendar calendar = new RunCalendar();
        public RunTermination termination = RunTermination.Ongoing;

        public List<string> activePerkIds = new List<string>();
        public List<string> decisionHistory = new List<string>();
        public List<MonthDecisionRecord> pastDecisions = new List<MonthDecisionRecord>();
        public Dictionary<string, NpcRunState> npcStates = new Dictionary<string, NpcRunState>();
        public Dictionary<string, QuestRunState> questStates = new Dictionary<string, QuestRunState>();
        public List<ActiveEventState> activeEvents = new List<ActiveEventState>();
        public List<ActivePerkState> activePerks = new List<ActivePerkState>();
        public List<string> unlockedActionIds = new List<string>();
        public Dictionary<string, int> actionCharges = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, int> actionCooldowns = new Dictionary<string, int>();
        public List<string> consumedSingleUseActions = new List<string>();
        public string activeCharacterId = string.Empty;
        public bool preventStatLossThisMonth = false;
        public bool isPreviewAttributesActive = false;
        public int seed;

        /// <summary>Estado do Evento de Guerra (inativo até ser declarado).</summary>
        public WarState war = new WarState();

        /// <summary>Guerra agendada para iniciar assim que o mês virar (quando o NPC deixa a sala).</summary>
        public WarLevel pendingWarLevel = WarLevel.None;
        public string pendingWarTargetId = string.Empty;

        /// <summary>
        /// Balanceamento da guerra (configuração imutável injetada pelo bootstrap a partir do WarEventDefinition).
        /// Não é estado da partida: não é salvo e sobrevive ao Reset().
        /// </summary>
        [NonSerialized] public WarBalance warBalance = WarBalance.CreateDefault();

        /// <summary>
        /// ID do evento interativo agendado para o próximo turno disponível.
        /// Quando preenchido, o próximo DrawAndPresentProposal será substituído pelo evento.
        /// </summary>
        public string scheduledEventId = string.Empty;

        /// <summary>
        /// Eventos agendados por mês específico do calendário (1..totalMonths).
        /// Mapeia: monthIndex → eventId.
        /// </summary>
        public Dictionary<int, string> scheduledEventsByMonth = new Dictionary<int, string>();

        /// <summary>
        /// Investimentos a Longo Prazo ativos. Cada entrada é decrementada mensalmente
        /// pelo MonthlyEffectsResolver; ao concluir, seus efeitos são disparados.
        /// </summary>
        public List<ActiveInvestmentState> activeInvestments = new List<ActiveInvestmentState>();

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
            pastDecisions.Clear();
            npcStates.Clear();
            questStates.Clear();
            activeEvents.Clear();
            activePerks.Clear();
            unlockedActionIds.Clear();
            actionCharges.Clear();
            actionCooldowns.Clear();
            consumedSingleUseActions.Clear();
            activeCharacterId = string.Empty;
            preventStatLossThisMonth = false;
            isPreviewAttributesActive = false;
            scheduledEventId = string.Empty;
            scheduledEventsByMonth.Clear();
            activeInvestments.Clear();
            war = new WarState();
            pendingWarLevel = WarLevel.None;
            pendingWarTargetId = string.Empty;
        }

        public void RecordMonthDecision(MonthDecisionRecord record)
        {
            if (record == null) return;
            int existingIndex = pastDecisions.FindIndex(r => r.monthIndex == record.monthIndex);
            if (existingIndex >= 0)
            {
                pastDecisions[existingIndex] = record;
            }
            else
            {
                pastDecisions.Add(record);
            }
        }

        public MonthDecisionRecord GetDecisionForMonth(int monthIndex)
        {
            return pastDecisions?.Find(r => r.monthIndex == monthIndex);
        }

        public NpcRunState GetOrCreateNpcState(string npcId)
        {
            if (string.IsNullOrEmpty(npcId)) return null;

            if (!npcStates.TryGetValue(npcId, out var state))
            {
                state = new NpcRunState(npcId);
                npcStates[npcId] = state;
            }
            return state;
        }

        public int GetNpcRelation(string npcId)
        {
            if (string.IsNullOrEmpty(npcId)) return 0;
            return npcStates.TryGetValue(npcId, out var state) ? state.relationScore : 0;
        }

        public bool IsNpcAvailable(string npcId)
        {
            if (string.IsNullOrEmpty(npcId)) return true;
            if (npcStates.TryGetValue(npcId, out var state) && state != null)
            {
                if (state.isDead || state.isRemoved || state.isSuspended)
                    return false;
            }
            return true;
        }

        public QuestRunState GetOrCreateQuestState(string questId)
        {
            if (string.IsNullOrEmpty(questId)) return null;

            if (!questStates.TryGetValue(questId, out var state))
            {
                state = new QuestRunState(questId);
                questStates[questId] = state;
            }
            return state;
        }

        public (int step, bool completed, bool failed) GetQuestState(string questId)
        {
            if (string.IsNullOrEmpty(questId) || !questStates.TryGetValue(questId, out var q))
            {
                return (0, false, false);
            }
            return (q.currentStepIndex, q.isCompleted, q.isFailed);
        }

        public bool IsQuestCompleted(string questId)
        {
            if (string.IsNullOrEmpty(questId)) return false;
            return questStates.TryGetValue(questId, out var q) && q.isCompleted;
        }

        public void TriggerEvent(string eventId, int duration = 3)
        {
            if (string.IsNullOrEmpty(eventId)) return;

            var existing = activeEvents.Find(e => string.Equals(e.eventId, eventId, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                existing.remainingMonths = duration;
            }
            else
            {
                activeEvents.Add(new ActiveEventState(eventId, duration));
            }
        }

        /// <summary>
        /// Agenda um evento interativo para o próximo turno sem proposta.
        /// Pode ser chamado por qualquer sistema: proposta aceita/recusada, ação no celular,
        /// relação com NPC, calendário, outro evento, etc.
        /// </summary>
        public void ScheduleEvent(string eventId)
        {
            if (!string.IsNullOrEmpty(eventId))
                scheduledEventId = eventId;
        }

        /// <summary>
        /// Agenda um evento interativo para um mês específico do calendário (1..totalMonths).
        /// </summary>
        public void ScheduleEventForMonth(string eventId, int targetMonthIndex)
        {
            if (!string.IsNullOrEmpty(eventId) && targetMonthIndex >= 1)
            {
                scheduledEventsByMonth[targetMonthIndex] = eventId;
            }
        }

        /// <summary>
        /// Agenda um evento interativo para daqui a N meses a partir do mês atual.
        /// </summary>
        public void ScheduleEventInMonths(string eventId, int monthsAhead)
        {
            int currentMonth = calendar != null ? calendar.currentMonthIndex : 1;
            ScheduleEventForMonth(eventId, currentMonth + Math.Max(1, monthsAhead));
        }

        /// <summary>
        /// Verifica se há algum evento agendado para um determinado mês do calendário.
        /// </summary>
        public bool IsEventScheduledOnMonth(int monthIndex, out string eventId)
        {
            eventId = string.Empty;
            return scheduledEventsByMonth != null && scheduledEventsByMonth.TryGetValue(monthIndex, out eventId);
        }

        /// <summary>
        /// Consome e retorna o ID do evento agendado (por mês do calendário ou próximo turno), limpando o campo.
        /// Chamado pelo sistema de fluxo antes de sortear a próxima proposta.
        /// </summary>
        public string ConsumeScheduledEvent()
        {
            // 1. Prioridade: evento agendado especificamente para o mês atual do calendário
            int currentMonth = calendar != null ? calendar.currentMonthIndex : -1;
            if (scheduledEventsByMonth != null && scheduledEventsByMonth.TryGetValue(currentMonth, out var monthEventId) && !string.IsNullOrEmpty(monthEventId))
            {
                scheduledEventsByMonth.Remove(currentMonth);
                scheduledEventId = string.Empty;
                return monthEventId;
            }

            // 2. Evento agendado diretamente para o próximo turno
            var id = scheduledEventId;
            scheduledEventId = string.Empty;
            return id;
        }

        /// <summary>
        /// Inicia um Investimento a Longo Prazo. Se já existir um investimento com o mesmo ID,
        /// reinicia sua contagem.
        /// </summary>
        public void StartInvestment(string investmentId, int durationMonths)
        {
            if (string.IsNullOrEmpty(investmentId) || durationMonths <= 0) return;

            var existing = activeInvestments.Find(i =>
                string.Equals(i.investmentId, investmentId, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
            {
                existing.remainingMonths = durationMonths;
                existing.totalMonths = durationMonths;
                existing.isCancelled = false;
            }
            else
            {
                activeInvestments.Add(new ActiveInvestmentState(investmentId, durationMonths));
            }
        }

        /// <summary>
        /// Cancela um investimento ativo sem disparar seus efeitos de conclusão.
        /// </summary>
        public void CancelInvestment(string investmentId)
        {
            if (string.IsNullOrEmpty(investmentId)) return;
            var inv = activeInvestments.Find(i =>
                string.Equals(i.investmentId, investmentId, StringComparison.OrdinalIgnoreCase));
            if (inv != null) inv.isCancelled = true;
        }

        public ActiveInvestmentState GetInvestment(string investmentId)
        {
            if (string.IsNullOrEmpty(investmentId)) return null;
            return activeInvestments.Find(i =>
                string.Equals(i.investmentId, investmentId, StringComparison.OrdinalIgnoreCase));
        }

        public bool HasPerk(string perkId)
        {
            if (string.IsNullOrEmpty(perkId) || activePerkIds == null) return false;
            foreach (var id in activePerkIds)
            {
                if (string.Equals(id, perkId, StringComparison.OrdinalIgnoreCase))
                {
                    var state = GetActivePerkState(id);
                    if (state != null && state.IsOnCooldown)
                        return false;
                    return true;
                }
            }
            return false;
        }

        public ActivePerkState GetActivePerkState(string perkId)
        {
            if (string.IsNullOrEmpty(perkId) || activePerks == null) return null;
            return activePerks.Find(p => string.Equals(p.perkId, perkId, StringComparison.OrdinalIgnoreCase));
        }

        public void GrantPerk(string perkId, int duration = 0, IReadOnlyDictionary<string, PerkDefinition> perkCatalog = null)
        {
            if (string.IsNullOrEmpty(perkId)) return;

            if (!activePerkIds.Exists(id => string.Equals(id, perkId, StringComparison.OrdinalIgnoreCase)))
            {
                activePerkIds.Add(perkId);
            }

            if (duration == 0 && string.Equals(perkId, "InvestimentoUsina", StringComparison.OrdinalIgnoreCase))
            {
                if (perkCatalog == null || !perkCatalog.TryGetValue(perkId, out var d) || d == null || d.durationMonths > 0)
                {
                    duration = (perkCatalog != null && perkCatalog.TryGetValue(perkId, out var pDef) && pDef != null && pDef.durationMonths > 0)
                        ? pDef.durationMonths
                        : 12;
                }
            }
            else if (duration == 0 && string.Equals(perkId, "AliancaLegislativo", StringComparison.OrdinalIgnoreCase))
            {
                duration = (perkCatalog != null && perkCatalog.TryGetValue(perkId, out var pDef) && pDef != null && pDef.durationMonths > 0)
                    ? pDef.durationMonths
                    : RunCalendar.DefaultTotalMonths;
            }

            var existing = activePerks.Find(p => string.Equals(p.perkId, perkId, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                existing.remainingMonths = duration;
            }
            else
            {
                activePerks.Add(new ActivePerkState(perkId, duration));
            }

            RefreshMaxStatBonuses(perkCatalog);
        }

        public void RemovePerk(string perkId, IReadOnlyDictionary<string, PerkDefinition> perkCatalog = null)
        {
            if (string.IsNullOrEmpty(perkId)) return;

            activePerkIds.Remove(perkId);
            activePerkIds.RemoveAll(id => string.Equals(id, perkId, StringComparison.OrdinalIgnoreCase));
            activePerks.RemoveAll(p => string.Equals(p.perkId, perkId, StringComparison.OrdinalIgnoreCase));
            RefreshMaxStatBonuses(perkCatalog);
        }

        public void RefreshMaxStatBonuses(IReadOnlyDictionary<string, PerkDefinition> perkCatalog = null)
        {
            if (stats == null) return;

            int climateBonus = 0;
            int relationsBonus = 0;
            int approvalBonus = 0;
            int economyBonus = 0;
            int corruptionBonus = 0;

            if (activePerkIds != null)
            {
                foreach (var perkId in activePerkIds)
                {
                    if (string.IsNullOrEmpty(perkId)) continue;

                    PerkDefinition def = null;
                    if (perkCatalog != null)
                    {
                        perkCatalog.TryGetValue(perkId, out def);
                    }

                    if (def != null && def.maxStatBonuses != null &&
                        (def.maxStatBonuses.climaticChanges != 0 ||
                         def.maxStatBonuses.internationalRelations != 0 ||
                         def.maxStatBonuses.popularApproval != 0 ||
                         def.maxStatBonuses.economy != 0 ||
                         def.maxStatBonuses.corruption != 0))
                    {
                        climateBonus += def.maxStatBonuses.climaticChanges;
                        relationsBonus += def.maxStatBonuses.internationalRelations;
                        approvalBonus += def.maxStatBonuses.popularApproval;
                        economyBonus += def.maxStatBonuses.economy;
                        corruptionBonus += def.maxStatBonuses.corruption;
                    }
                    else if (def == null || !def.isEmergencyRescue)
                    {
                        if (string.Equals(perkId, "ReservaFlorestal", StringComparison.OrdinalIgnoreCase))
                            climateBonus += 30;
                        else if (string.Equals(perkId, "ThisIsBrasil", StringComparison.OrdinalIgnoreCase))
                            relationsBonus += 20;
                    }
                }
            }

            stats.SetMaxBonus(StatId.ClimaticChanges, climateBonus);
            stats.SetMaxBonus(StatId.InternationalRelations, relationsBonus);
            stats.SetMaxBonus(StatId.PopularApproval, approvalBonus);
            stats.SetMaxBonus(StatId.Economy, economyBonus);
            stats.SetMaxBonus(StatId.Corruption, corruptionBonus);
        }

        public bool CheckAndApplyEmergencyRescue(out string perkId, out StatId statId, IReadOnlyDictionary<string, PerkDefinition> perkCatalog = null)
        {
            perkId = string.Empty;
            statId = default;

            if (activePerkIds == null || activePerkIds.Count == 0) return false;

            StatId? candidateStat = null;

            foreach (var activeId in activePerkIds)
            {
                if (string.IsNullOrEmpty(activeId)) continue;
                var perkState = GetActivePerkState(activeId);
                if (perkState != null && perkState.IsOnCooldown) continue;

                PerkDefinition def = null;
                if (perkCatalog != null)
                {
                    perkCatalog.TryGetValue(activeId, out def);
                }

                bool isAnyStatRescue = (def != null && def.rescueAnyDepletedStat) ||
                                       string.Equals(activeId, "Imorrivel", StringComparison.OrdinalIgnoreCase);

                if (isAnyStatRescue)
                {
                    if (stats.climaticChanges <= StatBlock.MinValue) candidateStat = StatId.ClimaticChanges;
                    else if (stats.internationalRelations <= StatBlock.MinValue) candidateStat = StatId.InternationalRelations;
                    else if (stats.popularApproval <= StatBlock.MinValue) candidateStat = StatId.PopularApproval;
                    else if (stats.economy <= StatBlock.MinValue) candidateStat = StatId.Economy;
                }
                else
                {
                    bool isClimateRescue = (def != null && def.isEmergencyRescue && def.rescueStat == StatId.ClimaticChanges) ||
                                           string.Equals(activeId, "ReservaFlorestal", StringComparison.OrdinalIgnoreCase);
                    bool isRelationsRescue = (def != null && def.isEmergencyRescue && def.rescueStat == StatId.InternationalRelations) ||
                                             string.Equals(activeId, "AliancaEUA", StringComparison.OrdinalIgnoreCase);
                    bool isEconomyRescue = (def != null && def.isEmergencyRescue && def.rescueStat == StatId.Economy);
                    bool isPopularityRescue = (def != null && def.isEmergencyRescue && def.rescueStat == StatId.PopularApproval);
                    bool isCorruptionRescue = (def != null && def.isEmergencyRescue && def.rescueStat == StatId.Corruption) ||
                                              string.Equals(activeId, "AliancaJudiciario", StringComparison.OrdinalIgnoreCase);

                    if (isClimateRescue && stats.climaticChanges <= StatBlock.MinValue) candidateStat = StatId.ClimaticChanges;
                    else if (isRelationsRescue && stats.internationalRelations <= StatBlock.MinValue) candidateStat = StatId.InternationalRelations;
                    else if (isEconomyRescue && stats.economy <= StatBlock.MinValue) candidateStat = StatId.Economy;
                    else if (isPopularityRescue && stats.popularApproval <= StatBlock.MinValue) candidateStat = StatId.PopularApproval;
                    else if (isCorruptionRescue && stats.corruption >= stats.GetMax(StatId.Corruption)) candidateStat = StatId.Corruption;
                }

                if (candidateStat.HasValue)
                {
                    var rescued = CheckAndApplyEmergencyRescue(perkCatalog);
                    if (rescued != null && rescued.Count > 0)
                    {
                        perkId = rescued[0];
                        statId = candidateStat.Value;
                        return true;
                    }
                }
            }

            return false;
        }

        public List<string> CheckAndApplyEmergencyRescue(IReadOnlyDictionary<string, PerkDefinition> perkCatalog = null)
        {
            var rescuedPerkIds = new List<string>();
            if (activePerkIds == null || activePerkIds.Count == 0) return rescuedPerkIds;

            // Itera sobre cópia para permitir remoção segura de perks consumidos
            var activeList = new List<string>(activePerkIds);

            foreach (var perkId in activeList)
            {
                if (string.IsNullOrEmpty(perkId)) continue;

                var perkState = GetActivePerkState(perkId);
                if (perkState != null && perkState.IsOnCooldown) continue;

                PerkDefinition def = null;
                if (perkCatalog != null)
                {
                    perkCatalog.TryGetValue(perkId, out def);
                }

                bool isAnyStatRescue = (def != null && def.rescueAnyDepletedStat) ||
                                       string.Equals(perkId, "Imorrivel", StringComparison.OrdinalIgnoreCase);

                if (isAnyStatRescue)
                {
                    bool anyDepleted = stats.climaticChanges <= StatBlock.MinValue ||
                                       stats.internationalRelations <= StatBlock.MinValue ||
                                       stats.popularApproval <= StatBlock.MinValue ||
                                       stats.economy <= StatBlock.MinValue;

                    if (anyDepleted)
                    {
                        int restoreVal = (def != null && def.rescueRestoreValue > 0) ? def.rescueRestoreValue : 1;
                        if (stats.climaticChanges <= StatBlock.MinValue) stats.climaticChanges = restoreVal;
                        if (stats.internationalRelations <= StatBlock.MinValue) stats.internationalRelations = restoreVal;
                        if (stats.popularApproval <= StatBlock.MinValue) stats.popularApproval = restoreVal;
                        if (stats.economy <= StatBlock.MinValue) stats.economy = restoreVal;

                        int recharge = (def != null && def.rechargeMonths > 0) ? def.rechargeMonths : 18;
                        if (recharge > 0 && perkState != null)
                        {
                            perkState.cooldownMonths = recharge;
                            perkState.timesUsed++;
                        }
                        else
                        {
                            RemovePerk(perkId, perkCatalog);
                        }

                        rescuedPerkIds.Add(perkId);
                    }
                    continue;
                }

                bool isClimateRescue = (def != null && def.isEmergencyRescue && def.rescueStat == StatId.ClimaticChanges) ||
                                       string.Equals(perkId, "ReservaFlorestal", StringComparison.OrdinalIgnoreCase);

                bool isRelationsRescue = (def != null && def.isEmergencyRescue && def.rescueStat == StatId.InternationalRelations) ||
                                         string.Equals(perkId, "AliancaEUA", StringComparison.OrdinalIgnoreCase);

                bool isEconomyRescue = (def != null && def.isEmergencyRescue && def.rescueStat == StatId.Economy);
                bool isPopularityRescue = (def != null && def.isEmergencyRescue && def.rescueStat == StatId.PopularApproval);
                bool isCorruptionRescue = (def != null && def.isEmergencyRescue && def.rescueStat == StatId.Corruption) ||
                                          string.Equals(perkId, "AliancaJudiciario", StringComparison.OrdinalIgnoreCase);

                if (isClimateRescue && stats.climaticChanges <= StatBlock.MinValue)
                {
                    int restoreVal = (def != null && def.rescueRestoreValue > 0) ? def.rescueRestoreValue : 35;
                    stats.climaticChanges = restoreVal;
                    ConsumeOrRechargePerk(perkId, def, perkState, perkCatalog);
                    rescuedPerkIds.Add(perkId);
                }
                else if (isRelationsRescue && stats.internationalRelations <= StatBlock.MinValue)
                {
                    int restoreVal = (def != null && def.rescueRestoreValue > 0) ? def.rescueRestoreValue : 30;
                    stats.internationalRelations = restoreVal;
                    ConsumeOrRechargePerk(perkId, def, perkState, perkCatalog);
                    rescuedPerkIds.Add(perkId);
                }
                else if (isEconomyRescue && stats.economy <= StatBlock.MinValue)
                {
                    int restoreVal = (def != null && def.rescueRestoreValue > 0) ? def.rescueRestoreValue : 30;
                    stats.economy = restoreVal;
                    ConsumeOrRechargePerk(perkId, def, perkState, perkCatalog);
                    rescuedPerkIds.Add(perkId);
                }
                else if (isPopularityRescue && stats.popularApproval <= StatBlock.MinValue)
                {
                    int restoreVal = (def != null && def.rescueRestoreValue > 0) ? def.rescueRestoreValue : 30;
                    stats.popularApproval = restoreVal;
                    ConsumeOrRechargePerk(perkId, def, perkState, perkCatalog);
                    rescuedPerkIds.Add(perkId);
                }
                else if (isCorruptionRescue && stats.corruption >= stats.GetMax(StatId.Corruption))
                {
                    int restoreVal = (def != null && def.rescueRestoreValue > 0) ? def.rescueRestoreValue : 65;
                    stats.corruption = restoreVal;
                    ConsumeOrRechargePerk(perkId, def, perkState, perkCatalog);
                    rescuedPerkIds.Add(perkId);
                }
            }

            if (rescuedPerkIds.Count > 0)
            {
                UpdateTermination();
            }

            return rescuedPerkIds;
        }

        private void ConsumeOrRechargePerk(string perkId, PerkDefinition def, ActivePerkState perkState, IReadOnlyDictionary<string, PerkDefinition> perkCatalog)
        {
            if (def != null && def.rechargeMonths > 0 && perkState != null)
            {
                perkState.cooldownMonths = def.rechargeMonths;
                perkState.timesUsed++;
            }
            else
            {
                RemovePerk(perkId, perkCatalog);
            }
        }

        public bool HasPopularityLossToRelationsConversion(IReadOnlyDictionary<string, PerkDefinition> perkCatalog = null)
        {
            if (activePerkIds == null || activePerkIds.Count == 0) return false;
            foreach (var perkId in activePerkIds)
            {
                if (string.Equals(perkId, "GrandeIrmao", StringComparison.OrdinalIgnoreCase))
                    return true;
                if (perkCatalog != null && perkCatalog.TryGetValue(perkId, out var def) && def != null && def.convertPopularityLossToRelations)
                    return true;
            }
            return false;
        }

        public void ApplyStatDelta(StatId id, int delta, IReadOnlyDictionary<string, PerkDefinition> perkCatalog = null)
        {
            if (!termination.IsOngoing) return;

            if (preventStatLossThisMonth && delta < 0 && id != StatId.Corruption)
            {
                return;
            }

            stats.ApplyDelta(id, delta);

            if (id == StatId.PopularApproval && delta < 0 && HasPopularityLossToRelationsConversion(perkCatalog))
            {
                int relationsGain = Math.Abs(delta) / 2;
                if (relationsGain > 0)
                {
                    stats.ApplyDelta(StatId.InternationalRelations, relationsGain);
                }
            }

            UpdateTermination();
        }

        public void ApplyStatImpacts(StatBlock impacts, bool hasCorruptionMods = false, IReadOnlyDictionary<string, PerkDefinition> perkCatalog = null)
        {
            if (!termination.IsOngoing || impacts == null) return;

            int popDeltaApplied = impacts.popularApproval;
            if (hasCorruptionMods)
            {
                popDeltaApplied += UnityEngine.Mathf.RoundToInt(impacts.corruption * 0.2f);
            }

            if (preventStatLossThisMonth)
            {
                var safeImpacts = impacts.Clone();
                if (safeImpacts.climaticChanges < 0) safeImpacts.climaticChanges = 0;
                if (safeImpacts.economy < 0) safeImpacts.economy = 0;
                if (safeImpacts.internationalRelations < 0) safeImpacts.internationalRelations = 0;
                if (safeImpacts.popularApproval < 0) safeImpacts.popularApproval = 0;
                stats.ApplyImpacts(safeImpacts, hasCorruptionMods);
                popDeltaApplied = Math.Max(0, popDeltaApplied);
            }
            else
            {
                stats.ApplyImpacts(impacts, hasCorruptionMods);
            }

            // O Grande Irmão / Vigiar e Punir: metade da Popularidade perdida é obtida em Relação Internacional
            if (popDeltaApplied < 0 && HasPopularityLossToRelationsConversion(perkCatalog))
            {
                int relationsGain = Math.Abs(popDeltaApplied) / 2;
                if (relationsGain > 0)
                {
                    stats.ApplyDelta(StatId.InternationalRelations, relationsGain);
                }
            }

            UpdateTermination();
        }

        public void ApplyPoliticalDelta(int deltaX, int deltaY)
        {
            if (!termination.IsOngoing) return;

            politicalAxis.ApplyDelta(deltaX, deltaY);
        }

        public (int deltaX, int deltaY) MovePoliticalAxisTowards(int targetX, int targetY, int step)
        {
            if (!termination.IsOngoing) return (0, 0);

            return politicalAxis.MoveTowards(targetX, targetY, step);
        }

        public (int deltaX, int deltaY) MovePoliticalAxisTowardsNpc(NpcDefinition npc, int step)
        {
            if (npc == null || !termination.IsOngoing) return (0, 0);

            return politicalAxis.MoveTowards(npc.politicalBiasX, npc.politicalBiasY, step);
        }

        public (int deltaX, int deltaY) ModifyNpcRelation(
            string npcId,
            int delta,
            IReadOnlyDictionary<string, NpcDefinition> npcCatalog = null,
            bool applyPoliticalMovement = true,
            int politicalStep = PoliticalAxis.DefaultRelationStep)
        {
            if (string.IsNullOrEmpty(npcId)) return (0, 0);

            var npcState = GetOrCreateNpcState(npcId);
            npcState?.ModifyRelation(delta);

            if (applyPoliticalMovement && delta > 0 && npcCatalog != null &&
                npcCatalog.TryGetValue(npcId, out var npcDef) && npcDef != null)
            {
                return MovePoliticalAxisTowards(npcDef.politicalBiasX, npcDef.politicalBiasY, politicalStep);
            }

            return (0, 0);
        }

        public void LockPoliticalAxis() => politicalAxis.Lock();
        public void UnlockPoliticalAxis() => politicalAxis.Unlock();

        public void UnlockAction(string actionId, int charges = 1)
        {
            if (string.IsNullOrEmpty(actionId)) return;

            if (!unlockedActionIds.Contains(actionId))
            {
                unlockedActionIds.Add(actionId);
            }

            if (charges > 0)
            {
                actionCharges[actionId] = charges;
                consumedSingleUseActions.Remove(actionId);
            }
        }

        public void AddActionCharges(string actionId, int charges = 1)
        {
            if (string.IsNullOrEmpty(actionId) || charges <= 0) return;

            if (!unlockedActionIds.Contains(actionId))
            {
                unlockedActionIds.Add(actionId);
            }

            int current = actionCharges.TryGetValue(actionId, out int c) ? c : 0;
            actionCharges[actionId] = current + charges;
            consumedSingleUseActions.Remove(actionId);
        }

        public int GetActionCharges(string actionId)
        {
            if (string.IsNullOrEmpty(actionId)) return 0;
            if (IsActionConsumed(actionId)) return 0;
            return actionCharges.TryGetValue(actionId, out int c) ? c : 1;
        }

        public void LockAction(string actionId)
        {
            if (!string.IsNullOrEmpty(actionId))
            {
                unlockedActionIds.Remove(actionId);
                actionCharges.Remove(actionId);
            }
        }

        public bool IsActionUnlocked(string actionId)
        {
            return !string.IsNullOrEmpty(actionId) && unlockedActionIds.Contains(actionId);
        }

        public bool IsActionConsumed(string actionId)
        {
            return !string.IsNullOrEmpty(actionId) && consumedSingleUseActions.Contains(actionId);
        }

        public bool IsActionOnCooldown(string actionId)
        {
            if (string.IsNullOrEmpty(actionId)) return false;
            return actionCooldowns.TryGetValue(actionId, out int turns) && turns > 0;
        }

        public int GetActionCooldown(string actionId)
        {
            if (string.IsNullOrEmpty(actionId)) return 0;
            return actionCooldowns.TryGetValue(actionId, out int turns) ? turns : 0;
        }

        public void RecordActionUsed(string actionId, FlipPhoneCooldownType cooldownType, int cooldownTurns)
        {
            if (string.IsNullOrEmpty(actionId)) return;

            if (cooldownType == FlipPhoneCooldownType.SingleUse)
            {
                int currentCharges = GetActionCharges(actionId);
                int remaining = Math.Max(0, currentCharges - 1);
                actionCharges[actionId] = remaining;

                if (remaining <= 0 && !consumedSingleUseActions.Contains(actionId))
                {
                    consumedSingleUseActions.Add(actionId);
                }
            }
            else if (cooldownType == FlipPhoneCooldownType.Turns && cooldownTurns > 0)
            {
                actionCooldowns[actionId] = cooldownTurns;
            }
        }

        public void TickActionCooldowns()
        {
            if (actionCooldowns.Count == 0) return;

            var keys = new List<string>(actionCooldowns.Keys);
            foreach (var key in keys)
            {
                if (actionCooldowns[key] > 0)
                {
                    actionCooldowns[key]--;
                    if (actionCooldowns[key] <= 0)
                    {
                        actionCooldowns.Remove(key);
                    }
                }
            }
        }

        public MonthlyEffectsReport AdvanceMonth(
            IReadOnlyDictionary<string, PerkDefinition> perkCatalog = null,
            IReadOnlyDictionary<string, RunEventDefinition> eventCatalog = null,
            IReadOnlyDictionary<string, LongTermInvestmentDefinition> investmentCatalog = null)
        {
            return MonthlyEffectsResolver.ResolveMonth(this, perkCatalog, eventCatalog, investmentCatalog);
        }

        public void ForceDefeat(string reason)
        {
            termination = RunTermination.CreateDefeat(reason);
        }

        public void ForceVictory(string reason = "Mandato Concluído com Sucesso!")
        {
            termination = RunTermination.CreateVictory(reason);
        }

        public void UpdateTermination()
        {
            termination = RunRules.Evaluate(stats, calendar, war != null ? war.outcome : WarOutcome.None);
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
                termination.reason,
                WarRules.CreateSnapshot(this)
            );
        }
    }
}
