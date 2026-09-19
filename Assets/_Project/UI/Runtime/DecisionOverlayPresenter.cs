using System;
using System.Collections.Generic;
using System.Text;
using Mandato.Content;
using Mandato.Core;
using Mandato.Run;
using UnityEngine;
using UnityEngine.UIElements;

namespace Mandato.UI
{
    /// <summary>
    /// Presenter do overlay de HUD na tela (Data/Calendário, Corrupção, Perks e Botão Voltar).
    /// As decisões de propostas foram migradas integralmente para o sistema diegético de Carimbo 3D no papel.
    /// </summary>
    public class DecisionOverlayPresenter : MonoBehaviour
    {
        public event Action<int> OnChoiceSelected;
        public event Action<ChoiceDefinition> OnChoiceHovered;
        public event Action OnChoiceUnhovered;
        public event Action OnBackRequested;
        public event Action OnBackClicked;

        [SerializeField] private UIDocument uiDocument;
        [SerializeField] private VisualTreeAsset uxmlAsset;
        [SerializeField] private PanelSettings panelSettings;

        private UIModalCoordinator modalCoordinator;
        private VisualElement decisionContainer;
        private VisualElement backContainer;
        private VisualElement vignetteCorruption;
        private VisualElement perksContainer;
        private VisualElement perkModalPopup;
        private Label lblPerkTitle;
        private Label lblPerkTag;
        private Label lblPerkDesc;
        private Label headerDateLabel;
        private Label headerMonthLabel;
        private Button btnBack;
        private CardDefinition currentCard;

        private bool isVisible = true;

        public bool IsVisible => isVisible;

        public void SetModalCoordinator(UIModalCoordinator coordinator)
        {
            if (modalCoordinator != null)
                modalCoordinator.OnContextChanged -= HandleContextChanged;

            modalCoordinator = coordinator;

            if (modalCoordinator != null)
            {
                modalCoordinator.OnContextChanged += HandleContextChanged;
                HandleContextChanged(modalCoordinator.CurrentContext, InteractionContext.DeskOverview);
            }
        }

        private void HandleContextChanged(InteractionContext newContext, InteractionContext oldContext)
        {
            if (newContext == InteractionContext.PaperInspect)
            {
                SetBackVisible(true);
            }
            else if (newContext == InteractionContext.DeskOverview)
            {
                SetBackVisible(false);
            }
        }

        public void SetVisible(bool visible)
        {
            isVisible = visible;
            EnsureReferences();
            CacheElements();

            if (uiDocument?.rootVisualElement != null)
            {
                uiDocument.rootVisualElement.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        private void Awake()
        {
            EnsureReferences();
            CacheElements();
            SetBackVisible(false);
        }

        private void OnEnable()
        {
            EnsureReferences();
            CacheElements();
            SetBackVisible(false);
        }

        private void EnsureReferences()
        {
            if (uiDocument == null)
            {
                uiDocument = GetComponent<UIDocument>() ?? GetComponentInChildren<UIDocument>() ?? gameObject.AddComponent<UIDocument>();
            }

            if (uiDocument.panelSettings == null && panelSettings != null)
            {
                uiDocument.panelSettings = panelSettings;
            }

            if (uiDocument.visualTreeAsset == null && uxmlAsset != null)
            {
                uiDocument.visualTreeAsset = uxmlAsset;
            }

#if UNITY_EDITOR
            if (uiDocument.panelSettings == null)
            {
                var screenPanel = UnityEditor.AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/UI/Decision/DecisionPanelSettings.asset");
                if (screenPanel != null) uiDocument.panelSettings = screenPanel;
            }
            if (uiDocument.visualTreeAsset == null)
            {
                var uxml = UnityEditor.AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/Decision/DecisionUI.uxml");
                if (uxml != null) uiDocument.visualTreeAsset = uxml;
            }
#endif
        }

        private void CacheElements()
        {
            if (uiDocument?.rootVisualElement == null) return;
            var root = uiDocument.rootVisualElement;

            decisionContainer = root.Q<VisualElement>("decision-container");
            backContainer = root.Q<VisualElement>("back-container") ?? decisionContainer;
            vignetteCorruption = root.Q<VisualElement>("vignette-corruption");
            perksContainer = root.Q<VisualElement>("perks-container");
            perkModalPopup = root.Q<VisualElement>("perk-modal-popup");
            lblPerkTitle = root.Q<Label>("perk-modal-title");
            lblPerkTag = root.Q<Label>("perk-modal-tag");
            lblPerkDesc = root.Q<Label>("perk-modal-desc");
            headerDateLabel = root.Q<Label>("header-date-label");
            headerMonthLabel = root.Q<Label>("header-month-label");

            if (perkModalPopup != null)
            {
                perkModalPopup.style.display = DisplayStyle.None;
            }

            btnBack = root.Q<Button>("btn-back") ?? root.Q<Button>("btn-return");
            if (btnBack != null)
            {
                btnBack.clicked -= OnBackButtonClicked;
                btnBack.clicked += OnBackButtonClicked;
            }
        }

        private void OnBackButtonClicked()
        {
            OnBackClicked?.Invoke();
            OnBackRequested?.Invoke();
        }

        public void SetBackVisible(bool visible)
        {
            EnsureReferences();
            CacheElements();

            var target = backContainer ?? decisionContainer;
            if (target != null)
            {
                if (visible)
                {
                    target.RemoveFromClassList("hidden");
                    target.style.display = DisplayStyle.Flex;
                }
                else
                {
                    target.AddToClassList("hidden");
                    target.style.display = DisplayStyle.None;
                }
            }
        }

        public void PresentChoices(CardDefinition card)
        {
            currentCard = card;
            SetBackVisible(false);
        }

        public void ClearChoices()
        {
            currentCard = null;
            OnChoiceUnhovered?.Invoke();

            var target = backContainer ?? decisionContainer;
            if (target != null)
            {
                target.AddToClassList("hidden");
                target.style.display = DisplayStyle.None;
            }
        }

        public void UpdateDateDisplay(string displayDate, int monthIndex = 1, int totalMonths = 48)
        {
            EnsureReferences();
            CacheElements();

            if (headerDateLabel != null && !string.IsNullOrEmpty(displayDate))
                headerDateLabel.text = displayDate;

            if (headerMonthLabel != null)
                headerMonthLabel.text = $"MÊS {monthIndex} DE {totalMonths}";
        }

        public void SetCorruptionLevel(int corruption)
        {
            EnsureReferences();
            CacheElements();
            if (vignetteCorruption == null) return;

            float factor = Mathf.Clamp01((corruption - 25f) / 75f);
            vignetteCorruption.style.opacity = factor * 0.9f;
        }

        public void RefreshActivePerks(IEnumerable<string> activePerkIds, IReadOnlyDictionary<string, PerkDefinition> perkCatalog)
        {
            EnsureReferences();
            CacheElements();

            if (perksContainer == null) return;
            perksContainer.Clear();

            if (activePerkIds == null)
            {
                HidePerkTooltip();
                return;
            }

            foreach (var perkId in activePerkIds)
            {
                if (string.IsNullOrEmpty(perkId)) continue;

                PerkDefinition def = null;
                perkCatalog?.TryGetValue(perkId, out def);

                var itemEl = new VisualElement();
                itemEl.AddToClassList("perk-item");

                var cardEl = new VisualElement();
                cardEl.AddToClassList("perk-icon-card");

                var iconEl = new VisualElement();
                iconEl.AddToClassList("perk-icon-large");

                if (def?.icon != null)
                {
                    iconEl.style.backgroundImage = new StyleBackground(def.icon);
                }

                cardEl.Add(iconEl);
                itemEl.Add(cardEl);

                string capturedId = perkId;
                PerkDefinition capturedDef = def;
                VisualElement capturedCard = cardEl;

                cardEl.RegisterCallback<PointerEnterEvent>(_ => ShowPerkTooltip(capturedId, capturedDef, capturedCard));
                cardEl.RegisterCallback<PointerLeaveEvent>(_ => HidePerkTooltip());

                perksContainer.Add(itemEl);
            }
        }

        private void ShowPerkTooltip(string perkId, PerkDefinition def, VisualElement targetCard)
        {
            if (perkModalPopup == null) return;

            if (lblPerkTitle != null)
                lblPerkTitle.text = !string.IsNullOrEmpty(def?.title) ? def.title : perkId;

            if (lblPerkTag != null)
                lblPerkTag.text = (def != null && def.durationMonths > 0) ? $"{def.durationMonths}M" : "PERK";

            if (lblPerkDesc != null)
                lblPerkDesc.text = FormatPerkDescription(def);

            perkModalPopup.style.left = 124;
            perkModalPopup.style.top = (targetCard != null && targetCard.worldBound.yMin > 0) ? targetCard.worldBound.yMin : 28;

            perkModalPopup.style.display = DisplayStyle.Flex;
            perkModalPopup.AddToClassList("open");
            modalCoordinator?.SetModalState(UIModalCoordinator.MODAL_PERK_TOOLTIP, true);
        }

        private void HidePerkTooltip()
        {
            if (perkModalPopup == null) return;
            perkModalPopup.RemoveFromClassList("open");
            perkModalPopup.style.display = DisplayStyle.None;
            modalCoordinator?.SetModalState(UIModalCoordinator.MODAL_PERK_TOOLTIP, false);
        }

        private string FormatPerkDescription(PerkDefinition def)
        {
            if (def == null) return "Vantagem ativa no mandato.";

            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(def.description))
            {
                sb.AppendLine(def.description);
            }

            if (def.statDeltasPerMonth != null)
            {
                var d = def.statDeltasPerMonth;
                var deltas = new List<string>();

                if (d.economy != 0) deltas.Add($"{(d.economy > 0 ? "+" : "")}{d.economy} Economia/mês");
                if (d.popularApproval != 0) deltas.Add($"{(d.popularApproval > 0 ? "+" : "")}{d.popularApproval} Aprovação/mês");
                if (d.internationalRelations != 0) deltas.Add($"{(d.internationalRelations > 0 ? "+" : "")}{d.internationalRelations} Relações Int./mês");
                if (d.climaticChanges != 0) deltas.Add($"{(d.climaticChanges > 0 ? "+" : "")}{d.climaticChanges} Meio Ambiente/mês");
                if (d.corruption != 0) deltas.Add($"{(d.corruption > 0 ? "+" : "")}{d.corruption} Corrupção/mês");

                if (deltas.Count > 0)
                {
                    if (sb.Length > 0) sb.AppendLine();
                    sb.Append("<b>Efeitos Mensais:</b> " + string.Join(", ", deltas));
                }
            }

            if (def.isEmergencyRescue)
            {
                if (sb.Length > 0) sb.AppendLine();
                string statName = def.rescueStat switch
                {
                    StatId.ClimaticChanges => "Meio Ambiente",
                    StatId.InternationalRelations => "Relações Internacionais",
                    StatId.PopularApproval => "Aprovação Popular",
                    StatId.Economy => "Economia",
                    StatId.Corruption => "Corrupção",
                    _ => "Atributo"
                };
                sb.Append($"🛡️ <b>Prevenção de Derrota:</b> Se o indicador de <b>{statName}</b> chegar a 0, restaura para {def.rescueRestoreValue} e consome este perk.");
            }

            return sb.ToString().TrimEnd();
        }

        private void Update()
        {
            if (!isVisible) return;

            // Atalho de retorno (ESC / Backspace)
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Backspace))
            {
                var target = backContainer ?? decisionContainer;
                if (target != null && target.style.display != DisplayStyle.None && !target.ClassListContains("hidden"))
                {
                    OnBackButtonClicked();
                }
            }
        }

        private void OnDestroy()
        {
            if (modalCoordinator != null)
            {
                modalCoordinator.OnContextChanged -= HandleContextChanged;
            }
        }
    }
}
