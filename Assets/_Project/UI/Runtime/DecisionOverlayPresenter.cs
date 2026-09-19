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
    public class DecisionOverlayPresenter : MonoBehaviour
    {
        [Header("Configuração de Atalhos")]
        public bool enableKeyboardShortcuts = true;

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
        private CardDefinition currentCard;

        private VisualElement wrapperBack;
        private VisualElement wrapperApprove;
        private VisualElement wrapperReject;
        private VisualElement wrapperContinue;
        private Button btnBack;
        private Button btnLeft;
        private Button btnRight;
        private Button btnContinue;
        private Label lblBack;
        private Label lblLeftChoice;
        private Label lblRightChoice;
        private Label lblContinue;

        private bool isChoicePending = false;
        private bool isSingleChoiceMode = false;
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

            wrapperBack = root.Q<VisualElement>("wrapper-back");
            wrapperApprove = root.Q<VisualElement>("wrapper-approve");
            wrapperReject = root.Q<VisualElement>("wrapper-reject");
            wrapperContinue = root.Q<VisualElement>("wrapper-continue");

            btnBack = root.Q<Button>("btn-back") ?? root.Q<Button>("btn-return");
            lblBack = root.Q<Label>("lbl-back-text");

            btnLeft = root.Q<Button>("btn-approve") ?? root.Q<Button>("btn-left");
            btnRight = root.Q<Button>("btn-reject") ?? root.Q<Button>("btn-right");
            btnContinue = root.Q<Button>("btn-continue");

            lblLeftChoice = root.Q<Label>("lbl-approve-text") ?? root.Q<Label>("label-left-choice") ?? root.Q<Label>("txt-left");
            lblRightChoice = root.Q<Label>("lbl-reject-text") ?? root.Q<Label>("label-right-choice") ?? root.Q<Label>("txt-right");
            lblContinue = root.Q<Label>("lbl-continue-text") ?? root.Q<Label>("txt-continue");

            BindButton(btnBack, OnBackButtonClicked);
            BindButton(btnLeft, OnLeftClicked, () => OnChoiceHover(0), OnChoiceLeave);
            BindButton(btnRight, OnRightClicked, () => OnChoiceHover(1), OnChoiceLeave);
            BindButton(btnContinue, OnContinueClicked, () => OnChoiceHover(0), OnChoiceLeave);
        }

        private void BindButton(Button btn, Action onClick, Action onEnter = null, Action onLeave = null)
        {
            if (btn == null) return;
            btn.clicked -= onClick;
            btn.clicked += onClick;

            if (onEnter != null)
            {
                btn.RegisterCallback<PointerEnterEvent>(_ => onEnter());
            }
            if (onLeave != null)
            {
                btn.RegisterCallback<PointerLeaveEvent>(_ => onLeave());
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

        private void OnChoiceHover(int index)
        {
            if (!isChoicePending || currentCard == null) return;
            ChoiceDefinition choice = (index == 0) ? currentCard.leftChoice : currentCard.rightChoice;
            OnChoiceHovered?.Invoke(choice);
        }

        private void OnChoiceLeave()
        {
            if (!isChoicePending) return;
            OnChoiceUnhovered?.Invoke();
        }

        public void PresentChoices(CardDefinition card)
        {
            EnsureReferences();
            CacheElements();
            isChoicePending = true;
            currentCard = card;

            SetBackVisible(false);

            if (card != null)
            {
                string leftText = !string.IsNullOrEmpty(card.leftChoice?.label) ? card.leftChoice.label : "Aceitar";
                string rightText = !string.IsNullOrEmpty(card.rightChoice?.label) ? card.rightChoice.label : "Recusar";

                bool hasOnlyOneOption = string.Equals(leftText, rightText, StringComparison.OrdinalIgnoreCase) ||
                                        leftText.IndexOf("Continuar", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                        rightText.IndexOf("Continuar", StringComparison.OrdinalIgnoreCase) >= 0;

                isSingleChoiceMode = hasOnlyOneOption;

                if (isSingleChoiceMode && wrapperContinue != null)
                {
                    wrapperContinue.style.display = DisplayStyle.Flex;
                    if (wrapperApprove != null) wrapperApprove.style.display = DisplayStyle.None;
                    if (wrapperReject != null) wrapperReject.style.display = DisplayStyle.None;
                    if (lblContinue != null) lblContinue.text = !string.IsNullOrEmpty(leftText) ? leftText : "Continuar";
                }
                else
                {
                    if (wrapperContinue != null) wrapperContinue.style.display = DisplayStyle.None;
                    if (wrapperApprove != null) wrapperApprove.style.display = DisplayStyle.Flex;
                    if (wrapperReject != null) wrapperReject.style.display = DisplayStyle.Flex;
                    if (lblLeftChoice != null) lblLeftChoice.text = leftText;
                    if (lblRightChoice != null) lblRightChoice.text = rightText;
                }
            }
        }

        public void ClearChoices()
        {
            isChoicePending = false;
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

        private void OnLeftClicked()
        {
            if (!isChoicePending) return;
            isChoicePending = false;
            ClearChoices();
            OnChoiceSelected?.Invoke(0);
        }

        private void OnRightClicked()
        {
            if (!isChoicePending) return;
            isChoicePending = false;
            ClearChoices();
            OnChoiceSelected?.Invoke(1);
        }

        private void OnContinueClicked()
        {
            if (!isChoicePending) return;
            isChoicePending = false;
            ClearChoices();
            OnChoiceSelected?.Invoke(0);
        }

        private void Update()
        {
            if (!isVisible || !enableKeyboardShortcuts) return;
            if (modalCoordinator != null && !modalCoordinator.CanProcessDecisionShortcuts()) return;

            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Backspace))
            {
                var target = backContainer ?? decisionContainer;
                if (target != null && target.style.display != DisplayStyle.None && !target.ClassListContains("hidden"))
                {
                    OnBackButtonClicked();
                    return;
                }
            }

            if (!isChoicePending) return;

            if (isSingleChoiceMode)
            {
                if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) ||
                    Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.LeftArrow))
                {
                    OnContinueClicked();
                }
            }
            else
            {
                if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.Alpha1))
                {
                    OnLeftClicked();
                }
                else if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.Alpha2))
                {
                    OnRightClicked();
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
