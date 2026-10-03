using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Mandato.UI
{
    /// <summary>
    /// Dossier com os dados históricos ou previsões de um dia específico do calendário presidencial.
    /// </summary>
    [Serializable]
    public class CalendarDayDossier
    {
        public int dayNumber;
        public string dateLabel = string.Empty;
        public string statusText = string.Empty;
        public string statusTagClass = "tag-approved"; // tag-approved, tag-rejected, tag-today, tag-crisis, tag-diplomacy, tag-deadline, tag-congress

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
    /// Gerencia o dossiê dos dias, navegação de meses e garante que o popup de hover
    /// seja renderizado em uma camada de overlay dedicada no topo absoluto do visual tree.
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
        [SerializeField] private int currentMonthIndex = 0; // 0 = Janeiro
        [SerializeField] private int totalMonthsInMandate = 48;

        private static readonly string[] MonthNames = new string[]
        {
            "JANEIRO", "FEVEREIRO", "MARÇO", "ABRIL", "MAIO", "JUNHO",
            "JULHO", "AGOSTO", "SETEMBRO", "OUTUBRO", "NOVEMBRO", "DEZEMBRO"
        };

        // Elementos da UI
        private VisualElement _calendarPage;
        private Label _yearLabel;
        private Label _monthNameLabel;
        private Label _monthSubtitleLabel;
        private Button _btnPrevMonth;
        private Button _btnNextMonth;

        // Overlay do Tooltip (sempre o último elemento do DOM = topo absoluto)
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

        // Dicionário de dossiês dos dias
        private readonly Dictionary<int, CalendarDayDossier> _dayDossiers = new Dictionary<int, CalendarDayDossier>();
        private readonly List<VisualElement> _cachedDayCells = new List<VisualElement>();
        private VisualElement _currentlyHoveredCell = null;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            EnsureReferences();
            InitializeDefaultDossiers();
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

            _yearLabel = root.Q<Label>("year-number");
            _monthNameLabel = root.Q<Label>("month-name");
            _monthSubtitleLabel = root.Q<Label>("month-subtitle");
            _btnPrevMonth = root.Q<Button>("btn-prev-month");
            _btnNextMonth = root.Q<Button>("btn-next-month");

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

            // Configura botões de navegação
            if (_btnPrevMonth != null)
            {
                _btnPrevMonth.clickable = null;
                _btnPrevMonth.clicked += OnPrevMonthClicked;
            }

            if (_btnNextMonth != null)
            {
                _btnNextMonth.clickable = null;
                _btnNextMonth.clicked += OnNextMonthClicked;
            }

            // Cache e registro de hover nas 35 células da grade
            _cachedDayCells.Clear();
            var daysGrid = root.Q<VisualElement>("days-grid");
            if (daysGrid != null)
            {
                for (int day = 1; day <= 31; day++)
                {
                    var cell = daysGrid.Q<VisualElement>($"day-cell-{day}");
                    if (cell != null)
                    {
                        _cachedDayCells.Add(cell);

                        // Oculta qualquer tooltip interno legado embutido na célula
                        var legacyTooltip = cell.Q<VisualElement>(className: "day-hover-tooltip");
                        if (legacyTooltip != null)
                        {
                            legacyTooltip.style.display = DisplayStyle.None;
                        }

                        int capturedDay = day;
                        cell.UnregisterCallback<PointerEnterEvent>(OnDayPointerEnter);
                        cell.UnregisterCallback<PointerLeaveEvent>(OnDayPointerLeave);

                        cell.RegisterCallback<PointerEnterEvent>(OnDayPointerEnter);
                        cell.RegisterCallback<PointerLeaveEvent>(OnDayPointerLeave);
                    }
                }
            }

            UpdateMonthDisplay();
        }

        private void OnDayPointerEnter(PointerEnterEvent evt)
        {
            if (evt.currentTarget is VisualElement cell)
            {
                int dayNumber = ExtractDayNumber(cell.name);
                if (dayNumber > 0)
                {
                    ShowTooltipForDay(dayNumber, cell);
                }
            }
        }

        private void OnDayPointerLeave(PointerLeaveEvent evt)
        {
            if (evt.currentTarget is VisualElement cell && cell == _currentlyHoveredCell)
            {
                HideTooltip();
            }
        }

        private int ExtractDayNumber(string cellName)
        {
            if (string.IsNullOrEmpty(cellName)) return 0;
            if (cellName.StartsWith("day-cell-") && int.TryParse(cellName.Substring(9), out int num))
            {
                return num;
            }
            return 0;
        }

        public void ShowTooltipForDay(int dayNumber, VisualElement cell)
        {
            if (_tooltipOverlay == null || _tooltipCard == null || cell == null) return;

            _currentlyHoveredCell = cell;

            // Busca ou gera o dossiê do dia
            if (!_dayDossiers.TryGetValue(dayNumber, out var dossier))
            {
                dossier = GenerateFallbackDossier(dayNumber);
            }

            PopulateTooltipData(dossier);
            PositionTooltipOverlay(cell);

            _tooltipOverlay.style.display = DisplayStyle.Flex;
            _tooltipOverlay.RemoveFromClassList("hidden");

            // Garante que a camada de overlay esteja no final absoluto do container pai
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

        private void PositionTooltipOverlay(VisualElement cell)
        {
            if (_tooltipCard == null || cell == null) return;

            // Medidas do painel/overlay
            float panelWidth = 1024f;
            float panelHeight = 1448f;
            if (_tooltipOverlay != null && _tooltipOverlay.layout.width > 10f)
            {
                panelWidth = _tooltipOverlay.layout.width;
                panelHeight = _tooltipOverlay.layout.height;
            }

            const float tooltipWidth = 330f;
            const float tooltipHeight = 260f; // altura aproximada máxima do card

            // Bounding box da célula convertida com precisão para o espaço local do overlay
            Rect cellRect = cell.worldBound;
            float cellX, cellY, cellW;

            if (_tooltipOverlay != null && cellRect.width > 1f)
            {
                Vector2 localPos = _tooltipOverlay.WorldToLocal(new Vector2(cellRect.x, cellRect.y));
                cellX = localPos.x;
                cellY = localPos.y;
                cellW = cellRect.width;
            }
            else
            {
                cellX = cell.layout.x > 0 ? cell.layout.x : 200f;
                cellY = cell.layout.y > 0 ? cell.layout.y : 350f;
                cellW = cell.layout.width > 1f ? cell.layout.width : 130f;
            }

            // Decisão inteligente de lado (esquerda vs direita) para nunca vazar da folha
            float targetX;
            if (cellX < panelWidth * 0.5f)
            {
                // Células da metade esquerda da grade: posiciona à DIREITA da célula
                targetX = cellX + cellW + 14f;
            }
            else
            {
                // Células da metade direita da grade: posiciona à ESQUERDA da célula
                targetX = cellX - tooltipWidth - 14f;
            }

            // Alinhamento vertical com clamp rigoroso para nunca sumir em cima nem embaixo
            float targetY = cellY - 10f;
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
                // Modo Evento Futuro
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
                // Modo Proposta / Despacho Ordinário
                SetVisible(_tooltipEventSection, false);

                SetVisible(_tooltipNpcSection, true);
                if (_tooltipNpcName != null) _tooltipNpcName.text = dossier.npcName;
                if (_tooltipProposalTitle != null) _tooltipProposalTitle.text = dossier.proposalTitle;
                if (_tooltipProposalDesc != null) _tooltipProposalDesc.text = dossier.proposalDesc;

                // Atributos
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

                // Perk
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

        private void OnPrevMonthClicked()
        {
            if (currentMonthIndex > 0)
            {
                currentMonthIndex--;
                UpdateMonthDisplay();
                PlayClickSound();
            }
        }

        private void OnNextMonthClicked()
        {
            if (currentMonthIndex < totalMonthsInMandate - 1)
            {
                currentMonthIndex++;
                UpdateMonthDisplay();
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

        private void UpdateMonthDisplay()
        {
            int yearOffset = currentMonthIndex / 12;
            int monthInYear = currentMonthIndex % 12;
            int displayYear = currentYear + yearOffset;
            string monthName = MonthNames[monthInYear];

            if (_yearLabel != null) _yearLabel.text = displayYear.ToString();
            if (_monthNameLabel != null) _monthNameLabel.text = monthName;
            if (_monthSubtitleLabel != null)
            {
                int monthNumber1Based = currentMonthIndex + 1;
                int mandateYear = yearOffset + 1;
                _monthSubtitleLabel.text = $"MÊS {monthNumber1Based:D2} DE {totalMonthsInMandate} • {mandateYear}º ANO DO MANDATO • 31 DIAS";
            }
        }

        /// <summary>
        /// Registra a decisão de uma proposta executada na partida no histórico do calendário.
        /// </summary>
        public void RecordProposalDecision(int day, string proposalTitle, string npcName, bool approved, string stat1 = "", bool stat1Pos = true, string stat2 = "", bool stat2Pos = true, string perk = "")
        {
            var dossier = new CalendarDayDossier
            {
                dayNumber = day,
                dateLabel = $"{day:D2} DE {MonthNames[currentMonthIndex % 12]}",
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

            _dayDossiers[day] = dossier;

            // Atualiza visual da célula na grade se encontrada
            if (uiDocument != null && uiDocument.rootVisualElement != null)
            {
                var cell = uiDocument.rootVisualElement.Q<VisualElement>($"day-cell-{day}");
                if (cell != null)
                {
                    cell.AddToClassList("day-cell--past");
                    cell.RemoveFromClassList("day-cell--today");
                    cell.RemoveFromClassList("day-cell--future");

                    // Garante selo de decisão
                    var seal = cell.Q<VisualElement>(className: "day-decision-seal");
                    if (seal != null)
                    {
                        seal.RemoveFromClassList("seal-approved");
                        seal.RemoveFromClassList("seal-rejected");
                        seal.AddToClassList(approved ? "seal-approved" : "seal-rejected");
                    }
                }
            }
        }

        private CalendarDayDossier GenerateFallbackDossier(int day)
        {
            string monthStr = MonthNames[currentMonthIndex % 12];
            if (day < 14)
            {
                return new CalendarDayDossier
                {
                    dayNumber = day,
                    dateLabel = $"{day:D2} DE {monthStr}",
                    statusText = "EXPEDIENTE CONCLUÍDO",
                    statusTagClass = "tag-approved",
                    isEvent = false,
                    npcName = "Secretaria-Geral",
                    proposalTitle = "Rotina Administrativa da União",
                    proposalDesc = "Despachos ordinários de ministérios e acompanhamento de índices nacionais.",
                    stat1Text = "+2 Estabilidade",
                    stat1Positive = true
                };
            }
            else if (day == 14)
            {
                return new CalendarDayDossier
                {
                    dayNumber = 14,
                    dateLabel = "14 DE JANEIRO • HOJE",
                    statusText = "EM DELIBERAÇÃO",
                    statusTagClass = "tag-today",
                    isEvent = false,
                    npcName = "Mesa Presidencial",
                    proposalTitle = "Despacho Pendente de Assinatura",
                    proposalDesc = "Avalie a proposta em análise sobre a mesa do gabinete e decida com carimbo A ou D.",
                    stat1Text = "Aguardando Decisão",
                    stat1Positive = true
                };
            }
            else
            {
                return new CalendarDayDossier
                {
                    dayNumber = day,
                    dateLabel = $"{day:D2} DE {monthStr}",
                    statusText = "AGENDA ABERTA",
                    statusTagClass = "tag-approved",
                    isEvent = false,
                    npcName = "Gabinete Presidencial",
                    proposalTitle = "Dia Livre na Agenda Governamental",
                    proposalDesc = "Nenhuma audiência extraordinária ou evento de crise agendado para esta data."
                };
            }
        }

        private void InitializeDefaultDossiers()
        {
            _dayDossiers.Clear();

            // DIA 01
            _dayDossiers[1] = new CalendarDayDossier
            {
                dayNumber = 1,
                dateLabel = "01 DE JANEIRO",
                statusText = "DESPACHADO: APROVADO",
                statusTagClass = "tag-approved",
                isEvent = false,
                npcName = "Ministro da Economia",
                proposalTitle = "Diretrizes do Novo Mandato",
                proposalDesc = "Aprovação do pacote inicial de governabilidade e metas fiscais do executivo.",
                stat1Text = "+10 Popularidade",
                stat1Positive = true,
                stat2Text = "+5 Estabilidade",
                stat2Positive = true,
                perkText = "★ Perk: Lua de Mel Política"
            };

            // DIA 02
            _dayDossiers[2] = new CalendarDayDossier
            {
                dayNumber = 2,
                dateLabel = "02 DE JANEIRO",
                statusText = "DESPACHADO: VETADO",
                statusTagClass = "tag-rejected",
                isEvent = false,
                npcName = "Deputado Suspeito",
                proposalTitle = "Emendas Secretas de Relator",
                proposalDesc = "Veto integral ao mecanismo orçamentário por ausência de transparência pública.",
                stat1Text = "+5 Popularidade",
                stat1Positive = true,
                stat2Text = "-8 Relações",
                stat2Positive = false
            };

            // DIA 03
            _dayDossiers[3] = new CalendarDayDossier
            {
                dayNumber = 3,
                dateLabel = "03 DE JANEIRO",
                statusText = "DESPACHADO: APROVADO",
                statusTagClass = "tag-approved",
                isEvent = false,
                npcName = "Min. Meio Ambiente",
                proposalTitle = "Operação Floresta Viva",
                proposalDesc = "Fiscalização ambiental ostensiva em terras demarcadas contra o desmatamento ilegal.",
                stat1Text = "+8 Clima",
                stat1Positive = true,
                stat2Text = "-4M Economia",
                stat2Positive = false,
                perkText = "★ Perk: Amazônia Sustentável"
            };

            // DIA 14
            _dayDossiers[14] = new CalendarDayDossier
            {
                dayNumber = 14,
                dateLabel = "14 DE JANEIRO • HOJE",
                statusText = "EM DELIBERAÇÃO",
                statusTagClass = "tag-today",
                isEvent = false,
                npcName = "Mesa Presidencial",
                proposalTitle = "Despacho Pendente de Assinatura",
                proposalDesc = "Avalie a proposta em análise sobre a mesa do gabinete e decida com carimbo A ou D.",
                stat1Text = "Decisão Pendente",
                stat1Positive = true
            };

            // DIA 18: CRISE
            _dayDossiers[18] = new CalendarDayDossier
            {
                dayNumber = 18,
                dateLabel = "18 DE JANEIRO",
                statusText = "EVENTO: CRISE FEDERATIVA",
                statusTagClass = "tag-crisis",
                isEvent = true,
                eventTitle = "Reunião de Emergência com Governadores",
                eventDesc = "Governadores de oposição exigem repasse orçamentário extraordinário para a saúde pública.",
                eventImpact = "Impacto previsto: Tensão nas Relações e Impacto no Orçamento."
            };

            // DIA 22: DIPLOMACIA
            _dayDossiers[22] = new CalendarDayDossier
            {
                dayNumber = 22,
                dateLabel = "22 DE JANEIRO",
                statusText = "EVENTO: DIPLOMACIA",
                statusTagClass = "tag-diplomacy",
                isEvent = true,
                eventTitle = "Cúpula Bilateral EUA - Brasil",
                eventDesc = "Comitiva presidencial norte-americana para debate de tarifas aduaneiras de aço e etanol.",
                eventImpact = "Impacto previsto: Oportunidade Econômica e Alianças Globais."
            };

            // DIA 25: FIM DE PRAZO
            _dayDossiers[25] = new CalendarDayDossier
            {
                dayNumber = 25,
                dateLabel = "25 DE JANEIRO",
                statusText = "FIM DE PRAZO: MORATÓRIA",
                statusTagClass = "tag-deadline",
                isEvent = true,
                eventTitle = "Término da Moratória de Juros",
                eventDesc = "Encerra a vigência do benefício fiscal de emergência concedido a produtores rurais.",
                eventImpact = "Impacto previsto: Normalização da Arrecadação Tributária."
            };

            // DIA 31: CONGRESSO
            _dayDossiers[31] = new CalendarDayDossier
            {
                dayNumber = 31,
                dateLabel = "31 DE JANEIRO",
                statusText = "SESSÃO DO CONGRESSO",
                statusTagClass = "tag-congress",
                isEvent = true,
                eventTitle = "Votação da Meta Fiscal Anual",
                eventDesc = "Votação decisiva no plenário que definirá as diretrizes orçamentárias e limites de gastos.",
                eventImpact = "Impacto previsto: Estabilidade Política e Confiança do Mercado."
            };
        }
    }
}
