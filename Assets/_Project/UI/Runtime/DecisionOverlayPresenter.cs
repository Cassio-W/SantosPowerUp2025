using System;
using Mandato.Content;
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

        [SerializeField] private UIDocument uiDocument;
        [SerializeField] private VisualTreeAsset uxmlAsset;
        [SerializeField] private PanelSettings panelSettings;
        private VisualElement decisionContainer;
        private VisualElement vignetteCorruption;
        private VisualElement perksContainer;
        private VisualElement perkModalPopup;
        private Label lblPerkTitle;
        private Label lblPerkTag;
        private Label lblPerkDesc;

        private VisualElement wrapperApprove;
        private VisualElement wrapperReject;
        private VisualElement wrapperContinue;
        private Button btnLeft;
        private Button btnRight;
        private Button btnContinue;
        private Label lblLeftChoice;
        private Label lblRightChoice;
        private Label lblContinue;

        private bool isChoicePending = false;
        private bool isSingleChoiceMode = false;

        private void Awake()
        {
            EnsureReferences();
            CacheElements();
            SubscribeFocusEvents();
        }

        private void OnEnable()
        {
            EnsureReferences();
            CacheElements();
            SubscribeFocusEvents();
        }

        private void EnsureReferences()
        {
            if (uiDocument == null)
            {
                uiDocument = GetComponent<UIDocument>() ?? GetComponentInChildren<UIDocument>();
            }

            if (uiDocument == null)
            {
                var docs = FindObjectsByType<UIDocument>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (var d in docs)
                {
                    if (d != null && d.visualTreeAsset != null && d.visualTreeAsset.name.IndexOf("Decision", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        uiDocument = d;
                        break;
                    }
                }
            }

            if (uiDocument == null)
            {
                uiDocument = gameObject.AddComponent<UIDocument>();
            }

            if (uiDocument.panelSettings == null)
            {
                if (panelSettings != null)
                {
                    uiDocument.panelSettings = panelSettings;
                }
                else
                {
#if UNITY_EDITOR
                    var screenPanel = UnityEditor.AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/UI/Decision/DecisionPanelSettings.asset");
                    if (screenPanel != null) uiDocument.panelSettings = screenPanel;
#endif
                }
            }

            if (uiDocument.visualTreeAsset == null)
            {
                if (uxmlAsset != null)
                {
                    uiDocument.visualTreeAsset = uxmlAsset;
                }
                else
                {
#if UNITY_EDITOR
                    var uxml = UnityEditor.AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/Decision/DecisionUI.uxml");
                    if (uxml != null) uiDocument.visualTreeAsset = uxml;
#endif
                }
            }
        }

        private void CacheElements()
        {
            if (uiDocument == null || uiDocument.rootVisualElement == null) return;

            VisualElement root = uiDocument.rootVisualElement;

            decisionContainer = root.Q<VisualElement>("decision-container");
            vignetteCorruption = root.Q<VisualElement>("vignette-corruption");
            perksContainer = root.Q<VisualElement>("perks-container");
            perkModalPopup = root.Q<VisualElement>("perk-modal-popup");
            lblPerkTitle = root.Q<Label>("perk-modal-title");
            lblPerkTag = root.Q<Label>("perk-modal-tag");
            lblPerkDesc = root.Q<Label>("perk-modal-desc");

            if (perkModalPopup != null)
            {
                perkModalPopup.style.display = DisplayStyle.None;
            }

            wrapperApprove = root.Q<VisualElement>("wrapper-approve");
            wrapperReject = root.Q<VisualElement>("wrapper-reject");
            wrapperContinue = root.Q<VisualElement>("wrapper-continue");

            btnLeft = root.Q<Button>("btn-approve") ?? root.Q<Button>("btn-left");
            btnRight = root.Q<Button>("btn-reject") ?? root.Q<Button>("btn-right");
            btnContinue = root.Q<Button>("btn-continue");

            lblLeftChoice = root.Q<Label>("lbl-approve-text") ?? root.Q<Label>("label-left-choice") ?? root.Q<Label>("txt-left");
            lblRightChoice = root.Q<Label>("lbl-reject-text") ?? root.Q<Label>("label-right-choice") ?? root.Q<Label>("txt-right");
            lblContinue = root.Q<Label>("lbl-continue-text") ?? root.Q<Label>("txt-continue");

            if (btnLeft != null)
            {
                btnLeft.clicked -= OnLeftClicked;
                btnLeft.clicked += OnLeftClicked;
            }

            if (btnRight != null)
            {
                btnRight.clicked -= OnRightClicked;
                btnRight.clicked += OnRightClicked;
            }

            if (btnContinue != null)
            {
                btnContinue.clicked -= OnContinueClicked;
                btnContinue.clicked += OnContinueClicked;
            }
        }

        public void PresentChoices(CardDefinition card)
        {
            EnsureReferences();
            CacheElements();
            isChoicePending = true;

            if (decisionContainer != null)
            {
                decisionContainer.RemoveFromClassList("hidden");
                decisionContainer.style.display = DisplayStyle.Flex;
            }

            if (card != null)
            {
                bool isTutorial = card.isTutorial ||
                                  card.id.IndexOf("Tutorial", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                  card.categoryTag.IndexOf("Tutorial", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                  card.title.IndexOf("Tutorial", StringComparison.OrdinalIgnoreCase) >= 0;

                string leftText = card.leftChoice != null && !string.IsNullOrEmpty(card.leftChoice.label) ? card.leftChoice.label : "Aceitar";
                string rightText = card.rightChoice != null && !string.IsNullOrEmpty(card.rightChoice.label) ? card.rightChoice.label : "Recusar";

                // Se a carta for de tutorial ou de 1 escolha (ou sem rejeição):
                bool hasOnlyOneOption = isTutorial ||
                                         string.Equals(leftText, rightText, StringComparison.OrdinalIgnoreCase) ||
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
            if (decisionContainer != null)
            {
                decisionContainer.AddToClassList("hidden");
                decisionContainer.style.display = DisplayStyle.None;
            }
        }

        public void SetCorruptionLevel(int corruption)
        {
            EnsureReferences();
            CacheElements();
            if (vignetteCorruption == null) return;

            // Começa a ficar visível a partir de 30% de corrupção, com opacidade máxima em 100%
            float factor = Mathf.Clamp01((corruption - 25f) / 75f);
            vignetteCorruption.style.opacity = factor * 0.9f;
        }

        public void RefreshActivePerks(System.Collections.Generic.IEnumerable<string> activePerkIds, System.Collections.Generic.IReadOnlyDictionary<string, PerkDefinition> perkCatalog)
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
                if (perkCatalog != null)
                {
                    perkCatalog.TryGetValue(perkId, out def);
                }

                var itemEl = new VisualElement();
                itemEl.AddToClassList("perk-item");

                var cardEl = new VisualElement();
                cardEl.AddToClassList("perk-icon-card");

                var iconEl = new VisualElement();
                iconEl.AddToClassList("perk-icon-large");

                if (def != null && def.icon != null)
                {
                    iconEl.style.backgroundImage = new StyleBackground(def.icon);
                }

                cardEl.Add(iconEl);
                itemEl.Add(cardEl);

                // Callbacks de hover para modal / tooltip
                string capturedId = perkId;
                PerkDefinition capturedDef = def;
                VisualElement capturedCard = cardEl;

                cardEl.RegisterCallback<PointerEnterEvent>(evt => ShowPerkTooltip(capturedId, capturedDef, capturedCard));
                cardEl.RegisterCallback<PointerLeaveEvent>(evt => HidePerkTooltip());

                perksContainer.Add(itemEl);
            }
        }

        private void ShowPerkTooltip(string perkId, PerkDefinition def, VisualElement targetCard)
        {
            if (perkModalPopup == null) return;

            if (lblPerkTitle != null)
            {
                lblPerkTitle.text = (def != null && !string.IsNullOrEmpty(def.title)) ? def.title : perkId;
            }

            if (lblPerkTag != null)
            {
                lblPerkTag.text = (def != null && def.durationMonths > 0) ? $"{def.durationMonths}M" : "PERK";
            }

            if (lblPerkDesc != null)
            {
                lblPerkDesc.text = FormatPerkDescription(def);
            }

            // Posiciona à direita do painel de perks
            perkModalPopup.style.left = 124;

            if (targetCard != null && targetCard.worldBound.yMin > 0)
            {
                perkModalPopup.style.top = targetCard.worldBound.yMin;
            }
            else
            {
                perkModalPopup.style.top = 28;
            }

            perkModalPopup.style.display = DisplayStyle.Flex;
            perkModalPopup.AddToClassList("open");
        }

        private void HidePerkTooltip()
        {
            if (perkModalPopup == null) return;
            perkModalPopup.RemoveFromClassList("open");
            perkModalPopup.style.display = DisplayStyle.None;
        }

        private string FormatPerkDescription(PerkDefinition def)
        {
            if (def == null) return "Vantagem ativa no mandato.";

            var sb = new System.Text.StringBuilder();
            if (!string.IsNullOrEmpty(def.description))
            {
                sb.AppendLine(def.description);
            }

            if (def.statDeltasPerMonth != null)
            {
                var d = def.statDeltasPerMonth;
                var deltas = new System.Collections.Generic.List<string>();

                if (d.economy != 0)
                    deltas.Add($"{(d.economy > 0 ? "+" : "")}{d.economy} Economia/mês");
                if (d.popularApproval != 0)
                    deltas.Add($"{(d.popularApproval > 0 ? "+" : "")}{d.popularApproval} Aprovação/mês");
                if (d.internationalRelations != 0)
                    deltas.Add($"{(d.internationalRelations > 0 ? "+" : "")}{d.internationalRelations} Relações Int./mês");
                if (d.climaticChanges != 0)
                    deltas.Add($"{(d.climaticChanges > 0 ? "+" : "")}{d.climaticChanges} Meio Ambiente/mês");
                if (d.corruption != 0)
                    deltas.Add($"{(d.corruption > 0 ? "+" : "")}{d.corruption} Corrupção/mês");

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
                    Mandato.Core.StatId.ClimaticChanges => "Meio Ambiente",
                    Mandato.Core.StatId.InternationalRelations => "Relações Internacionais",
                    Mandato.Core.StatId.PopularApproval => "Aprovação Popular",
                    Mandato.Core.StatId.Economy => "Economia",
                    Mandato.Core.StatId.Corruption => "Corrupção",
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
            OnChoiceSelected?.Invoke(0); // 0 = Aceitar / Aprovar
        }

        private void OnRightClicked()
        {
            if (!isChoicePending) return;
            isChoicePending = false;
            ClearChoices();
            OnChoiceSelected?.Invoke(1); // 1 = Recusar / Rejeitar
        }

        private void OnContinueClicked()
        {
            if (!isChoicePending) return;
            isChoicePending = false;
            ClearChoices();
            OnChoiceSelected?.Invoke(0); // 0 = Continuar (Tutorial)
        }

        private void Update()
        {
            if (!enableKeyboardShortcuts || !isChoicePending) return;

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
                // Opção Esquerda (0 = Aceitar)
                if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.Alpha1))
                {
                    OnLeftClicked();
                }
                // Opção Direita (1 = Recusar)
                else if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.Alpha2) ||
                         Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
                {
                    OnRightClicked();
                }
            }
        }

        private void SubscribeFocusEvents()
        {
            try
            {
                Type focusType = null;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    focusType = asm.GetType("CameraFocusManager");
                    if (focusType != null) break;
                }

                if (focusType != null)
                {
                    var eventInfo = focusType.GetEvent("OnObjectFocusChanged");
                    if (eventInfo != null)
                    {
                        var instanceProp = focusType.GetProperty("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                        var inst = instanceProp?.GetValue(null) ?? FindFirstObjectByType(focusType);
                        if (inst != null)
                        {
                            var handler = Delegate.CreateDelegate(eventInfo.EventHandlerType, this, typeof(DecisionOverlayPresenter).GetMethod(nameof(HandleCameraFocusChanged), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance));
                            eventInfo.RemoveEventHandler(inst, handler);
                            eventInfo.AddEventHandler(inst, handler);
                        }
                    }
                }
            }
            catch { }
        }

        private void HandleCameraFocusChanged(object focusedObject)
        {
            if (isChoicePending && decisionContainer != null)
            {
                decisionContainer.RemoveFromClassList("hidden");
                decisionContainer.style.display = DisplayStyle.Flex;
            }
        }

        private void OnDestroy()
        {
            if (btnLeft != null) btnLeft.clicked -= OnLeftClicked;
            if (btnRight != null) btnRight.clicked -= OnRightClicked;
            if (btnContinue != null) btnContinue.clicked -= OnContinueClicked;

            try
            {
                Type focusType = null;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    focusType = asm.GetType("CameraFocusManager");
                    if (focusType != null) break;
                }

                if (focusType != null)
                {
                    var eventInfo = focusType.GetEvent("OnObjectFocusChanged");
                    if (eventInfo != null)
                    {
                        var instanceProp = focusType.GetProperty("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                        var inst = instanceProp?.GetValue(null);
                        if (inst != null)
                        {
                            var handler = Delegate.CreateDelegate(eventInfo.EventHandlerType, this, typeof(DecisionOverlayPresenter).GetMethod(nameof(HandleCameraFocusChanged), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance));
                            eventInfo.RemoveEventHandler(inst, handler);
                        }
                    }
                }
            }
            catch { }
        }
    }
}
