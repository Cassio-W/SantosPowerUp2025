using System;
using System.Collections.Generic;
using Mandato.Content;
using Mandato.Core;
using Mandato.Run;
using Mandato.UI;
using UnityEngine;

namespace Mandato.Infrastructure
{
    /// <summary>
    /// Estrutura de dados serializada para exibir em tempo real no Inspector do Unity
    /// todas as variáveis de estado, atributos, calendário, propostas, investimentos e
    /// modificadores rastreados pela partida (RunState e RunStateMachine).
    /// Suporta sincronização bidirecional: você pode alterar Atributos, Calendário e Eixo Político
    /// diretamente no Inspector e o jogo reagirá em tempo real.
    /// </summary>
    [Serializable]
    public class RunInspectorDebug
    {
        [Header("Opções de Edição (Debug In-Game)")]
        [Tooltip("Quando ativado, alterações manuais feitas no Inspector (Sliders de Mês, Atributos, etc.) serão aplicadas diretamente ao jogo.")]
        public bool enableInspectorTwoWaySync = true;

        [Header("1. Atributos da Presidência")]
        [SerializeField] private DebugStatsInspector stats = new DebugStatsInspector();

        [Header("2. Calendário e Data")]
        [SerializeField] private DebugCalendarInspector calendar = new DebugCalendarInspector();

        [Header("3. Carta Sorteada no Mês")]
        [SerializeField] private DebugCardInspector currentCard = new DebugCardInspector();

        [Header("4. Investimentos a Longo Prazo Ativos")]
        [SerializeField] private List<DebugInvestmentEntry> activeInvestments = new List<DebugInvestmentEntry>();

        [Header("5. Perks e Modificadores Ativos")]
        [SerializeField] private List<DebugPerkEntry> activePerks = new List<DebugPerkEntry>();

        [Header("6. Eventos e Guerra")]
        [SerializeField] private DebugEventsAndWarInspector eventsAndWar = new DebugEventsAndWarInspector();

        [Header("7. NPCs e Relações")]
        [SerializeField] private List<DebugNpcEntry> npcRelations = new List<DebugNpcEntry>();

        [Header("8. Estado Geral e Fluxo")]
        [SerializeField] private DebugRunOverviewInspector overview = new DebugRunOverviewInspector();

        public DebugStatsInspector Stats => stats;
        public DebugCalendarInspector Calendar => calendar;
        public DebugCardInspector CurrentCard => currentCard;
        public List<DebugInvestmentEntry> ActiveInvestments => activeInvestments;
        public List<DebugPerkEntry> ActivePerks => activePerks;
        public DebugEventsAndWarInspector EventsAndWar => eventsAndWar;
        public List<DebugNpcEntry> NpcRelations => npcRelations;
        public DebugRunOverviewInspector Overview => overview;

        [NonSerialized] private int _lastKnownMonth = -1;
        [NonSerialized] private int _lastKnownStartYear = -1;
        [NonSerialized] private int _lastKnownTotalMonths = -1;

        [NonSerialized] private int _lastKnownClima = -1;
        [NonSerialized] private int _lastKnownRel = -1;
        [NonSerialized] private int _lastKnownApr = -1;
        [NonSerialized] private int _lastKnownEco = -1;
        [NonSerialized] private int _lastKnownCor = -1;

        [NonSerialized] private int _lastKnownPolX = int.MinValue;
        [NonSerialized] private int _lastKnownPolY = int.MinValue;

        [NonSerialized] private bool _isInitialized = false;

        /// <summary>
        /// Atualiza todas as variáveis serializadas no Inspector a partir do estado atual da partida,
        /// aplicando quaisquer alterações manuais feitas pelo usuário no Inspector de volta ao jogo.
        /// </summary>
        public void UpdateFrom(
            RunStateMachine stateMachine,
            RunCatalog catalog,
            UIModalCoordinator modalCoordinator,
            bool isPlayerHandRaised = false,
            bool isAwaitingSpace = false,
            Action onStatsModified = null,
            Action onCalendarModified = null)
        {
            if (stateMachine == null || stateMachine.RunState == null)
                return;

            var runState = stateMachine.RunState;

            // Inicialização do rastreador de mudanças
            if (!_isInitialized)
            {
                if (runState.calendar != null)
                {
                    _lastKnownMonth = runState.calendar.currentMonthIndex;
                    _lastKnownStartYear = runState.calendar.startYear;
                    _lastKnownTotalMonths = runState.calendar.totalMonths;
                    calendar.currentMonthIndex = _lastKnownMonth;
                    calendar.startYear = _lastKnownStartYear;
                    calendar.totalMonths = _lastKnownTotalMonths;
                }

                if (runState.stats != null)
                {
                    _lastKnownClima = runState.stats.climaticChanges;
                    _lastKnownRel = runState.stats.internationalRelations;
                    _lastKnownApr = runState.stats.popularApproval;
                    _lastKnownEco = runState.stats.economy;
                    _lastKnownCor = runState.stats.corruption;

                    stats.climaticChanges = _lastKnownClima;
                    stats.internationalRelations = _lastKnownRel;
                    stats.popularApproval = _lastKnownApr;
                    stats.economy = _lastKnownEco;
                    stats.corruption = _lastKnownCor;
                }

                if (runState.politicalAxis != null)
                {
                    _lastKnownPolX = runState.politicalAxis.x;
                    _lastKnownPolY = runState.politicalAxis.y;
                    stats.politicalEconomicX = _lastKnownPolX;
                    stats.politicalSocialY = _lastKnownPolY;
                }

                _isInitialized = true;
            }

            // 1. Calendário e Data (Bidirecional)
            if (runState.calendar != null)
            {
                if (enableInspectorTwoWaySync)
                {
                    bool calendarChangedByInspector = false;

                    // Mês Atual
                    if (runState.calendar.currentMonthIndex != _lastKnownMonth)
                    {
                        // O jogo avançou o mês internamente: atualiza o inspector
                        calendar.currentMonthIndex = runState.calendar.currentMonthIndex;
                        _lastKnownMonth = runState.calendar.currentMonthIndex;
                    }
                    else if (calendar.currentMonthIndex != _lastKnownMonth)
                    {
                        // O desenvolvedor alterou o slider no Inspector: atualiza o jogo!
                        int clampedMonth = Mathf.Clamp(calendar.currentMonthIndex, 1, Math.Max(1, runState.calendar.totalMonths));
                        calendar.currentMonthIndex = clampedMonth;
                        runState.calendar.currentMonthIndex = clampedMonth;
                        _lastKnownMonth = clampedMonth;
                        calendarChangedByInspector = true;
                    }

                    // Ano de Início
                    if (runState.calendar.startYear != _lastKnownStartYear)
                    {
                        calendar.startYear = runState.calendar.startYear;
                        _lastKnownStartYear = runState.calendar.startYear;
                    }
                    else if (calendar.startYear != _lastKnownStartYear && calendar.startYear > 1900)
                    {
                        runState.calendar.startYear = calendar.startYear;
                        _lastKnownStartYear = calendar.startYear;
                        calendarChangedByInspector = true;
                    }

                    // Total de Meses
                    if (runState.calendar.totalMonths != _lastKnownTotalMonths)
                    {
                        calendar.totalMonths = runState.calendar.totalMonths;
                        _lastKnownTotalMonths = runState.calendar.totalMonths;
                    }
                    else if (calendar.totalMonths != _lastKnownTotalMonths && calendar.totalMonths > 0)
                    {
                        runState.calendar.totalMonths = calendar.totalMonths;
                        _lastKnownTotalMonths = calendar.totalMonths;
                        calendarChangedByInspector = true;
                    }

                    if (calendarChangedByInspector)
                    {
                        onCalendarModified?.Invoke();
                    }
                }
                else
                {
                    calendar.currentMonthIndex = runState.calendar.currentMonthIndex;
                    calendar.startYear = runState.calendar.startYear;
                    calendar.totalMonths = runState.calendar.totalMonths;
                    _lastKnownMonth = runState.calendar.currentMonthIndex;
                    _lastKnownStartYear = runState.calendar.startYear;
                    _lastKnownTotalMonths = runState.calendar.totalMonths;
                }

                // Atualiza campos informativos derivados
                calendar.monthInYear = runState.calendar.MonthInYear;
                calendar.year = runState.calendar.Year;
                calendar.monthName = runState.calendar.MonthName;
                calendar.displayDate = runState.calendar.DisplayDate;
                calendar.remainingMonths = Math.Max(0, runState.calendar.totalMonths - runState.calendar.currentMonthIndex);
                calendar.isLastMonth = runState.calendar.IsLastMonth;
                calendar.isTermCompleted = runState.calendar.IsTermCompleted;
            }

            // 2. Atributos da Presidência (Bidirecional)
            if (runState.stats != null)
            {
                if (enableInspectorTwoWaySync)
                {
                    bool statsModifiedByInspector = false;

                    // Clima
                    if (runState.stats.climaticChanges != _lastKnownClima)
                    {
                        stats.climaticChanges = runState.stats.climaticChanges;
                        _lastKnownClima = runState.stats.climaticChanges;
                    }
                    else if (stats.climaticChanges != _lastKnownClima)
                    {
                        stats.climaticChanges = Mathf.Clamp(stats.climaticChanges, 0, 100);
                        runState.stats.climaticChanges = stats.climaticChanges;
                        _lastKnownClima = stats.climaticChanges;
                        statsModifiedByInspector = true;
                    }

                    // Relações Internacionais
                    if (runState.stats.internationalRelations != _lastKnownRel)
                    {
                        stats.internationalRelations = runState.stats.internationalRelations;
                        _lastKnownRel = runState.stats.internationalRelations;
                    }
                    else if (stats.internationalRelations != _lastKnownRel)
                    {
                        stats.internationalRelations = Mathf.Clamp(stats.internationalRelations, 0, 100);
                        runState.stats.internationalRelations = stats.internationalRelations;
                        _lastKnownRel = stats.internationalRelations;
                        statsModifiedByInspector = true;
                    }

                    // Aprovação Popular
                    if (runState.stats.popularApproval != _lastKnownApr)
                    {
                        stats.popularApproval = runState.stats.popularApproval;
                        _lastKnownApr = runState.stats.popularApproval;
                    }
                    else if (stats.popularApproval != _lastKnownApr)
                    {
                        stats.popularApproval = Mathf.Clamp(stats.popularApproval, 0, 100);
                        runState.stats.popularApproval = stats.popularApproval;
                        _lastKnownApr = stats.popularApproval;
                        statsModifiedByInspector = true;
                    }

                    // Economia
                    if (runState.stats.economy != _lastKnownEco)
                    {
                        stats.economy = runState.stats.economy;
                        _lastKnownEco = runState.stats.economy;
                    }
                    else if (stats.economy != _lastKnownEco)
                    {
                        stats.economy = Mathf.Clamp(stats.economy, 0, 100);
                        runState.stats.economy = stats.economy;
                        _lastKnownEco = stats.economy;
                        statsModifiedByInspector = true;
                    }

                    // Corrupção
                    if (runState.stats.corruption != _lastKnownCor)
                    {
                        stats.corruption = runState.stats.corruption;
                        _lastKnownCor = runState.stats.corruption;
                    }
                    else if (stats.corruption != _lastKnownCor)
                    {
                        stats.corruption = Mathf.Clamp(stats.corruption, 0, 100);
                        runState.stats.corruption = stats.corruption;
                        _lastKnownCor = stats.corruption;
                        statsModifiedByInspector = true;
                    }

                    if (statsModifiedByInspector)
                    {
                        onStatsModified?.Invoke();
                    }
                }
                else
                {
                    stats.climaticChanges = runState.stats.climaticChanges;
                    stats.internationalRelations = runState.stats.internationalRelations;
                    stats.popularApproval = runState.stats.popularApproval;
                    stats.economy = runState.stats.economy;
                    stats.corruption = runState.stats.corruption;

                    _lastKnownClima = stats.climaticChanges;
                    _lastKnownRel = stats.internationalRelations;
                    _lastKnownApr = stats.popularApproval;
                    _lastKnownEco = stats.economy;
                    _lastKnownCor = stats.corruption;
                }
            }

            // Eixo Político (Bidirecional)
            if (runState.politicalAxis != null)
            {
                if (enableInspectorTwoWaySync)
                {
                    if (runState.politicalAxis.x != _lastKnownPolX)
                    {
                        stats.politicalEconomicX = runState.politicalAxis.x;
                        _lastKnownPolX = runState.politicalAxis.x;
                    }
                    else if (stats.politicalEconomicX != _lastKnownPolX)
                    {
                        stats.politicalEconomicX = Mathf.Clamp(stats.politicalEconomicX, -10, 10);
                        runState.politicalAxis.x = stats.politicalEconomicX;
                        _lastKnownPolX = stats.politicalEconomicX;
                    }

                    if (runState.politicalAxis.y != _lastKnownPolY)
                    {
                        stats.politicalSocialY = runState.politicalAxis.y;
                        _lastKnownPolY = runState.politicalAxis.y;
                    }
                    else if (stats.politicalSocialY != _lastKnownPolY)
                    {
                        stats.politicalSocialY = Mathf.Clamp(stats.politicalSocialY, -10, 10);
                        runState.politicalAxis.y = stats.politicalSocialY;
                        _lastKnownPolY = stats.politicalSocialY;
                    }
                }
                else
                {
                    stats.politicalEconomicX = runState.politicalAxis.x;
                    stats.politicalSocialY = runState.politicalAxis.y;
                    _lastKnownPolX = stats.politicalEconomicX;
                    _lastKnownPolY = stats.politicalSocialY;
                }

                stats.politicalQuadrant = runState.politicalAxis.Quadrant;
                stats.isPoliticalLocked = runState.politicalAxis.isLocked;
            }

            // 3. Carta Sorteada no Mês
            var card = stateMachine.CurrentCard;
            if (card != null)
            {
                currentCard.hasActiveCard = true;
                currentCard.cardId = card.id ?? string.Empty;
                currentCard.title = card.title ?? string.Empty;
                currentCard.npcName = card.npc != null ? card.npc.name : (!string.IsNullOrEmpty(card.npcId) ? card.npcId : "Desconhecido");
                currentCard.npcId = card.GetNpcId();
                currentCard.category = card.categoryTag ?? string.Empty;
                currentCard.description = card.description ?? string.Empty;
                currentCard.leftChoiceLabel = card.leftChoice?.label ?? "Aprovar";
                currentCard.rightChoiceLabel = card.rightChoice?.label ?? "Rejeitar";
                currentCard.isTutorial = card.isTutorial;
                currentCard.isRoutineCard = (card.id == "CRD_FALLBACK_ROUTINE");
            }
            else
            {
                currentCard.hasActiveCard = false;
                currentCard.cardId = "(Nenhuma carta sorteada no momento)";
                currentCard.title = string.Empty;
                currentCard.npcName = string.Empty;
                currentCard.npcId = string.Empty;
                currentCard.category = string.Empty;
                currentCard.description = string.Empty;
                currentCard.leftChoiceLabel = string.Empty;
                currentCard.rightChoiceLabel = string.Empty;
                currentCard.isTutorial = false;
                currentCard.isRoutineCard = false;
            }

            // 4. Investimentos a Longo Prazo Ativos
            activeInvestments.Clear();
            if (runState.activeInvestments != null)
            {
                for (int i = 0; i < runState.activeInvestments.Count; i++)
                {
                    var inv = runState.activeInvestments[i];
                    if (inv == null) continue;

                    string invTitle = inv.investmentId;
                    string costStr = "-";
                    if (catalog?.Investments != null && catalog.Investments.TryGetValue(inv.investmentId, out var def) && def != null)
                    {
                        if (!string.IsNullOrEmpty(def.title)) invTitle = def.title;
                        if (def.costPerMonth != null)
                        {
                            costStr = $"Clima:{def.costPerMonth.climaticChanges} Rel:{def.costPerMonth.internationalRelations} Apr:{def.costPerMonth.popularApproval} Eco:{def.costPerMonth.economy}";
                        }
                    }

                    int elapsed = Math.Max(0, inv.totalMonths - inv.remainingMonths);
                    activeInvestments.Add(new DebugInvestmentEntry
                    {
                        investmentId = inv.investmentId,
                        title = invTitle,
                        remainingMonths = inv.remainingMonths,
                        totalMonths = inv.totalMonths,
                        progress = $"{elapsed}/{inv.totalMonths} meses decorridos ({inv.remainingMonths} restantes)",
                        costPerMonth = costStr,
                        isCancelled = inv.isCancelled,
                        isCompleted = inv.IsCompleted
                    });
                }
            }

            // 5. Perks e Modificadores Ativos
            activePerks.Clear();
            if (runState.activePerks != null)
            {
                for (int i = 0; i < runState.activePerks.Count; i++)
                {
                    var perk = runState.activePerks[i];
                    if (perk == null) continue;

                    string perkTitle = perk.perkId;
                    string perkDeltas = "-";
                    if (catalog?.Perks != null && catalog.Perks.TryGetValue(perk.perkId, out var def) && def != null)
                    {
                        if (!string.IsNullOrEmpty(def.title)) perkTitle = def.title;
                        if (def.statDeltasPerMonth != null)
                        {
                            perkDeltas = $"Clima:{def.statDeltasPerMonth.climaticChanges} Rel:{def.statDeltasPerMonth.internationalRelations} Apr:{def.statDeltasPerMonth.popularApproval} Eco:{def.statDeltasPerMonth.economy}";
                        }
                    }

                    string durationText = perk.remainingMonths <= 0 ? "Permanente" : $"{perk.remainingMonths} meses restantes";
                    activePerks.Add(new DebugPerkEntry
                    {
                        perkId = perk.perkId,
                        title = perkTitle,
                        remainingMonths = perk.remainingMonths,
                        timesUsed = perk.timesUsed,
                        duration = durationText,
                        monthlyDeltas = perkDeltas
                    });
                }
            }

            // 6. Eventos e Guerra
            eventsAndWar.scheduledNextEventId = runState.scheduledEventId ?? string.Empty;
            if (runState.scheduledEventsByMonth != null && runState.scheduledEventsByMonth.Count > 0)
            {
                var parts = new List<string>();
                foreach (var kvp in runState.scheduledEventsByMonth)
                {
                    parts.Add($"Mês {kvp.Key}: {kvp.Value}");
                }
                eventsAndWar.scheduledEventsByMonthSummary = string.Join(" | ", parts);
            }
            else
            {
                eventsAndWar.scheduledEventsByMonthSummary = "(Nenhum evento agendado por mês)";
            }

            eventsAndWar.activeEvents.Clear();
            if (runState.activeEvents != null)
            {
                for (int i = 0; i < runState.activeEvents.Count; i++)
                {
                    var ev = runState.activeEvents[i];
                    if (ev != null)
                    {
                        eventsAndWar.activeEvents.Add(new DebugActiveEventEntry
                        {
                            eventId = ev.eventId,
                            remainingMonths = ev.remainingMonths
                        });
                    }
                }
            }

            if (runState.war != null)
            {
                eventsAndWar.isWarActive = runState.war.isActive;
                eventsAndWar.warLevel = runState.war.level.ToString();
                eventsAndWar.targetCountryId = runState.war.targetId ?? string.Empty;
                eventsAndWar.warProgress = runState.war.progress;
                eventsAndWar.monthsInWar = runState.war.monthsInWar;
                eventsAndWar.warOutcome = runState.war.outcome.ToString();
                eventsAndWar.warTechnology = runState.war.technology;
                eventsAndWar.warTroops = runState.war.troops;
                eventsAndWar.investedThisMonth = runState.war.investedThisMonth;
            }
            eventsAndWar.pendingWarLevel = runState.pendingWarLevel.ToString();
            eventsAndWar.pendingWarTargetId = runState.pendingWarTargetId ?? string.Empty;

            // 7. NPCs e Relações
            npcRelations.Clear();
            if (runState.npcStates != null)
            {
                foreach (var kvp in runState.npcStates)
                {
                    var npc = kvp.Value;
                    if (npc == null) continue;

                    string npcName = npc.npcId;
                    if (catalog?.Npcs != null && catalog.Npcs.TryGetValue(npc.npcId, out var def) && def != null && !string.IsNullOrEmpty(def.name))
                    {
                        npcName = def.name;
                    }

                    npcRelations.Add(new DebugNpcEntry
                    {
                        npcId = npc.npcId,
                        npcName = npcName,
                        relationScore = npc.relationScore,
                        isMet = npc.isMet,
                        interactions = npc.interactionCount,
                        suspendedMonths = npc.suspendedMonths,
                        isDead = npc.isDead,
                        isRemoved = npc.isRemoved
                    });
                }
            }

            // 8. Estado Geral e Fluxo
            overview.currentPhase = stateMachine.CurrentPhase.ToString();
            overview.interactionContext = modalCoordinator != null ? modalCoordinator.CurrentContext.ToString() : "Nenhum";
            overview.terminationStatus = runState.termination.status.ToString();
            overview.terminationReason = runState.termination.reason ?? string.Empty;
            overview.seed = runState.seed;
            overview.activeCharacterId = runState.activeCharacterId ?? string.Empty;
            overview.preventStatLossThisMonth = runState.preventStatLossThisMonth;
            overview.isPlayerHandRaised = isPlayerHandRaised;
            overview.isAwaitingSpaceForNextNpc = isAwaitingSpace;
            overview.pastDecisionsCount = runState.pastDecisions != null ? runState.pastDecisions.Count : 0;

            if (runState.pastDecisions != null && runState.pastDecisions.Count > 0)
            {
                var last = runState.pastDecisions[runState.pastDecisions.Count - 1];
                string action = last.isApproved ? "APROVADA" : "REJEITADA";
                overview.lastDecisionSummary = $"Mês {last.monthIndex}: [{action}] {last.title} ({last.npcName})";
            }
            else
            {
                overview.lastDecisionSummary = "(Nenhuma decisão tomada ainda)";
            }
        }
    }

    [Serializable]
    public class DebugStatsInspector
    {
        [Tooltip("Impacto ambiental/climático (0 a 100).")]
        [Range(0, 100)] public int climaticChanges = 50;

        [Tooltip("Relações com nações externas (0 a 100).")]
        [Range(0, 100)] public int internationalRelations = 50;

        [Tooltip("Aprovação da população (0 a 100).")]
        [Range(0, 100)] public int popularApproval = 50;

        [Tooltip("Saúde econômica e fundos (0 a 100).")]
        [Range(0, 100)] public int economy = 50;

        [Tooltip("Nível de corrupção do governo (0 a 100).")]
        [Range(0, 100)] public int corruption = 0;

        [Header("Eixo Político")]
        [Tooltip("Eixo Econômico: -10 (Esquerda) a +10 (Direita).")]
        [Range(-10, 10)] public int politicalEconomicX = 0;

        [Tooltip("Eixo Social/Governança: -10 (Libertário) a +10 (Autoritário).")]
        [Range(-10, 10)] public int politicalSocialY = 0;

        public string politicalQuadrant = "Centro";
        public bool isPoliticalLocked = false;
    }

    [Serializable]
    public class DebugCalendarInspector
    {
        [Header("Controles de Tempo")]
        [Tooltip("Mês atual do mandato (1 a 48). Altere este slider para navegar livremente pelos meses.")]
        [Range(1, 48)]
        public int currentMonthIndex = 1;

        [Tooltip("Ano de início do mandato.")]
        public int startYear = 2026;

        [Tooltip("Total de meses do mandato (padrão: 48).")]
        public int totalMonths = 48;

        [Header("Dados Calculados da Data")]
        [Tooltip("Data formatada (MM/AAAA) exibida no monitor e calendários.")]
        public string displayDate = "01/2026";
        public string monthName = "Janeiro";
        public int monthInYear = 1;
        public int year = 2026;
        public int remainingMonths = 48;
        public bool isLastMonth = false;
        public bool isTermCompleted = false;
    }

    [Serializable]
    public class DebugCardInspector
    {
        public bool hasActiveCard = false;
        public string cardId = string.Empty;
        public string title = string.Empty;
        public string npcName = string.Empty;
        public string npcId = string.Empty;
        public string category = string.Empty;
        public string leftChoiceLabel = string.Empty;
        public string rightChoiceLabel = string.Empty;
        public bool isTutorial = false;
        public bool isRoutineCard = false;
        [TextArea(2, 5)] public string description = string.Empty;
    }

    [Serializable]
    public class DebugInvestmentEntry
    {
        public string investmentId = string.Empty;
        public string title = string.Empty;
        public int remainingMonths = 0;
        public int totalMonths = 0;
        public string progress = string.Empty;
        public string costPerMonth = string.Empty;
        public bool isCancelled = false;
        public bool isCompleted = false;
    }

    [Serializable]
    public class DebugPerkEntry
    {
        public string perkId = string.Empty;
        public string title = string.Empty;
        public string duration = string.Empty;
        public int remainingMonths = 0;
        public int timesUsed = 0;
        public string monthlyDeltas = string.Empty;
    }

    [Serializable]
    public class DebugActiveEventEntry
    {
        public string eventId = string.Empty;
        public int remainingMonths = 0;
    }

    [Serializable]
    public class DebugEventsAndWarInspector
    {
        [Header("Eventos")]
        public string scheduledNextEventId = string.Empty;
        public string scheduledEventsByMonthSummary = string.Empty;
        public List<DebugActiveEventEntry> activeEvents = new List<DebugActiveEventEntry>();

        [Header("Guerra")]
        public bool isWarActive = false;
        public string warLevel = "None";
        public string targetCountryId = string.Empty;
        [Range(0, 100)] public int warProgress = 50;
        public int monthsInWar = 0;
        public string warOutcome = "None";
        public int warTechnology = 0;
        public int warTroops = 0;
        public bool investedThisMonth = false;
        public string pendingWarLevel = "None";
        public string pendingWarTargetId = string.Empty;
    }

    [Serializable]
    public class DebugNpcEntry
    {
        public string npcId = string.Empty;
        public string npcName = string.Empty;
        [Range(-100, 100)] public int relationScore = 0;
        public bool isMet = false;
        public int interactions = 0;
        public int suspendedMonths = 0;
        public bool isDead = false;
        public bool isRemoved = false;
    }

    [Serializable]
    public class DebugRunOverviewInspector
    {
        public string currentPhase = "PreparingRun";
        public string interactionContext = "DeskOverview";
        public string terminationStatus = "Ongoing";
        public string terminationReason = string.Empty;
        public int seed = 0;
        public string activeCharacterId = string.Empty;
        public bool preventStatLossThisMonth = false;
        public bool isPlayerHandRaised = false;
        public bool isAwaitingSpaceForNextNpc = false;
        public int pastDecisionsCount = 0;
        public string lastDecisionSummary = string.Empty;
    }
}
