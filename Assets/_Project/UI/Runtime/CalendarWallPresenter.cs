using System;
using System.Collections.Generic;
using Mandato.Content;
using Mandato.Core;
using Mandato.Run;
using UnityEngine;
using UnityEngine.UIElements;

namespace Mandato.UI
{
    /// <summary>
    /// Dossier com os dados históricos ou previsões de um mês específico do calendário presidencial.
    /// Mantém compatibilidade com a tipagem anterior.
    /// </summary>
    [Serializable]
    public class CalendarDayDossier
    {
        public int dayNumber;
        public int monthNumber;
        public string dateLabel = string.Empty;
        public string statusText = string.Empty;
        public string statusTagClass = "tag-approved"; // tag-approved, tag-rejected, tag-today, tag-diplomacy

        public bool isEvent = false;

        // Dados de Proposta / Despacho
        public string npcName = string.Empty;
        public string proposalTitle = string.Empty;
        public string proposalDesc = string.Empty;
        public string stat1Text = string.Empty;
        public bool stat1Positive = true;
        public string stat2Text = string.Empty;
        public bool stat2Positive = true;
        public string perkText = string.Empty;

        // Dados de Evento Futuro
        public string eventTitle = string.Empty;
        public string eventDesc = string.Empty;
        public string eventImpact = string.Empty;
    }

    /// <summary>
    /// Presenter diegético do Calendário de Parede do Gabinete Presidencial.
    /// Exibe os 12 meses do ano do mandato selecionado (1º a 4º Ano).
    /// Permite navegar pelos anos do mandato através dos botões de navegação lateral.
    /// Sincroniza dinamicamente as decisões reais, agendamentos futuros e a deliberação na mesa do gabinete.
    /// </summary>
    [DisallowMultipleComponent]
    public class CalendarWallPresenter : MonoBehaviour
    {
        public static CalendarWallPresenter Instance { get; private set; }

        [Header("--- Referências ---")]
        [SerializeField] private UIDocument uiDocument;

        [Header("--- Áudio Opcional ---")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip hoverSound;
        [SerializeField] private AudioClip clickSound;

        [Header("--- Configuração do Mandato ---")]
        [SerializeField] private int currentYear = 2026;
        [SerializeField] private int totalMonthsInMandate = 48;

        private static readonly string[] MonthNames = new string[]
        {
            "JANEIRO", "FEVEREIRO", "MARÇO", "ABRIL", "MAIO", "JUNHO",
            "JULHO", "AGOSTO", "SETEMBRO", "OUTUBRO", "NOVEMBRO", "DEZEMBRO"
        };

        private static readonly string[] MonthShortNames = new string[]
        {
            "JAN", "FEV", "MAR", "ABR", "MAI", "JUN",
            "JUL", "AGO", "SET", "OUT", "NOV", "DEZ"
        };

        // Estado dinâmico do runtime da partida
        private RunState _currentRunState;
        private IReadOnlyDictionary<string, RunEventDefinition> _eventsCatalog;
        private IReadOnlyDictionary<string, PerkDefinition> _perksCatalog;
        private CardDefinition _currentCard;

        // Navegação por anos (1 a 4)
        private int _viewingYearIndex = 1; // 1 = 1º Ano, 2 = 2º Ano, etc.
        private const int TotalYearsInMandate = 4;

        // Elementos da UI
        private VisualElement _calendarPage;
        private Label _yearNumberLabel;
        private Label _yearTitleLabel;
        private Label _yearSubtitleLabel;
        private Button _btnPrevYear;
        private Button _btnNextYear;
        private ScrollView _scheduledEventsList;
        private VisualElement _monthsGrid;

        // Overlay do Tooltip (topo absoluto)
        private VisualElement _tooltipOverlay;
        private VisualElement _tooltipCard;
        private Label _tooltipDateLabel;
        private Label _tooltipStatusTag;
        private VisualElement _tooltipNpcSection;
        private Label _tooltipNpcName;
        private Label _tooltipProposalTitle;
        private Label _tooltipProposalDesc;
        private VisualElement _tooltipStatsSection;
        private Label _tooltipStat1;
        private Label _tooltipStat2;
        private VisualElement _tooltipPerkSection;
        private Label _tooltipPerkText;
        private VisualElement _tooltipEventSection;
        private Label _tooltipEventTitle;
        private Label _tooltipEventDesc;
        private Label _tooltipEventImpact;

        // Dicionário de dossiês dos meses do mandato (1 a 48)
        private readonly Dictionary<int, CalendarDayDossier> _monthDossiers = new Dictionary<int, CalendarDayDossier>();
        private VisualElement _currentlyHoveredCell = null;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            EnsureReferences();
        }

        private void OnEnable()
        {
            EnsureReferences();
            BindUI();
        }

        private void Start()
        {
            EnsureReferences();
            BindUI();
        }

        public void EnsureReferences()
        {
            if (uiDocument == null)
            {
                uiDocument = GetComponent<UIDocument>() ?? GetComponentInChildren<UIDocument>();
            }

            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
            }
        }

        public void BindUI()
        {
            if (uiDocument == null || uiDocument.rootVisualElement == null) return;

            var root = uiDocument.rootVisualElement;
            _calendarPage = root.Q<VisualElement>("calendar-page") ?? root;

            _yearNumberLabel = root.Q<Label>("year-number");
            _yearTitleLabel = root.Q<Label>("year-title");
            _yearSubtitleLabel = root.Q<Label>("year-subtitle");
            _btnPrevYear = root.Q<Button>("btn-prev-year");
            _btnNextYear = root.Q<Button>("btn-next-year");
            _scheduledEventsList = root.Q<ScrollView>("scheduled-events-list");
            _monthsGrid = root.Q<VisualElement>("months-grid");

            // Cache dos elementos do overlay de tooltip
            _tooltipOverlay = root.Q<VisualElement>("calendar-tooltip-overlay");
            _tooltipCard = root.Q<VisualElement>("calendar-tooltip-card");
            _tooltipDateLabel = root.Q<Label>("tooltip-date-label");
            _tooltipStatusTag = root.Q<Label>("tooltip-status-tag");
            _tooltipNpcSection = root.Q<VisualElement>("tooltip-npc-section");
            _tooltipNpcName = root.Q<Label>("tooltip-npc-name");
            _tooltipProposalTitle = root.Q<Label>("tooltip-proposal-title");
            _tooltipProposalDesc = root.Q<Label>("tooltip-proposal-desc");
            _tooltipStatsSection = root.Q<VisualElement>("tooltip-stats-section");
            _tooltipStat1 = root.Q<Label>("tooltip-stat-1");
            _tooltipStat2 = root.Q<Label>("tooltip-stat-2");
            _tooltipPerkSection = root.Q<VisualElement>("tooltip-perk-section");
            _tooltipPerkText = root.Q<Label>("tooltip-perk-text");
            _tooltipEventSection = root.Q<VisualElement>("tooltip-event-section");
            _tooltipEventTitle = root.Q<Label>("tooltip-event-title");
            _tooltipEventDesc = root.Q<Label>("tooltip-event-desc");
            _tooltipEventImpact = root.Q<Label>("tooltip-event-impact");

            HideTooltip();

            // Configura botões de navegação de anos
            if (_btnPrevYear != null)
            {
                _btnPrevYear.clickable = null;
                _btnPrevYear.clicked += OnPrevYearClicked;
            }

            if (_btnNextYear != null)
            {
                _btnNextYear.clickable = null;
                _btnNextYear.clicked += OnNextYearClicked;
            }

            // Registra callbacks de hover nos 12 cards de mês da grade anual
            if (_monthsGrid != null)
            {
                for (int slot = 1; slot <= 12; slot++)
                {
                    var card = _monthsGrid.Q<VisualElement>($"month-card-{slot}");
                    if (card != null)
                    {
                        card.UnregisterCallback<PointerEnterEvent>(OnMonthCardPointerEnter);
                        card.UnregisterCallback<PointerLeaveEvent>(OnMonthCardPointerLeave);
                        card.RegisterCallback<PointerEnterEvent>(OnMonthCardPointerEnter);
                        card.RegisterCallback<PointerLeaveEvent>(OnMonthCardPointerLeave);
                    }
                }
            }

            RenderYear(_viewingYearIndex);
        }

        /// <summary>
        /// Sincroniza o calendário com o estado real e atual da partida.
        /// </summary>
        public void SyncWithRun(
            RunState runState,
            IReadOnlyDictionary<string, RunEventDefinition> eventsCatalog = null,
            IReadOnlyDictionary<string, PerkDefinition> perksCatalog = null,
            CardDefinition currentCard = null)
        {
            _currentRunState = runState;
            if (eventsCatalog != null) _eventsCatalog = eventsCatalog;
            if (perksCatalog != null) _perksCatalog = perksCatalog;
            _currentCard = currentCard;

            int activeMonth = _currentRunState != null ? _currentRunState.calendar.currentMonthIndex : 1;
            int activeYear = Mathf.Clamp((activeMonth - 1) / 12 + 1, 1, TotalYearsInMandate);

            // Ajusta o ano visualizado para o ano do mês ativo
            _viewingYearIndex = activeYear;

            RenderYear(_viewingYearIndex);
        }

        /// <summary>
        /// Compatibilidade com chamadas legadas que passavam o índice do mês (1 a 48).
        /// </summary>
        public void RenderMonth(int monthIndex)
        {
            int year = Mathf.Clamp((monthIndex - 1) / 12 + 1, 1, TotalYearsInMandate);
            RenderYear(year);
        }

        /// <summary>
        /// Renderiza o ano selecionado do mandato (1 a 4) com os seus 12 meses correspondentes.
        /// </summary>
        public void RenderYear(int yearIndex)
        {
            _viewingYearIndex = Mathf.Clamp(yearIndex, 1, TotalYearsInMandate);

            int activeMonth = _currentRunState != null ? _currentRunState.calendar.currentMonthIndex : 1;
            int startYear = _currentRunState != null && _currentRunState.calendar != null
                ? _currentRunState.calendar.startYear
                : currentYear;

            int displayYear = startYear + (_viewingYearIndex - 1);
            int startMonthOfThisYear = (_viewingYearIndex - 1) * 12 + 1;
            int endMonthOfThisYear = _viewingYearIndex * 12;

            if (_yearNumberLabel != null)
                _yearNumberLabel.text = displayYear.ToString();

            if (_yearTitleLabel != null)
                _yearTitleLabel.text = $"{_viewingYearIndex}º ANO DO MANDATO";

            if (_yearSubtitleLabel != null)
                _yearSubtitleLabel.text = $"MESES {startMonthOfThisYear:D2} A {endMonthOfThisYear:D2} DE {totalMonthsInMandate} • CRONOGRAMA ANUAL DO GOVERNO";

            if (_btnPrevYear != null)
                _btnPrevYear.SetEnabled(_viewingYearIndex > 1);

            if (_btnNextYear != null)
                _btnNextYear.SetEnabled(_viewingYearIndex < TotalYearsInMandate);

            // Renderiza cada um dos 12 meses na grade
            if (_monthsGrid != null)
            {
                for (int slot = 1; slot <= 12; slot++)
                {
                    int mandateMonth = (_viewingYearIndex - 1) * 12 + slot;
                    var card = _monthsGrid.Q<VisualElement>($"month-card-{slot}");
                    if (card != null)
                    {
                        RenderMonthCard(card, slot, mandateMonth, activeMonth, displayYear);
                    }
                }
            }

            // Renderiza a lista de agendamentos e acontecimentos do ano no painel inferior
            PopulateScheduledEventsList(_viewingYearIndex, activeMonth);
        }

        private void RenderMonthCard(
            VisualElement card,
            int slotIndex,
            int mandateMonth,
            int activeMonth,
            int displayYear)
        {
            string monthName = MonthNames[slotIndex - 1];

            // Atualiza cabeçalho do card
            var titleLabel = card.Q<Label>($"month-card-title-{slotIndex}") ?? card.Q<Label>(className: "month-title-label");
            if (titleLabel != null)
            {
                titleLabel.text = $"{slotIndex:D2} {monthName}";
            }

            var tagLabel = card.Q<Label>($"month-card-tag-{slotIndex}") ?? card.Q<Label>(className: "month-tag-label");
            if (tagLabel != null)
            {
                tagLabel.text = $"MÊS {mandateMonth:D2}";
            }

            // Limpa classes anteriores do card
            card.RemoveFromClassList("month-card--past");
            card.RemoveFromClassList("month-card--current");
            card.RemoveFromClassList("month-card--future");
            card.RemoveFromClassList("month-card--event");

            // Limpa corpo do card
            var body = card.Q<VisualElement>($"month-card-body-{slotIndex}") ?? card.Q<VisualElement>(className: "month-card-body");
            if (body != null)
            {
                body.Clear();
            }

            CalendarDayDossier dossier = null;

            if (mandateMonth < activeMonth)
            {
                // =========================================================================
                // ESTADO 1: MÊS PASSADO (Com carimbo ink stamp APROVADO, VETADO ou EVENTO)
                // =========================================================================
                card.AddToClassList("month-card--past");

                var decision = _currentRunState?.GetDecisionForMonth(mandateMonth);
                if (decision != null)
                {
                    if (decision.isEvent)
                    {
                        if (body != null)
                        {
                            var stamp = new VisualElement();
                            stamp.AddToClassList("month-stamp");
                            stamp.AddToClassList("stamp-event");
                            stamp.pickingMode = PickingMode.Ignore;

                            var stampLabel = new Label("★ EVENTO");
                            stampLabel.AddToClassList("month-stamp-label");
                            stampLabel.pickingMode = PickingMode.Ignore;
                            stamp.Add(stampLabel);
                            body.Add(stamp);

                            var titleLbl = new Label(decision.title);
                            titleLbl.AddToClassList("month-card-decision-title");
                            titleLbl.pickingMode = PickingMode.Ignore;
                            body.Add(titleLbl);

                            if (!string.IsNullOrEmpty(decision.choiceLabel))
                            {
                                var subLbl = new Label(decision.choiceLabel);
                                subLbl.AddToClassList("month-card-decision-sub");
                                subLbl.pickingMode = PickingMode.Ignore;
                                body.Add(subLbl);
                            }
                        }

                        dossier = new CalendarDayDossier
                        {
                            monthNumber = mandateMonth,
                            dateLabel = $"{monthName} DE {displayYear} • MÊS {mandateMonth:D2}",
                            statusText = "EVENTO CONCLUÍDO",
                            statusTagClass = "tag-diplomacy",
                            isEvent = true,
                            eventTitle = decision.title,
                            eventDesc = decision.choiceLabel,
                            eventImpact = $"{decision.stat1Text} {decision.stat2Text}".Trim()
                        };
                    }
                    else
                    {
                        if (body != null)
                        {
                            var stamp = new VisualElement();
                            stamp.AddToClassList("month-stamp");
                            stamp.AddToClassList(decision.isApproved ? "stamp-approved" : "stamp-rejected");
                            stamp.pickingMode = PickingMode.Ignore;

                            var stampLabel = new Label(decision.isApproved ? "APROVADO" : "VETADO");
                            stampLabel.AddToClassList("month-stamp-label");
                            stampLabel.pickingMode = PickingMode.Ignore;
                            stamp.Add(stampLabel);
                            body.Add(stamp);

                            var titleLbl = new Label(decision.title);
                            titleLbl.AddToClassList("month-card-decision-title");
                            titleLbl.pickingMode = PickingMode.Ignore;
                            body.Add(titleLbl);

                            if (!string.IsNullOrEmpty(decision.choiceLabel))
                            {
                                var subLbl = new Label(decision.choiceLabel);
                                subLbl.AddToClassList("month-card-decision-sub");
                                subLbl.pickingMode = PickingMode.Ignore;
                                body.Add(subLbl);
                            }
                        }

                        dossier = new CalendarDayDossier
                        {
                            monthNumber = mandateMonth,
                            dateLabel = $"{monthName} DE {displayYear} • MÊS {mandateMonth:D2}",
                            statusText = decision.isApproved ? "DESPACHADO: APROVADO" : "DESPACHADO: VETADO",
                            statusTagClass = decision.isApproved ? "tag-approved" : "tag-rejected",
                            isEvent = false,
                            npcName = decision.npcName,
                            proposalTitle = decision.title,
                            proposalDesc = decision.choiceLabel,
                            stat1Text = decision.stat1Text,
                            stat1Positive = decision.stat1Positive,
                            stat2Text = decision.stat2Text,
                            stat2Positive = decision.stat2Positive,
                            perkText = decision.perkText
                        };
                    }
                }
                else
                {
                    if (body != null)
                    {
                        var stamp = new VisualElement();
                        stamp.AddToClassList("month-stamp");
                        stamp.AddToClassList("stamp-approved");
                        stamp.pickingMode = PickingMode.Ignore;

                        var stampLabel = new Label("CONCLUÍDO");
                        stampLabel.AddToClassList("month-stamp-label");
                        stampLabel.pickingMode = PickingMode.Ignore;
                        stamp.Add(stampLabel);
                        body.Add(stamp);

                        var titleLbl = new Label("Expediente da União");
                        titleLbl.AddToClassList("month-card-decision-title");
                        titleLbl.pickingMode = PickingMode.Ignore;
                        body.Add(titleLbl);
                    }

                    dossier = new CalendarDayDossier
                    {
                        monthNumber = mandateMonth,
                        dateLabel = $"{monthName} DE {displayYear} • MÊS {mandateMonth:D2}",
                        statusText = "EXPEDIENTE CONCLUÍDO",
                        statusTagClass = "tag-approved",
                        isEvent = false,
                        npcName = "Secretaria-Geral",
                        proposalTitle = "Expediente Presidencial Concluído",
                        proposalDesc = "Atividades ordinárias e despachos administrativos arquivados.",
                        stat1Text = "Mês Encerrado",
                        stat1Positive = true
                    };
                }
            }
            else if (mandateMonth == activeMonth)
            {
                // =========================================================================
                // ESTADO 2: MÊS ATUAL (Destaque proeminente do despacho sob deliberação)
                // =========================================================================
                card.AddToClassList("month-card--current");

                string scheduledThisMonth = string.Empty;
                bool hasEvent = _currentRunState != null && (
                    !string.IsNullOrEmpty(_currentRunState.scheduledEventId) ||
                    _currentRunState.scheduledEventsByMonth.TryGetValue(activeMonth, out scheduledThisMonth)
                );

                if (body != null)
                {
                    var badge = new VisualElement();
                    badge.AddToClassList("current-month-badge");
                    badge.pickingMode = PickingMode.Ignore;

                    var badgeLabel = new Label("● MÊS ATUAL");
                    badgeLabel.AddToClassList("current-month-badge-label");
                    badgeLabel.pickingMode = PickingMode.Ignore;
                    badge.Add(badgeLabel);
                    body.Add(badge);
                }

                if (hasEvent)
                {
                    string evId = !string.IsNullOrEmpty(_currentRunState.scheduledEventId)
                        ? _currentRunState.scheduledEventId
                        : scheduledThisMonth;
                    string evTitle = ResolveEventTitle(evId);

                    if (body != null)
                    {
                        var titleLbl = new Label(evTitle);
                        titleLbl.AddToClassList("current-proposal-title");
                        titleLbl.pickingMode = PickingMode.Ignore;
                        body.Add(titleLbl);

                        var subLbl = new Label("EVENTO EM ANDAMENTO");
                        subLbl.AddToClassList("current-proposal-sub");
                        subLbl.pickingMode = PickingMode.Ignore;
                        body.Add(subLbl);
                    }

                    dossier = new CalendarDayDossier
                    {
                        monthNumber = mandateMonth,
                        dateLabel = $"{monthName} DE {displayYear} • MÊS ATUAL",
                        statusText = "EVENTO DESTE MÊS",
                        statusTagClass = "tag-today",
                        isEvent = true,
                        eventTitle = evTitle,
                        eventDesc = "Evento extraordinário agendado para o gabinete presidencial.",
                        eventImpact = "Negociações e consequências diretas nos indicadores políticos."
                    };
                }
                else if (_currentCard != null)
                {
                    string npcLabel = _currentCard.npc != null && !string.IsNullOrEmpty(_currentCard.npc.displayName)
                        ? _currentCard.npc.displayName
                        : (_currentCard.GetNpcId() ?? "Gabinete Presidencial");

                    if (body != null)
                    {
                        var titleLbl = new Label(_currentCard.title);
                        titleLbl.AddToClassList("current-proposal-title");
                        titleLbl.pickingMode = PickingMode.Ignore;
                        body.Add(titleLbl);

                        var subLbl = new Label($"EM DELIBERAÇÃO • {npcLabel}");
                        subLbl.AddToClassList("current-proposal-sub");
                        subLbl.pickingMode = PickingMode.Ignore;
                        body.Add(subLbl);
                    }

                    dossier = new CalendarDayDossier
                    {
                        monthNumber = mandateMonth,
                        dateLabel = $"{monthName} DE {displayYear} • MÊS ATUAL",
                        statusText = "EM DELIBERAÇÃO",
                        statusTagClass = "tag-today",
                        isEvent = false,
                        npcName = npcLabel,
                        proposalTitle = _currentCard.title,
                        proposalDesc = _currentCard.description,
                        stat1Text = "Aguardando Decisão (Carimbo A/D)",
                        stat1Positive = true
                    };
                }
                else
                {
                    if (body != null)
                    {
                        var titleLbl = new Label("Despacho em Análise");
                        titleLbl.AddToClassList("current-proposal-title");
                        titleLbl.pickingMode = PickingMode.Ignore;
                        body.Add(titleLbl);

                        var subLbl = new Label("EM DELIBERAÇÃO");
                        subLbl.AddToClassList("current-proposal-sub");
                        subLbl.pickingMode = PickingMode.Ignore;
                        body.Add(subLbl);
                    }

                    dossier = new CalendarDayDossier
                    {
                        monthNumber = mandateMonth,
                        dateLabel = $"{monthName} DE {displayYear} • MÊS ATUAL",
                        statusText = "EM DELIBERAÇÃO",
                        statusTagClass = "tag-today",
                        isEvent = false,
                        npcName = "Mesa Presidencial",
                        proposalTitle = "Despacho em Análise",
                        proposalDesc = "Avalie a proposta na mesa do gabinete presidencial.",
                        stat1Text = "Decisão Pendente",
                        stat1Positive = true
                    };
                }
            }
            else
            {
                // =========================================================================
                // ESTADO 3: MÊS FUTURO (Agenda aberta ou Evento Agendado)
                // =========================================================================
                card.AddToClassList("month-card--future");

                if (_currentRunState != null &&
                    _currentRunState.scheduledEventsByMonth.TryGetValue(mandateMonth, out string schedEventId))
                {
                    card.AddToClassList("month-card--event");
                    string evTitle = ResolveEventTitle(schedEventId);

                    if (body != null)
                    {
                        var badge = new VisualElement();
                        badge.AddToClassList("future-event-badge");
                        badge.pickingMode = PickingMode.Ignore;

                        var badgeLabel = new Label("★ EVENTO AGENDADO");
                        badgeLabel.AddToClassList("future-event-badge-label");
                        badgeLabel.pickingMode = PickingMode.Ignore;
                        badge.Add(badgeLabel);
                        body.Add(badge);

                        var titleLbl = new Label(evTitle);
                        titleLbl.AddToClassList("future-event-title");
                        titleLbl.pickingMode = PickingMode.Ignore;
                        body.Add(titleLbl);
                    }

                    dossier = new CalendarDayDossier
                    {
                        monthNumber = mandateMonth,
                        dateLabel = $"{monthName} DE {displayYear} • PREVISÃO",
                        statusText = "EVENTO AGENDADO",
                        statusTagClass = "tag-diplomacy",
                        isEvent = true,
                        eventTitle = evTitle,
                        eventDesc = "Evento programado no cronograma oficial da presidência.",
                        eventImpact = "Audiências e decisões com lideranças nacionais."
                    };
                }
                else
                {
                    if (body != null)
                    {
                        var openLabel = new Label("AGENDA ABERTA");
                        openLabel.AddToClassList("future-open-agenda");
                        openLabel.pickingMode = PickingMode.Ignore;
                        body.Add(openLabel);

                        var openSub = new Label("Pauta em definição");
                        openSub.AddToClassList("future-open-sub");
                        openSub.pickingMode = PickingMode.Ignore;
                        body.Add(openSub);
                    }

                    dossier = new CalendarDayDossier
                    {
                        monthNumber = mandateMonth,
                        dateLabel = $"{monthName} DE {displayYear} • FUTURO",
                        statusText = "AGENDA FUTURA",
                        statusTagClass = "tag-approved",
                        isEvent = false,
                        npcName = "Gabinete Presidencial",
                        proposalTitle = "Calendário Aberto",
                        proposalDesc = "Acontecimentos e novas propostas serão pautados conforme o avanço do mandato."
                    };
                }
            }

            if (dossier != null)
            {
                _monthDossiers[mandateMonth] = dossier;
            }
        }

        private void PopulateScheduledEventsList(int yearIndex, int activeMonth)
        {
            if (_scheduledEventsList == null) return;
            _scheduledEventsList.Clear();

            int startMonthOfThisYear = (yearIndex - 1) * 12 + 1;
            int endMonthOfThisYear = yearIndex * 12;

            int itemsAdded = 0;

            // 1. Proposta atual sob deliberação (se o mês ativo cair neste ano visualizado)
            if (activeMonth >= startMonthOfThisYear && activeMonth <= endMonthOfThisYear)
            {
                if (_currentCard != null)
                {
                    string npcStr = _currentCard.npc != null && !string.IsNullOrEmpty(_currentCard.npc.displayName)
                        ? _currentCard.npc.displayName
                        : (_currentCard.GetNpcId() ?? "Gabinete");

                    AddScheduleListItem(
                        $"{((activeMonth - 1) % 12) + 1:D2}",
                        MonthAbbreviation(activeMonth),
                        "DELIBERAÇÃO",
                        "badge-deadline",
                        "MÊS ATUAL",
                        _currentCard.title,
                        $"Proposta sob despacho presidencial ({npcStr}).",
                        "item--deadline"
                    );
                    itemsAdded++;
                }

                // Evento imediato agendado para este mês
                string nextEventId = _currentRunState?.scheduledEventId;
                if (!string.IsNullOrEmpty(nextEventId))
                {
                    AddScheduleListItem(
                        $"{((activeMonth - 1) % 12) + 1:D2}",
                        MonthAbbreviation(activeMonth),
                        "EVENTO DO MÊS",
                        "badge-diplomacy",
                        "IMINENTE",
                        ResolveEventTitle(nextEventId),
                        "Evento especial agendado para o gabinete presidencial.",
                        "item--diplomacy"
                    );
                    itemsAdded++;
                }
            }

            // 2. Eventos agendados nos meses deste ano
            if (_currentRunState != null && _currentRunState.scheduledEventsByMonth != null)
            {
                foreach (var kvp in _currentRunState.scheduledEventsByMonth)
                {
                    int m = kvp.Key;
                    if (m >= startMonthOfThisYear && m <= endMonthOfThisYear)
                    {
                        int diff = m - activeMonth;
                        string timeTxt = diff == 0 ? "ESTE MÊS" : (diff > 0 ? (diff == 1 ? "PRÓXIMO MÊS" : $"EM {diff} MESES") : "PASSADO");
                        AddScheduleListItem(
                            $"{((m - 1) % 12) + 1:D2}",
                            MonthAbbreviation(m),
                            "AGENDAMENTO",
                            "badge-congress",
                            timeTxt,
                            ResolveEventTitle(kvp.Value),
                            $"Evento confirmado no calendário oficial para o Mês {m:D2}.",
                            "item--congress"
                        );
                        itemsAdded++;
                    }
                }
            }

            // 3. Decisões tomadas nos meses deste ano (histórico do ano)
            if (_currentRunState != null && _currentRunState.pastDecisions != null)
            {
                foreach (var dec in _currentRunState.pastDecisions)
                {
                    if (dec.monthIndex >= startMonthOfThisYear && dec.monthIndex <= endMonthOfThisYear)
                    {
                        string badgeTxt = dec.isEvent ? "EVENTO" : (dec.isApproved ? "APROVADO" : "VETADO");
                        string badgeCls = dec.isEvent ? "badge-diplomacy" : (dec.isApproved ? "badge-congress" : "badge-crisis");
                        string itemCls = dec.isEvent ? "item--diplomacy" : (dec.isApproved ? "item--congress" : "item--crisis");

                        AddScheduleListItem(
                            $"{((dec.monthIndex - 1) % 12) + 1:D2}",
                            MonthAbbreviation(dec.monthIndex),
                            badgeTxt,
                            badgeCls,
                            "CONCLUÍDO",
                            dec.title,
                            $"Despacho: {dec.choiceLabel}. {dec.stat1Text} {dec.stat2Text}".Trim(),
                            itemCls
                        );
                        itemsAdded++;
                    }
                }
            }

            // 4. Perks ativos do governo (se estivermos no ano ativo)
            if (activeMonth >= startMonthOfThisYear && activeMonth <= endMonthOfThisYear && _currentRunState?.activePerks != null)
            {
                foreach (var perk in _currentRunState.activePerks)
                {
                    string pTitle = ResolvePerkTitle(perk.perkId);
                    string pTime = perk.remainingMonths > 0 ? $"{perk.remainingMonths} MESES" : "PERMANENTE";
                    AddScheduleListItem(
                        "★",
                        "PRK",
                        "DIRETRIZ ATIVA",
                        "badge-congress",
                        pTime,
                        pTitle,
                        "Modificador governamental em vigor.",
                        "item--congress"
                    );
                    itemsAdded++;
                }
            }

            // Item padrão se a lista estiver vazia
            if (itemsAdded == 0)
            {
                AddScheduleListItem(
                    "--",
                    MonthAbbreviation(startMonthOfThisYear),
                    "ROTINA",
                    "badge-congress",
                    "ORDINÁRIO",
                    "Expediente Administrativo da União",
                    "Despachos de rotina e acompanhamento de indicadores nacionais.",
                    "item--congress"
                );
            }
        }

        private void AddScheduleListItem(
            string dayTxt,
            string monTxt,
            string badgeText,
            string badgeClass,
            string timeText,
            string nameText,
            string detailsText,
            string itemClass = "item--diplomacy")
        {
            var item = new VisualElement();
            item.AddToClassList("schedule-item");
            item.AddToClassList(itemClass);
            item.pickingMode = PickingMode.Ignore;

            var dateCol = new VisualElement();
            dateCol.AddToClassList("schedule-date-col");
            dateCol.pickingMode = PickingMode.Ignore;
            var dayLabel = new Label(dayTxt);
            dayLabel.AddToClassList("schedule-day-txt");
            dayLabel.pickingMode = PickingMode.Ignore;
            var monLabel = new Label(monTxt);
            monLabel.AddToClassList("schedule-mon-txt");
            monLabel.pickingMode = PickingMode.Ignore;
            dateCol.Add(dayLabel);
            dateCol.Add(monLabel);

            var infoCol = new VisualElement();
            infoCol.AddToClassList("schedule-info-col");
            infoCol.pickingMode = PickingMode.Ignore;

            var badgeRow = new VisualElement();
            badgeRow.AddToClassList("schedule-badge-row");
            badgeRow.pickingMode = PickingMode.Ignore;

            var badgePill = new Label(badgeText);
            badgePill.AddToClassList("badge-pill");
            badgePill.AddToClassList(badgeClass);
            badgePill.pickingMode = PickingMode.Ignore;

            var timeLabel = new Label(timeText);
            timeLabel.AddToClassList("badge-time");
            timeLabel.pickingMode = PickingMode.Ignore;

            badgeRow.Add(badgePill);
            badgeRow.Add(timeLabel);

            var nameLabel = new Label(nameText);
            nameLabel.AddToClassList("schedule-name");
            nameLabel.pickingMode = PickingMode.Ignore;

            var detailsLabel = new Label(detailsText);
            detailsLabel.AddToClassList("schedule-details");
            detailsLabel.pickingMode = PickingMode.Ignore;

            infoCol.Add(badgeRow);
            infoCol.Add(nameLabel);
            infoCol.Add(detailsLabel);

            item.Add(dateCol);
            item.Add(infoCol);

            _scheduledEventsList.Add(item);
        }

        private string ResolveEventTitle(string eventId)
        {
            if (string.IsNullOrEmpty(eventId)) return "Evento Presidencial";
            if (eventId.Equals("FestaCorporativa", StringComparison.OrdinalIgnoreCase))
                return "Festa Corporativa";
            if (_eventsCatalog != null && _eventsCatalog.TryGetValue(eventId, out var evDef) && evDef != null)
                return evDef.title;
            return eventId;
        }

        private string ResolvePerkTitle(string perkId)
        {
            if (string.IsNullOrEmpty(perkId)) return "Diretriz Governamental";
            if (_perksCatalog != null && _perksCatalog.TryGetValue(perkId, out var pDef) && pDef != null)
                return pDef.title;
            return perkId;
        }

        private string MonthAbbreviation(int monthIndex)
        {
            if (monthIndex < 1) monthIndex = 1;
            int idx = (monthIndex - 1) % 12;
            return MonthShortNames[idx];
        }

        private void OnMonthCardPointerEnter(PointerEnterEvent evt)
        {
            if (evt.currentTarget is VisualElement card)
            {
                int slot = ExtractMonthSlot(card.name);
                if (slot >= 1 && slot <= 12)
                {
                    int mandateMonth = (_viewingYearIndex - 1) * 12 + slot;
                    ShowTooltipForMonth(mandateMonth, card);
                }
            }
        }

        private void OnMonthCardPointerLeave(PointerLeaveEvent evt)
        {
            if (evt.currentTarget is VisualElement card && card == _currentlyHoveredCell)
            {
                HideTooltip();
            }
        }

        private int ExtractMonthSlot(string cardName)
        {
            if (string.IsNullOrEmpty(cardName)) return 0;
            if (cardName.StartsWith("month-card-") && int.TryParse(cardName.Substring(11), out int num))
            {
                return num;
            }
            return 0;
        }

        public void ShowTooltipForMonth(int mandateMonth, VisualElement card)
        {
            if (_tooltipOverlay == null || _tooltipCard == null || card == null) return;

            _currentlyHoveredCell = card;

            if (!_monthDossiers.TryGetValue(mandateMonth, out var dossier))
            {
                int monthInYear = (mandateMonth - 1) % 12;
                dossier = new CalendarDayDossier
                {
                    monthNumber = mandateMonth,
                    dateLabel = $"{MonthNames[monthInYear]} • MÊS {mandateMonth:D2}",
                    statusText = "EXPEDIENTE ORDINÁRIO",
                    statusTagClass = "tag-approved",
                    isEvent = false,
                    npcName = "Gabinete Presidencial",
                    proposalTitle = "Rotina Administrativa",
                    proposalDesc = "Atividades ordinárias de governo e acompanhamento de políticas públicas."
                };
            }

            PopulateTooltipData(dossier);
            PositionTooltipOverlay(card);

            _tooltipOverlay.style.display = DisplayStyle.Flex;
            _tooltipOverlay.RemoveFromClassList("hidden");

            if (_calendarPage != null && _tooltipOverlay.parent == _calendarPage &&
                _calendarPage.IndexOf(_tooltipOverlay) < _calendarPage.childCount - 1)
            {
                _tooltipOverlay.BringToFront();
            }

            if (audioSource != null && hoverSound != null)
            {
                audioSource.PlayOneShot(hoverSound, 0.4f);
            }
        }

        public void HideTooltip()
        {
            _currentlyHoveredCell = null;
            if (_tooltipOverlay != null)
            {
                _tooltipOverlay.style.display = DisplayStyle.None;
                _tooltipOverlay.AddToClassList("hidden");
            }
        }

        private void PositionTooltipOverlay(VisualElement card)
        {
            if (_tooltipCard == null || card == null) return;

            float panelWidth = 1024f;
            float panelHeight = 1448f;
            if (_tooltipOverlay != null && _tooltipOverlay.layout.width > 10f)
            {
                panelWidth = _tooltipOverlay.layout.width;
                panelHeight = _tooltipOverlay.layout.height;
            }

            const float tooltipWidth = 350f;
            const float tooltipHeight = 280f;

            Rect cardRect = card.worldBound;
            float cardX, cardY, cardW;

            if (_tooltipOverlay != null && cardRect.width > 1f)
            {
                Vector2 localPos = _tooltipOverlay.WorldToLocal(new Vector2(cardRect.x, cardRect.y));
                cardX = localPos.x;
                cardY = localPos.y;
                cardW = cardRect.width;
            }
            else
            {
                cardX = card.layout.x > 0 ? card.layout.x : 200f;
                cardY = card.layout.y > 0 ? card.layout.y : 350f;
                cardW = card.layout.width > 1f ? card.layout.width : 280f;
            }

            float targetX;
            if (cardX < panelWidth * 0.55f)
            {
                targetX = cardX + cardW + 14f;
            }
            else
            {
                targetX = cardX - tooltipWidth - 14f;
            }

            float targetY = cardY - 10f;
            targetX = Mathf.Clamp(targetX, 20f, panelWidth - tooltipWidth - 20f);
            targetY = Mathf.Clamp(targetY, 20f, panelHeight - tooltipHeight - 20f);

            _tooltipCard.style.left = targetX;
            _tooltipCard.style.top = targetY;
        }

        private void PopulateTooltipData(CalendarDayDossier dossier)
        {
            if (_tooltipDateLabel != null)
                _tooltipDateLabel.text = dossier.dateLabel;

            if (_tooltipStatusTag != null)
            {
                _tooltipStatusTag.text = dossier.statusText;
                _tooltipStatusTag.ClearClassList();
                _tooltipStatusTag.AddToClassList("tooltip-status-tag");
                if (!string.IsNullOrEmpty(dossier.statusTagClass))
                {
                    _tooltipStatusTag.AddToClassList(dossier.statusTagClass);
                }
            }

            if (dossier.isEvent)
            {
                SetVisible(_tooltipNpcSection, false);
                SetVisible(_tooltipStatsSection, false);
                SetVisible(_tooltipPerkSection, false);

                SetVisible(_tooltipEventSection, true);
                if (_tooltipEventTitle != null) _tooltipEventTitle.text = dossier.eventTitle;
                if (_tooltipEventDesc != null) _tooltipEventDesc.text = dossier.eventDesc;
                if (_tooltipEventImpact != null)
                {
                    _tooltipEventImpact.text = dossier.eventImpact;
                    SetVisible(_tooltipEventImpact, !string.IsNullOrEmpty(dossier.eventImpact));
                }
            }
            else
            {
                SetVisible(_tooltipEventSection, false);

                SetVisible(_tooltipNpcSection, true);
                if (_tooltipNpcName != null) _tooltipNpcName.text = dossier.npcName;
                if (_tooltipProposalTitle != null) _tooltipProposalTitle.text = dossier.proposalTitle;
                if (_tooltipProposalDesc != null) _tooltipProposalDesc.text = dossier.proposalDesc;

                bool hasStat1 = !string.IsNullOrEmpty(dossier.stat1Text);
                bool hasStat2 = !string.IsNullOrEmpty(dossier.stat2Text);
                SetVisible(_tooltipStatsSection, hasStat1 || hasStat2);

                if (_tooltipStat1 != null)
                {
                    SetVisible(_tooltipStat1, hasStat1);
                    if (hasStat1)
                    {
                        _tooltipStat1.text = dossier.stat1Text;
                        _tooltipStat1.ClearClassList();
                        _tooltipStat1.AddToClassList("stat-pill");
                        _tooltipStat1.AddToClassList(dossier.stat1Positive ? "stat-pill--pos" : "stat-pill--neg");
                    }
                }

                if (_tooltipStat2 != null)
                {
                    SetVisible(_tooltipStat2, hasStat2);
                    if (hasStat2)
                    {
                        _tooltipStat2.text = dossier.stat2Text;
                        _tooltipStat2.ClearClassList();
                        _tooltipStat2.AddToClassList("stat-pill");
                        _tooltipStat2.AddToClassList(dossier.stat2Positive ? "stat-pill--pos" : "stat-pill--neg");
                    }
                }

                bool hasPerk = !string.IsNullOrEmpty(dossier.perkText);
                SetVisible(_tooltipPerkSection, hasPerk);
                if (_tooltipPerkText != null && hasPerk)
                {
                    _tooltipPerkText.text = dossier.perkText;
                }
            }
        }

        private void SetVisible(VisualElement el, bool visible)
        {
            if (el == null) return;
            el.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (visible) el.RemoveFromClassList("hidden");
            else el.AddToClassList("hidden");
        }

        private void OnPrevYearClicked()
        {
            if (_viewingYearIndex > 1)
            {
                _viewingYearIndex--;
                RenderYear(_viewingYearIndex);
                PlayClickSound();
            }
        }

        private void OnNextYearClicked()
        {
            if (_viewingYearIndex < TotalYearsInMandate)
            {
                _viewingYearIndex++;
                RenderYear(_viewingYearIndex);
                PlayClickSound();
            }
        }

        private void PlayClickSound()
        {
            if (audioSource != null && clickSound != null)
            {
                audioSource.PlayOneShot(clickSound, 0.6f);
            }
        }

        /// <summary>
        /// Registra a decisão de uma proposta diretamente no histórico visual do calendário (compatibilidade).
        /// </summary>
        public void RecordProposalDecision(int month, string proposalTitle, string npcName, bool approved, string stat1 = "", bool stat1Pos = true, string stat2 = "", bool stat2Pos = true, string perk = "")
        {
            var dossier = new CalendarDayDossier
            {
                monthNumber = month,
                dateLabel = $"MÊS {month:D2}",
                statusText = approved ? "DESPACHADO: APROVADO" : "DESPACHADO: VETADO",
                statusTagClass = approved ? "tag-approved" : "tag-rejected",
                isEvent = false,
                npcName = npcName,
                proposalTitle = proposalTitle,
                proposalDesc = approved ? "Proposta presidencial aprovada e chancelada para execução nacional." : "Veto presidencial aplicado por conveniência e interesse público.",
                stat1Text = stat1,
                stat1Positive = stat1Pos,
                stat2Text = stat2,
                stat2Positive = stat2Pos,
                perkText = perk
            };

            _monthDossiers[month] = dossier;
            RenderYear(_viewingYearIndex);
        }
    }
}
