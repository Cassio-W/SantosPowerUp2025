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

        private UIDocument uiDocument;
        private VisualElement decisionContainer;
        private VisualElement vignetteCorruption;
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
                var docs = FindObjectsByType<UIDocument>(FindObjectsSortMode.None);
                foreach (var d in docs)
                {
                    if (d != null && d.visualTreeAsset != null && d.visualTreeAsset.name.IndexOf("Decision", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        uiDocument = d;
                        break;
                    }
                }
            }
        }

        private void CacheElements()
        {
            if (uiDocument == null || uiDocument.rootVisualElement == null) return;

            VisualElement root = uiDocument.rootVisualElement;

            decisionContainer = root.Q<VisualElement>("decision-container");
            vignetteCorruption = root.Q<VisualElement>("vignette-corruption");
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
                bool isTutorial = card.id.IndexOf("Tutorial", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                  card.title.IndexOf("Tutorial", StringComparison.OrdinalIgnoreCase) >= 0;

                string leftText = card.leftChoice != null && !string.IsNullOrEmpty(card.leftChoice.label) ? card.leftChoice.label : "Aceitar";
                string rightText = card.rightChoice != null && !string.IsNullOrEmpty(card.rightChoice.label) ? card.rightChoice.label : "Recusar";

                // Se a carta for de tutorial de 1 escolha (ou sem rejeição):
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
                    if (lblContinue != null) lblContinue.text = "Continuar";
                }
                else
                {
                    if (wrapperContinue != null) wrapperContinue.style.display = DisplayStyle.None;
                    if (wrapperApprove != null) wrapperApprove.style.display = DisplayStyle.Flex;
                    if (wrapperReject != null) wrapperReject.style.display = DisplayStyle.Flex;
                    if (lblLeftChoice != null) lblLeftChoice.text = "Aceitar";
                    if (lblRightChoice != null) lblRightChoice.text = "Recusar";
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
