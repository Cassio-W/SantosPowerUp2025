using System;
using System.Collections;
using Mandato.Content;
using Mandato.Core;
using Mandato.Presentation;
using Mandato.Run;
using Mandato.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mandato.Infrastructure
{
    public class RunFlowCoordinator : MonoBehaviour
    {
        private RunStateMachine stateMachine;
        private RunCatalog catalog;
        private RunProfileService profileService;
        private ScenePresentationBindings bindings;
        private FlipPhoneCoordinator flipPhoneCoordinator;
        private UIModalCoordinator modalCoordinator;
        private TutorialManager tutorialManager;
        private InteractiveEventRegistry eventRegistry;

        [Header("Configuração de Fluxo")]
        [SerializeField] private bool requireSpaceToCallNextNpc = true;
        [SerializeField] private KeyCode callNextNpcKey = KeyCode.Space;
        [SerializeField] private float delayBetweenProposals = 1.5f;
        [SerializeField] private string mainMenuSceneName = "MenuV2";
        [Tooltip("Se verdadeiro, o hover dos botões de decisão exibe as setas de impacto no monitor.")]
        [SerializeField] private bool enableDecisionHoverPreview = false;

        [Header("Tempos de Decisão e Carimbo")]
        [Tooltip("Tempo de pausa para visualização do papel carimbado antes de sair do foco.")]
        [SerializeField] private float postStampWaitDuration = 0.55f;
        [Tooltip("Timeout máximo de espera para a câmera retornar à visão da mesa.")]
        [SerializeField] private float cameraUnfocusWaitTimeout = 1.0f;

        [Header("Final da Partida / Cerimônia")]
        [Tooltip("Ponto de foco da câmera para a cena final de palmas.")]
        [SerializeField] private Transform posCameraPalmas;
        [Tooltip("Ponto de spawn do prefab de palmas na cena.")]
        [SerializeField] private Transform posPalmas;
        [Tooltip("Prefab de palmas instanciado ao final da partida.")]
        [SerializeField] private GameObject palmasPrefab;
        [Tooltip("Nome do interlocutor no diálogo pré-relatório final.")]
        [SerializeField] private string endDialogueSpeaker = "ASSESSOR";
        [Tooltip("Texto do diálogo de encerramento em caso de vitória/mandato cumprido.")]
        [TextArea(2, 4)]
        [SerializeField] private string victoryEndDialogueText = "Parabéns, Presidente! Você concluiu seu mandato com sucesso e liderou a nação através de grandes reformas e desafios.";
        [Tooltip("Texto do diálogo de encerramento em caso de derrota.")]
        [TextArea(2, 4)]
        [SerializeField] private string defeatEndDialogueText = "Parabéns pelo empenho e dedicação, Presidente! Governar o país é uma missão árdua e você enfrentou grandes decisões até o fim do mandato.";

        [Header("Telemetria e Debug In-Game (Inspector)")]
        [Tooltip("Exibição e monitoramento em tempo real de todas as variáveis internas e estado da partida no Inspector.")]
        [SerializeField] private RunInspectorDebug inspectorDebug = new RunInspectorDebug();

        private bool isAwaitingSpaceForNextNpc = false;
        private bool isPlayerHandRaised = false;
        private bool isPaperFocused = false;
        private bool isDismissingProposal = false;
        private bool isPcFocused = false;
        private bool isProcessingDecision = false;
        private GameObject activePalmasInstance;

        public bool IsAwaitingSpaceForNextNpc => isAwaitingSpaceForNextNpc;
        public bool IsPlayerHandRaised => isPlayerHandRaised;
        public bool IsPaperFocused => isPaperFocused;
        public bool IsDismissingProposal => isDismissingProposal;
        public bool IsPcFocused => isPcFocused;
        public bool IsProcessingDecision => isProcessingDecision;
        public Transform PosCameraPalmas
        {
            get => GetPosCameraPalmas();
            set => posCameraPalmas = value;
        }
        public Transform PosPalmas
        {
            get => GetPosPalmas();
            set => posPalmas = value;
        }
        public GameObject PalmasPrefab
        {
            get => GetPalmasPrefab();
            set => palmasPrefab = value;
        }
        public GameObject ActivePalmasInstance => activePalmasInstance;
        public string EndDialogueSpeaker
        {
            get => endDialogueSpeaker;
            set => endDialogueSpeaker = value;
        }
        public string VictoryEndDialogueText
        {
            get => victoryEndDialogueText;
            set => victoryEndDialogueText = value;
        }
        public string DefeatEndDialogueText
        {
            get => defeatEndDialogueText;
            set => defeatEndDialogueText = value;
        }
        public float PostStampWaitDuration
        {
            get => postStampWaitDuration;
            set => postStampWaitDuration = Mathf.Max(0f, value);
        }
        public float CameraUnfocusWaitTimeout
        {
            get => cameraUnfocusWaitTimeout;
            set => cameraUnfocusWaitTimeout = Mathf.Max(0.1f, value);
        }
        public UIModalCoordinator ModalCoordinator => modalCoordinator;
        public TutorialManager TutorialManager => tutorialManager;
        public bool EnableDecisionHoverPreview
        {
            get => enableDecisionHoverPreview;
            set => enableDecisionHoverPreview = value;
        }
        public RunInspectorDebug InspectorDebug => inspectorDebug;

        public void Initialize(
            RunStateMachine stateMachine,
            RunCatalog catalog,
            RunProfileService profileService,
            ScenePresentationBindings bindings,
            FlipPhoneCoordinator flipPhoneCoordinator,
            bool requireSpace = true,
            KeyCode callKey = KeyCode.Space,
            float delayBetween = 1.5f,
            string menuScene = "MenuV2",
            UIModalCoordinator modalCoordinator = null,
            InteractiveEventRegistry eventRegistry = null)
        {
            this.stateMachine = stateMachine;
            this.catalog = catalog;
            this.profileService = profileService;
            this.bindings = bindings;
            this.flipPhoneCoordinator = flipPhoneCoordinator;
            this.modalCoordinator = modalCoordinator ?? new UIModalCoordinator();
            this.eventRegistry = eventRegistry;
            this.requireSpaceToCallNextNpc = requireSpace;
            this.callNextNpcKey = callKey;
            this.delayBetweenProposals = delayBetween;
            this.mainMenuSceneName = string.IsNullOrEmpty(menuScene) ? "MenuV2" : menuScene;

            this.tutorialManager = (bindings?.TutorialManager != null)
                ? bindings.TutorialManager
                : GetComponent<TutorialManager>() ?? FindFirstObjectByType<TutorialManager>(FindObjectsInactive.Include) ?? gameObject.AddComponent<TutorialManager>();

            var speechBubble = bindings?.SpeechBubblePresenter ?? FindFirstObjectByType<NpcSpeechBubblePresenter>(FindObjectsInactive.Include);
            var cameraFocus = bindings?.CameraFocus ?? CameraFocusManager.Instance ?? FindFirstObjectByType<CameraFocusManager>(FindObjectsInactive.Include);

            this.tutorialManager.Initialize(
                stateMachine,
                catalog,
                speechBubble,
                bindings?.PaperPresenter,
                bindings?.DecisionOverlayPresenter,
                cameraFocus,
                this.modalCoordinator
            );

            Bind();
        }

        private void Bind()
        {
            if (stateMachine == null || bindings == null) return;

            if (modalCoordinator != null)
            {
                modalCoordinator.OnContextChanged -= HandleContextChanged;
                modalCoordinator.OnContextChanged += HandleContextChanged;
                HandleContextChanged(modalCoordinator.CurrentContext, InteractionContext.DeskOverview);
            }

            if (bindings.PresentationCoordinator != null)
            {
                bindings.PresentationCoordinator.Bind(stateMachine, catalog.Cards, catalog.Investments);
                bindings.PresentationCoordinator.OnProposalOnDesk += HandleProposalReadyOnDesk;
                bindings.PresentationCoordinator.OnConsequencesFinished += HandleConsequencesFinishedAndAdvance;
            }
            else
            {
                stateMachine.OnProposalReady += HandleProposalReadyOnDesk;
            }

            stateMachine.OnConsequencesReady += HandleConsequencesResolved;
            stateMachine.OnRunTerminated += HandleRunTerminated;

            if (bindings.DecisionOverlayPresenter != null)
            {
                bindings.DecisionOverlayPresenter.SetModalCoordinator(modalCoordinator);
                bindings.DecisionOverlayPresenter.OnChoiceSelected += HandlePlayerChoiceSubmitted;
                bindings.DecisionOverlayPresenter.OnChoiceHovered += HandlePlayerChoiceHovered;
                bindings.DecisionOverlayPresenter.OnChoiceUnhovered += HandlePlayerChoiceUnhovered;
                bindings.DecisionOverlayPresenter.OnBackRequested += HandleDecisionBackRequested;
                bindings.DecisionOverlayPresenter.OnCalendarRequested += HandleCalendarRequested;
            }

            if (bindings.StampTool != null)
            {
                bindings.StampTool.OnStampApplied -= HandleStampApplied;
                bindings.StampTool.OnStampApplied += HandleStampApplied;
                bindings.StampTool.OnStampPreviewUpdated -= HandleStampPreviewUpdated;
                bindings.StampTool.OnStampPreviewUpdated += HandleStampPreviewUpdated;
                bindings.StampTool.OnStampDecisionSubmitted -= HandlePlayerChoiceSubmitted;
                bindings.StampTool.OnStampDecisionSubmitted += HandlePlayerChoiceSubmitted;
            }

            if (bindings.FlipPhonePresenter != null)
            {
                bindings.FlipPhonePresenter.SetModalCoordinator(modalCoordinator);
                bindings.FlipPhonePresenter.OnPhoneOpened += () => modalCoordinator?.SetContext(InteractionContext.PhoneDrawer);
                bindings.FlipPhonePresenter.OnPhoneClosed += () =>
                {
                    if (modalCoordinator?.CurrentContext == InteractionContext.PhoneDrawer)
                    {
                        modalCoordinator.SetContext(InteractionContext.DeskOverview);
                    }
                };
            }

            if (flipPhoneCoordinator != null)
            {
                flipPhoneCoordinator.OnActionExecuted += HandleFlipPhoneActionExecuted;
            }

            if (bindings.EndScreenPresenter != null)
            {
                bindings.EndScreenPresenter.OnRestartRequested += RestartRun;
                bindings.EndScreenPresenter.OnMainMenuRequested += ReturnToMenu;
            }

            if (bindings.DeskCallButton != null)
            {
                bindings.DeskCallButton.OnCallRequested += AuthorizeNextVisitor;
            }

            if (bindings.CameraFocus != null)
            {
                bindings.CameraFocus.OnObjectFocusChanged += HandleObjectFocusChanged;
            }

            if (bindings.RetroMonitorPresenter != null)
            {
                bindings.RetroMonitorPresenter.OnWarInvestmentRequested -= SubmitWarInvestment;
                bindings.RetroMonitorPresenter.OnWarInvestmentRequested += SubmitWarInvestment;
                bindings.RetroMonitorPresenter.OnWarSurrenderRequested -= SurrenderWar;
                bindings.RetroMonitorPresenter.OnWarSurrenderRequested += SurrenderWar;
            }
        }

        private void OnDestroy()
        {
            CleanupEndingVisuals();

            if (modalCoordinator != null)
                modalCoordinator.OnContextChanged -= HandleContextChanged;

            if (bindings?.DecisionOverlayPresenter != null)
            {
                bindings.DecisionOverlayPresenter.OnBackRequested -= HandleDecisionBackRequested;
                bindings.DecisionOverlayPresenter.OnCalendarRequested -= HandleCalendarRequested;
            }

            if (bindings?.StampTool != null)
            {
                bindings.StampTool.OnStampApplied -= HandleStampApplied;
                bindings.StampTool.OnStampPreviewUpdated -= HandleStampPreviewUpdated;
                bindings.StampTool.OnStampDecisionSubmitted -= HandlePlayerChoiceSubmitted;
            }

            if (flipPhoneCoordinator != null)
                flipPhoneCoordinator.OnActionExecuted -= HandleFlipPhoneActionExecuted;

            if (bindings?.DeskCallButton != null)
                bindings.DeskCallButton.OnCallRequested -= AuthorizeNextVisitor;

            if (bindings?.CameraFocus != null)
                bindings.CameraFocus.OnObjectFocusChanged -= HandleObjectFocusChanged;

            if (bindings?.RetroMonitorPresenter != null)
            {
                bindings.RetroMonitorPresenter.OnWarInvestmentRequested -= SubmitWarInvestment;
                bindings.RetroMonitorPresenter.OnWarSurrenderRequested -= SurrenderWar;
            }
        }

        private void HandleStampPreviewUpdated(bool isVisible, bool isApproved, Vector2 uv)
        {
            bindings?.PaperPresenter?.UpdateStampPreview(isVisible, isApproved, uv);
        }

        private void HandleStampApplied(int choiceIndex, Vector2 uv)
        {
            if (bindings?.PaperPresenter != null)
            {
                float randomAngle = UnityEngine.Random.Range(-5f, 5f);
                bindings.PaperPresenter.AddStampMark(choiceIndex == 0, uv, randomAngle);
            }
        }

        private void HandleDecisionBackRequested()
        {
            var cam = CameraFocusManager.Instance ?? bindings?.CameraFocus;
            if (cam != null && cam.HasActiveFocus)
            {
                cam.Unfocus();
            }
        }

        private void HandleContextChanged(InteractionContext newContext, InteractionContext oldContext)
        {
            isPcFocused = (newContext == InteractionContext.PcTerminal);
            isPaperFocused = (newContext == InteractionContext.PaperInspect);
            bindings?.RetroMonitorPresenter?.SetInteractable(isPcFocused);

            switch (newContext)
            {
                case InteractionContext.DeskOverview:
                    bindings?.StampTool?.SetInspectActive(false);
                    if (bindings != null)
                    {
                        bindings.FlipPhonePresenter?.SetInteractable(true);
                        bindings.DeskCallButton?.SetInteractable(true);
                        bindings.DecisionOverlayPresenter?.SetVisible(true);
                        bindings.DecisionOverlayPresenter?.SetBackVisible(false);

                        if (oldContext == InteractionContext.PaperInspect && IsProposalActive() && !isDismissingProposal && !isPlayerHandRaised)
                        {
                            bindings.PlayDealAnimation();
                            isPlayerHandRaised = true;
                        }
                    }
                    break;

                case InteractionContext.PaperInspect:
                    bindings?.StampTool?.SetInspectActive(true);
                    if (bindings != null)
                    {
                        bindings.ResetPlayerHandImmediate();
                        isPlayerHandRaised = false;
                        bindings.FlipPhonePresenter?.SetInteractable(false);
                        bindings.DeskCallButton?.SetInteractable(false);
                        bindings.DecisionOverlayPresenter?.SetVisible(true);
                        bindings.DecisionOverlayPresenter?.SetBackVisible(true);
                    }
                    break;

                case InteractionContext.PcTerminal:
                    bindings?.StampTool?.SetInspectActive(false);
                    flipPhoneCoordinator?.ClosePhone();
                    if (bindings != null)
                    {
                        bindings.FlipPhonePresenter?.SetInteractable(false);
                        bindings.DecisionOverlayPresenter?.SetVisible(false);
                        bindings.DeskCallButton?.SetInteractable(false);
                    }
                    break;

                case InteractionContext.PhoneDrawer:
                    bindings?.StampTool?.SetInspectActive(false);
                    if (bindings != null)
                    {
                        bindings.DeskCallButton?.SetInteractable(true);
                        bindings.DecisionOverlayPresenter?.SetVisible(false);
                    }
                    break;

                case InteractionContext.CalendarInspect:
                    bindings?.StampTool?.SetInspectActive(false);
                    flipPhoneCoordinator?.ClosePhone();
                    if (bindings != null)
                    {
                        bindings.FlipPhonePresenter?.SetInteractable(false);
                        bindings.DeskCallButton?.SetInteractable(false);
                        bindings.DecisionOverlayPresenter?.SetVisible(true);
                        bindings.DecisionOverlayPresenter?.SetBackVisible(true);
                    }
                    break;

                case InteractionContext.TutorialStep:
                    bindings?.StampTool?.SetInspectActive(false);
                    if (bindings != null)
                    {
                        bindings.FlipPhonePresenter?.SetInteractable(false);
                        bindings.DeskCallButton?.SetInteractable(false);
                        bindings.DecisionOverlayPresenter?.SetBackVisible(false);
                    }
                    break;

                case InteractionContext.EventMinigame:
                    bindings?.StampTool?.SetInspectActive(false);
                    flipPhoneCoordinator?.ClosePhone();
                    if (bindings != null)
                    {
                        bindings.FlipPhonePresenter?.SetInteractable(false);
                        bindings.DeskCallButton?.SetInteractable(false);
                        bindings.DecisionOverlayPresenter?.SetVisible(false);
                    }
                    break;

                case InteractionContext.EndSummary:
                    bindings?.StampTool?.SetInspectActive(false);
                    flipPhoneCoordinator?.ClosePhone();
                    if (bindings != null)
                    {
                        bindings.FlipPhonePresenter?.SetInteractable(false);
                        bindings.DeskCallButton?.SetInteractable(false);
                        bindings.DecisionOverlayPresenter?.SetVisible(false);
                    }
                    break;
            }
        }

        public void HandleObjectFocusChanged(FocusableObject focusedObject)
        {
            if (focusedObject == null)
            {
                if (modalCoordinator != null && (modalCoordinator.CurrentContext == InteractionContext.PcTerminal ||
                                                 modalCoordinator.CurrentContext == InteractionContext.PaperInspect ||
                                                 modalCoordinator.CurrentContext == InteractionContext.CalendarInspect))
                {
                    modalCoordinator.SetContext(InteractionContext.DeskOverview);
                }
            }
            else if (IsMonitorFocusable(focusedObject))
            {
                modalCoordinator?.SetContext(InteractionContext.PcTerminal);
            }
            else if (focusedObject is PaperFocusableObject)
            {
                modalCoordinator?.SetContext(InteractionContext.PaperInspect);
            }
            else if (IsCalendarFocusable(focusedObject))
            {
                modalCoordinator?.SetContext(InteractionContext.CalendarInspect);
            }
        }

        public bool IsProposalActive()
        {
            if (stateMachine?.CurrentCard == null) return false;
            if (stateMachine.RunState != null && (stateMachine.RunState.termination.IsDefeat || stateMachine.RunState.termination.IsVictory)) return false;
            if (isAwaitingSpaceForNextNpc || isDismissingProposal) return false;
            if (tutorialManager != null && tutorialManager.IsTutorialActive) return false;
            if (IsTutorialCard(stateMachine.CurrentCard)) return false;

            return stateMachine.CurrentPhase == RunPhase.PresentingProposal ||
                   stateMachine.CurrentPhase == RunPhase.AwaitingChoice;
        }

        public bool IsMonitorFocusable(FocusableObject obj)
        {
            if (obj == null) return false;

            if (bindings?.RetroMonitorPresenter != null)
            {
                if (obj.gameObject == bindings.RetroMonitorPresenter.gameObject ||
                    obj.transform.IsChildOf(bindings.RetroMonitorPresenter.transform) ||
                    bindings.RetroMonitorPresenter.transform.IsChildOf(obj.transform))
                {
                    return true;
                }
            }

            string name = obj.name.ToLowerInvariant();
            return name.Contains("pc") || name.Contains("monitor") || name.Contains("computador") || name.Contains("computer") || name.Contains("tela");
        }

        public bool IsCalendarFocusable(FocusableObject obj)
        {
            if (obj == null) return false;

            if (bindings?.CalendarFocusable != null && obj == bindings.CalendarFocusable)
            {
                return true;
            }

            if (bindings?.CalendarPresenter != null)
            {
                if (obj.gameObject == bindings.CalendarPresenter.gameObject ||
                    obj.transform.IsChildOf(bindings.CalendarPresenter.transform) ||
                    bindings.CalendarPresenter.transform.IsChildOf(obj.transform))
                {
                    return true;
                }
            }

            string name = obj.name.ToLowerInvariant();
            return name.Contains("calendar") || name.Contains("calendario");
        }

        private void HandleCalendarRequested()
        {
            if (modalCoordinator != null && modalCoordinator.CurrentContext == InteractionContext.CalendarInspect)
            {
                HandleDecisionBackRequested();
                return;
            }

            var calendar = bindings?.CalendarPresenter ?? CalendarWallPresenter.Instance;
            if (calendar != null && stateMachine != null)
            {
                calendar.SyncWithRun(stateMachine.RunState, catalog?.Events, catalog?.Perks, stateMachine.CurrentCard);
            }

            var cam = CameraFocusManager.Instance ?? bindings?.CameraFocus;
            FocusableObject targetFocusable = bindings?.CalendarFocusable;

            if (targetFocusable == null && bindings?.CalendarPresenter != null)
            {
                targetFocusable = bindings.CalendarPresenter.GetComponent<FocusableObject>() ??
                                  bindings.CalendarPresenter.GetComponentInChildren<FocusableObject>();
            }

            if (targetFocusable == null && CalendarWallPresenter.Instance != null)
            {
                targetFocusable = CalendarWallPresenter.Instance.GetComponent<FocusableObject>() ??
                                  CalendarWallPresenter.Instance.GetComponentInChildren<FocusableObject>();
            }

            if (targetFocusable != null)
            {
                if (cam != null)
                {
                    cam.Focus(targetFocusable);
                }
                modalCoordinator?.SetContext(InteractionContext.CalendarInspect);
            }
            else
            {
                Transform calendarTransform = bindings?.CalendarPresenter != null ? bindings.CalendarPresenter.transform :
                                             (CalendarWallPresenter.Instance != null ? CalendarWallPresenter.Instance.transform : null);

                if (calendarTransform != null && cam != null)
                {
                    cam.FocusPoint(calendarTransform, 50f, 0.45f);
                    modalCoordinator?.SetContext(InteractionContext.CalendarInspect);
                }
            }
        }

        public void SetPcFocusState(bool isFocused)
        {
            if (isFocused)
            {
                modalCoordinator?.SetContext(InteractionContext.PcTerminal);
            }
            else if (modalCoordinator?.CurrentContext == InteractionContext.PcTerminal)
            {
                modalCoordinator?.SetContext(InteractionContext.DeskOverview);
            }
        }

        private void HandleFlipPhoneActionExecuted(FlipPhoneUseReport report)
        {
            if (report != null && report.success)
            {
                if (report.startedWarLevel != WarLevel.None && bindings?.RetroMonitorPresenter != null)
                {
                    bindings.RetroMonitorPresenter.SetLogVariantClass("log-alert");
                    bindings.RetroMonitorPresenter.LogMessage($"> [DEFESA] Conflito bélico deflagrado! {report.startedWarLevel.DisplayName()}");
                }

                if (report.dismissedCurrentProposal)
                {
                    DismissCurrentProposal();
                }
                else
                {
                    SyncPresenters(applyCameraEffects: true, instantCamera: false);
                }
            }
        }

        private bool advanceMonthOnCurrentDismissal = true;

        public void DismissCurrentProposal(bool advanceMonth = true)
        {
            if (stateMachine?.CurrentCard == null) return;

            isDismissingProposal = true;
            advanceMonthOnCurrentDismissal = advanceMonth;
            bindings?.StampTool?.SetInspectActive(false);
            modalCoordinator?.SetContext(InteractionContext.DeskOverview);

            bindings?.RetroMonitorPresenter?.ClearPreviewImpacts();
            bindings?.DecisionOverlayPresenter?.ClearChoices();
            flipPhoneCoordinator?.ClosePhone();

            bindings?.PlayDealAnimationReverse();
            isPlayerHandRaised = false;

            if (bindings?.PaperPresenter != null)
            {
                bindings.PaperPresenter.SetPaperInteractable(false);
                bindings.PaperPresenter.SetPaperActive(false);
            }

            bindings?.CameraFocus?.Unfocus();

            if (bindings?.PresentationCoordinator != null)
            {
                bindings.PresentationCoordinator.DismissCurrentProposal(false, FinishProposalDismissal);
            }
            else
            {
                FinishProposalDismissal();
            }
        }

        private void FinishProposalDismissal()
        {
            if (stateMachine == null) return;

            stateMachine.DismissCurrentProposal(advanceMonthOnCurrentDismissal, catalog.Perks, catalog.Events, catalog.Investments);

            if (stateMachine.RunState.termination.IsDefeat || stateMachine.RunState.termination.IsVictory)
                return;

            SyncPresenters(applyCameraEffects: true, instantCamera: false);
            isDismissingProposal = false;

            SaveCurrentRunState();

            if (requireSpaceToCallNextNpc)
            {
                isAwaitingSpaceForNextNpc = true;
            }
            else
            {
                StartCoroutine(DrawNextProposalRoutine(delayBetweenProposals));
            }
        }

        private void Update()
        {
            inspectorDebug?.UpdateFrom(
                stateMachine,
                catalog,
                modalCoordinator,
                isPlayerHandRaised,
                isAwaitingSpaceForNextNpc,
                OnInspectorStatsModified,
                OnInspectorCalendarModified
            );

            bool spaceOrEnterDown = Input.GetKeyDown(callNextNpcKey) ||
                                    Input.GetKeyDown(KeyCode.Space) ||
                                    Input.GetKeyDown(KeyCode.Return) ||
                                    Input.GetKeyDown(KeyCode.KeypadEnter);

            bool spaceOrEnterUp = Input.GetKeyUp(callNextNpcKey) ||
                                  Input.GetKeyUp(KeyCode.Space) ||
                                  Input.GetKeyUp(KeyCode.Return) ||
                                  Input.GetKeyUp(KeyCode.KeypadEnter);

            if (spaceOrEnterDown)
            {
                if (modalCoordinator != null && !modalCoordinator.CanCallNextVisitor()) return;
                if (bindings?.EndScreenPresenter != null && bindings.EndScreenPresenter.IsVisible) return;

                bindings?.DeskCallButton?.StartPress();

                if (isAwaitingSpaceForNextNpc)
                {
                    AuthorizeNextVisitor();
                }
            }
            else if (spaceOrEnterUp)
            {
                bindings?.DeskCallButton?.ReleasePress();
            }
        }

        private void OnInspectorStatsModified()
        {
            SyncPresenters(applyCameraEffects: true, instantCamera: true);
        }

        private void OnInspectorCalendarModified()
        {
            SyncPresenters(applyCameraEffects: false, instantCamera: true);
        }

        [ContextMenu("Debug/Calendário: Avançar 1 Mês (+1)")]
        public void DebugAdvanceOneMonth()
        {
            if (stateMachine?.RunState?.calendar != null)
            {
                SetCalendarMonth(stateMachine.RunState.calendar.currentMonthIndex + 1);
            }
        }

        [ContextMenu("Debug/Calendário: Retroceder 1 Mês (-1)")]
        public void DebugRewindOneMonth()
        {
            if (stateMachine?.RunState?.calendar != null)
            {
                SetCalendarMonth(stateMachine.RunState.calendar.currentMonthIndex - 1);
            }
        }

        [ContextMenu("Debug/Calendário: Avançar 1 Ano (+12)")]
        public void DebugAdvanceOneYear()
        {
            if (stateMachine?.RunState?.calendar != null)
            {
                SetCalendarMonth(stateMachine.RunState.calendar.currentMonthIndex + 12);
            }
        }

        [ContextMenu("Debug/Calendário: Ir para Último Mês (48)")]
        public void DebugJumpToLastMonth()
        {
            if (stateMachine?.RunState?.calendar != null)
            {
                SetCalendarMonth(stateMachine.RunState.calendar.totalMonths);
            }
        }

        [ContextMenu("Debug/Calendário: Resetar para Mês 1")]
        public void DebugResetCalendarToStart()
        {
            SetCalendarMonth(1);
        }

        public void SetCalendarMonth(int targetMonth)
        {
            if (stateMachine?.RunState?.calendar == null) return;
            int clamped = Mathf.Clamp(targetMonth, 1, Math.Max(1, stateMachine.RunState.calendar.totalMonths));
            stateMachine.RunState.calendar.currentMonthIndex = clamped;
            if (inspectorDebug != null)
            {
                inspectorDebug.Calendar.currentMonthIndex = clamped;
            }
            SyncPresenters(applyCameraEffects: false, instantCamera: true);
            Debug.Log($"[RunFlowCoordinator] Calendário alterado para o mês {clamped} ({stateMachine.RunState.calendar.DisplayDate})");
        }

        [ContextMenu("Debug/Aplicar Atributos do Inspector")]
        public void DebugApplyInspectorAttributes()
        {
            if (stateMachine?.RunState?.stats != null && inspectorDebug != null)
            {
                var stats = stateMachine.RunState.stats;
                stats.climaticChanges = Mathf.Clamp(inspectorDebug.Stats.climaticChanges, 0, 100);
                stats.internationalRelations = Mathf.Clamp(inspectorDebug.Stats.internationalRelations, 0, 100);
                stats.popularApproval = Mathf.Clamp(inspectorDebug.Stats.popularApproval, 0, 100);
                stats.economy = Mathf.Clamp(inspectorDebug.Stats.economy, 0, 100);
                stats.corruption = Mathf.Clamp(inspectorDebug.Stats.corruption, 0, 100);
                SyncPresenters(applyCameraEffects: true, instantCamera: true);
                Debug.Log("[RunFlowCoordinator] Atributos aplicados a partir do Inspector com sucesso!");
            }
        }

        [ContextMenu("Debug/Setar Todos Atributos para 50")]
        public void DebugSetStatsTo50()
        {
            if (stateMachine?.RunState?.stats != null)
            {
                var stats = stateMachine.RunState.stats;
                stats.climaticChanges = 50;
                stats.internationalRelations = 50;
                stats.popularApproval = 50;
                stats.economy = 50;
                stats.corruption = 0;
                SyncPresenters(applyCameraEffects: true, instantCamera: true);
                Debug.Log("[RunFlowCoordinator] Atributos resetados para 50!");
            }
        }

        [ContextMenu("Debug/Setar Todos Atributos para 100")]
        public void DebugSetStatsTo100()
        {
            if (stateMachine?.RunState?.stats != null)
            {
                var stats = stateMachine.RunState.stats;
                stats.climaticChanges = 100;
                stats.internationalRelations = 100;
                stats.popularApproval = 100;
                stats.economy = 100;
                stats.corruption = 0;
                SyncPresenters(applyCameraEffects: true, instantCamera: true);
                Debug.Log("[RunFlowCoordinator] Atributos maximizados para 100!");
            }
        }

        [ContextMenu("Debug/Zerar Corrupção")]
        public void DebugZeroCorruption()
        {
            if (stateMachine?.RunState?.stats != null)
            {
                stateMachine.RunState.stats.corruption = 0;
                SyncPresenters(applyCameraEffects: true, instantCamera: true);
                Debug.Log("[RunFlowCoordinator] Corrupção zerada!");
            }
        }

        public void StartFlow()
        {
            SyncPresenters(applyCameraEffects: true, instantCamera: true);
            DrawFirstProposal();
        }

        public void DrawFirstProposal()
        {
            if (stateMachine != null && catalog?.Cards.Count > 0)
            {
                stateMachine.DrawAndPresentProposal(catalog.Cards);
            }
        }

        public void AuthorizeNextVisitor()
        {
            if (!isAwaitingSpaceForNextNpc) return;
            if (modalCoordinator != null && !modalCoordinator.CanCallNextVisitor()) return;
            if (bindings?.EndScreenPresenter != null && bindings.EndScreenPresenter.IsVisible) return;

            isAwaitingSpaceForNextNpc = false;
            bindings?.DeskCallButton?.StartPress();
            StartCoroutine(DrawNextProposalRoutine(0.05f));
        }

        private void HandleProposalReadyOnDesk(CardDefinition card)
        {
            if (card == null || stateMachine == null) return;

            string displayDate = stateMachine.RunState.calendar.DisplayDate;
            int monthIndex = stateMachine.RunState.calendar.CurrentMonthIndex;

            if (tutorialManager != null && tutorialManager.IsTutorialCard(card))
            {
                isDismissingProposal = false;
                isPaperFocused = false;

                if (isPlayerHandRaised)
                {
                    bindings?.PlayDealAnimationReverse();
                    isPlayerHandRaised = false;
                }

                tutorialManager.PresentTutorialStep(card);
                bindings?.RetroMonitorPresenter?.NotifyNewProposal(card);
                bindings?.RetroMonitorPresenter?.UpdateDateDisplay(displayDate);
                return;
            }

            if (tutorialManager != null && tutorialManager.IsTutorialActive)
            {
                tutorialManager.CompleteTutorial();
            }

            isDismissingProposal = false;
            isPaperFocused = false;

            bindings?.PaperPresenter?.SetProposal(card, displayDate);

            if (!isPlayerHandRaised && bindings != null)
            {
                bindings.PlayDealAnimation();
                isPlayerHandRaised = true;
            }

            if (bindings?.DecisionOverlayPresenter != null)
            {
                bindings.DecisionOverlayPresenter.SetVisible(true);
                bindings.DecisionOverlayPresenter.PresentChoices(card);
                bindings.DecisionOverlayPresenter.UpdateDateDisplay(displayDate, monthIndex);
            }

            RefreshPerksUI();

            if (bindings?.RetroMonitorPresenter != null)
            {
                bindings.RetroMonitorPresenter.NotifyNewProposal(card);
                bindings.RetroMonitorPresenter.UpdateDateDisplay(displayDate);
            }

            var calendar = bindings?.CalendarPresenter ?? CalendarWallPresenter.Instance;
            if (calendar != null)
            {
                calendar.SyncWithRun(stateMachine.RunState, catalog?.Events, catalog?.Perks, card);
            }
        }

        private void HandlePlayerChoiceHovered(ChoiceDefinition choice)
        {
            if (enableDecisionHoverPreview && choice != null && bindings?.RetroMonitorPresenter != null)
            {
                bindings.RetroMonitorPresenter.ShowPreviewImpacts(choice.statImpacts);
            }
        }

        private void HandlePlayerChoiceUnhovered()
        {
            bindings?.RetroMonitorPresenter?.ClearPreviewImpacts();
        }

        public void HandlePlayerChoiceSubmitted(int choiceIndex)
        {
            if (stateMachine == null || isProcessingDecision) return;

            StartCoroutine(ProcessPlayerDecisionRoutine(choiceIndex));
        }

        private IEnumerator ProcessPlayerDecisionRoutine(int choiceIndex)
        {
            isProcessingDecision = true;
            isDismissingProposal = true;

            // Desativa imediatamente as interações do carimbo, overlay e papel
            bindings?.StampTool?.SetInspectActive(false);
            bindings?.RetroMonitorPresenter?.ClearPreviewImpacts();
            bindings?.DecisionOverlayPresenter?.SetVisible(false);
            bindings?.PaperPresenter?.SetPaperInteractable(false);

            // 1. Pausa breve só para o jogador ver o papel carimbado
            if (postStampWaitDuration > 0f)
            {
                yield return new WaitForSeconds(postStampWaitDuration);
            }

            // 2. Espera sair do modo de foco no papel (Unfocus da câmera)
            var cam = CameraFocusManager.Instance ?? bindings?.CameraFocus;
            if (cam != null && cam.HasActiveFocus)
            {
                cam.Unfocus();

                float elapsed = 0f;
                while (cam.IsTransitioning && elapsed < cameraUnfocusWaitTimeout)
                {
                    elapsed += Time.deltaTime;
                    yield return null;
                }
            }

            modalCoordinator?.SetContext(InteractionContext.DeskOverview);
            isPaperFocused = false;

            // O papel some logo antes de iniciar a animação do jogador
            bindings?.PaperPresenter?.SetPaperActive(false);

            // 3. Toca a animação da mão do jogador de acordo com a decisão:
            // "Joia" para aprovar (choiceIndex == 0) e "Dislike" para recusar (choiceIndex == 1)
            float animDuration = 0f;
            if (bindings != null)
            {
                animDuration = bindings.PlayDecisionHandAnimation(choiceIndex);
            }

            if (animDuration > 0f)
            {
                yield return new WaitForSeconds(animDuration);
            }

            // 4. No fim da animação, inicia a rotina normal de decisão (atualização no PC, reação do NPC, saída pela porta, etc.)
            isProcessingDecision = false;

            bool isTutorial = IsTutorialCard(stateMachine.CurrentCard);
            bool hasMoreTutorial = isTutorial && HasRemainingTutorialCards();

            if (!isTutorial || !hasMoreTutorial)
            {
                bindings?.PlayDealAnimationReverse();
                isPlayerHandRaised = false;
                bindings?.PaperPresenter?.SetPaperInteractable(false);
            }
            else
            {
                bindings?.PaperPresenter?.SetPaperInteractable(true);
            }

            stateMachine.SubmitChoice(choiceIndex, catalog.Quests, catalog.Perks, catalog.Npcs);
        }

        private void HandleConsequencesResolved(ResolutionReport report)
        {
            if (report == null || stateMachine == null) return;

            bindings?.DecisionOverlayPresenter?.ClearChoices();
            SyncPresenters(report: report, applyCameraEffects: true, instantCamera: false);

            if (bindings?.PresentationCoordinator == null)
            {
                HandleConsequencesFinishedAndAdvance(report);
            }
        }

        private void HandleConsequencesFinishedAndAdvance(ResolutionReport report = null)
        {
            if (stateMachine == null) return;

            if (stateMachine.RunState.termination.IsDefeat || stateMachine.RunState.termination.IsVictory)
                return;

            bool wasTutorial = IsTutorialCard(stateMachine.CurrentCard);
            bool hasMoreTutorial = HasRemainingTutorialCards();

            var monthlyReport = stateMachine.CompleteTurnAndAdvance(catalog.Perks, catalog.Events, catalog.Investments);

            if (stateMachine.RunState.war != null && stateMachine.RunState.war.isActive && stateMachine.RunState.war.monthsInWar == 0 && bindings?.RetroMonitorPresenter != null)
            {
                bindings.RetroMonitorPresenter.SetLogVariantClass("log-alert");
                bindings.RetroMonitorPresenter.LogMessage($"> [DEFESA] Conflito bélico deflagrado! {stateMachine.RunState.war.level.DisplayName()}");
            }

            if (monthlyReport?.war != null && bindings?.RetroMonitorPresenter != null)
            {
                if (monthlyReport.war.maintenanceFailed)
                {
                    bindings.RetroMonitorPresenter.SetLogVariantClass("log-alert");
                    bindings.RetroMonitorPresenter.LogMessage("> [ALERTA MILITAR] Falha de manutenção! Tropas ou Tecnologia esgotadas.");
                }
                else if (monthlyReport.war.offensiveBonusApplied)
                {
                    bindings.RetroMonitorPresenter.SetLogVariantClass("log-system");
                    bindings.RetroMonitorPresenter.LogMessage("> [OFENSIVA] Vantagem tática consolidada (+10% no conflito).");
                }

                if (monthlyReport.war.outcome == WarOutcome.Victory)
                {
                    bindings.RetroMonitorPresenter.SetLogVariantClass("log-system");
                    bindings.RetroMonitorPresenter.LogMessage("> [VITÓRIA TOTAL] Conflito bélico concluído com vitória esmagadora!");
                }
            }

            SyncPresenters(applyCameraEffects: (monthlyReport != null), instantCamera: false);

            if (!wasTutorial)
            {
                SaveCurrentRunState();
            }

            if (stateMachine.RunState.termination.IsDefeat || stateMachine.RunState.termination.IsVictory)
                return;

            if (wasTutorial && hasMoreTutorial)
            {
                isAwaitingSpaceForNextNpc = false;
                StartCoroutine(DrawNextProposalRoutine(0.05f));
            }
            else
            {
                if (wasTutorial && !hasMoreTutorial)
                {
                    tutorialManager?.CompleteTutorial();
                }

                bindings?.PaperPresenter?.SetPaperActive(false);
                isDismissingProposal = false;

                if (requireSpaceToCallNextNpc)
                {
                    isAwaitingSpaceForNextNpc = true;
                }
                else
                {
                    StartCoroutine(DrawNextProposalRoutine(delayBetweenProposals));
                }
            }
        }

        private IEnumerator DrawNextProposalRoutine(float delay = 0.2f)
        {
            yield return new WaitForSeconds(delay);

            if (stateMachine == null || catalog?.Cards.Count <= 0) yield break;

            // Antes de sortear a próxima proposta, verifica se há evento interativo agendado
            string pendingEventId = stateMachine.RunState?.ConsumeScheduledEvent();
            if (!string.IsNullOrEmpty(pendingEventId))
            {
                if (eventRegistry != null && eventRegistry.TryGet(pendingEventId, out var ev))
                {
                    stateMachine.BeginEventPhase();
                    modalCoordinator?.SetContext(InteractionContext.EventMinigame);
                    ev.Begin(stateMachine.RunState, OnInteractiveEventCompleted);
                    yield break; // evento assumiu o controle
                }
                else
                {
                    Debug.LogWarning($"[RunFlowCoordinator] Evento '{pendingEventId}' agendado mas não encontrado no registry. Sorteando proposta normal.");
                }
            }

            stateMachine.DrawAndPresentProposal(catalog.Cards);
        }

        /// <summary>
        /// Chamado pelo evento interativo quando ele conclui (callback de caixa preta).
        /// Aplica o resultado no estado do jogo e retoma o fluxo normal de proposta.
        /// </summary>
        private void OnInteractiveEventCompleted(InteractiveEventResult result)
        {
            if (result == null || stateMachine == null) return;

            // Aplica os efeitos declarados pelo evento
            if (result.wasCompleted)
            {
                var runState = stateMachine.RunState;

                if (result.statImpacts != null)
                    runState.ApplyStatImpacts(result.statImpacts);

                foreach (var perkId in result.grantPerkIds ?? new System.Collections.Generic.List<string>())
                    runState.GrantPerk(perkId);

                if (result.npcRelationDeltas != null)
                {
                    foreach (var kvp in result.npcRelationDeltas)
                    {
                        runState.ModifyNpcRelation(kvp.Key, kvp.Value, catalog?.Npcs);
                    }
                }

                if (!string.IsNullOrEmpty(result.scheduleNextEventId))
                    runState.ScheduleEvent(result.scheduleNextEventId);

                // injectCardIds e removeCardIds são processados pelo DeckState
                if (stateMachine.DeckState != null)
                {
                    foreach (var cardId in result.injectCardIds ?? new System.Collections.Generic.List<string>())
                        stateMachine.DeckState.InjectCard(cardId, onTop: true);

                    foreach (var cardId in result.removeCardIds ?? new System.Collections.Generic.List<string>())
                        stateMachine.DeckState.RemoveCard(cardId);
                }

                // Registra o evento no histórico do calendário
                int eventMonth = stateMachine.RunState.calendar.currentMonthIndex;
                string evTitle = !string.IsNullOrEmpty(result.eventTitle) ? result.eventTitle : "Festa Corporativa";
                string stat1Txt = string.Empty;
                bool stat1Pos = true;
                string stat2Txt = string.Empty;
                bool stat2Pos = true;

                if (result.statImpacts != null)
                {
                    var impacts = new System.Collections.Generic.List<(string text, bool positive)>();
                    if (result.statImpacts.popularApproval != 0)
                        impacts.Add(($"{result.statImpacts.popularApproval:+0;-0} Popularidade", result.statImpacts.popularApproval > 0));
                    if (result.statImpacts.economy != 0)
                        impacts.Add(($"{result.statImpacts.economy:+0;-0} Economia", result.statImpacts.economy > 0));
                    if (result.statImpacts.internationalRelations != 0)
                        impacts.Add(($"{result.statImpacts.internationalRelations:+0;-0} Relações", result.statImpacts.internationalRelations > 0));
                    if (result.statImpacts.climaticChanges != 0)
                        impacts.Add(($"{result.statImpacts.climaticChanges:+0;-0} Clima", result.statImpacts.climaticChanges > 0));
                    if (result.statImpacts.corruption != 0)
                        impacts.Add(($"{result.statImpacts.corruption:+0;-0} Corrupção", result.statImpacts.corruption < 0));

                    if (impacts.Count > 0)
                    {
                        stat1Txt = impacts[0].text;
                        stat1Pos = impacts[0].positive;
                    }
                    if (impacts.Count > 1)
                    {
                        stat2Txt = impacts[1].text;
                        stat2Pos = impacts[1].positive;
                    }
                }

                string perkSum = string.Empty;
                if (result.grantPerkIds != null && result.grantPerkIds.Count > 0)
                {
                    perkSum = $"★ Perk: {result.grantPerkIds[0]}";
                }

                var eventRecord = new MonthDecisionRecord
                {
                    monthIndex = eventMonth,
                    cardId = string.Empty,
                    title = evTitle,
                    npcName = "Lideranças & Empresários",
                    isApproved = result.wasCompleted,
                    choiceLabel = result.wasCompleted ? "Participação Concluída" : "Evento Encerrado",
                    stat1Text = stat1Txt,
                    stat1Positive = stat1Pos,
                    stat2Text = stat2Txt,
                    stat2Positive = stat2Pos,
                    perkText = perkSum,
                    isEvent = true,
                    eventId = result.eventId ?? "FestaCorporativa",
                    eventDetails = "Interações e negociações com lideranças políticas e empresariais."
                };
                stateMachine.RunState.RecordMonthDecision(eventRecord);
            }

            // Retorna ao contexto da mesa e verifica término
            modalCoordinator?.SetContext(InteractionContext.DeskOverview);

            if (stateMachine.RunState.termination.IsDefeat || stateMachine.RunState.termination.IsVictory)
                return;

            // O evento substituiu a proposta: CompleteTurnAndAdvance aceita RunningEvent e avança o mês no calendário
            var monthlyReport = stateMachine.CompleteTurnAndAdvance(catalog.Perks, catalog.Events, catalog.Investments);
            SyncPresenters(applyCameraEffects: (monthlyReport != null), instantCamera: false);

            // Salva as modificações e interações efetuadas durante o evento interativo
            SaveCurrentRunState();

            if (stateMachine.RunState.termination.IsDefeat || stateMachine.RunState.termination.IsVictory)
                return;

            if (requireSpaceToCallNextNpc)
                isAwaitingSpaceForNextNpc = true;
            else
                StartCoroutine(DrawNextProposalRoutine(delayBetweenProposals));
        }

        private void HandleRunTerminated(RunTermination termination)
        {
            if (stateMachine == null) return;

            isAwaitingSpaceForNextNpc = false;
            modalCoordinator?.SetContext(InteractionContext.EndSummary);

            RunSnapshot finalSnapshot = stateMachine.RunState.GetSnapshot();
            EndingDefinition evaluatedEnding = (catalog?.Endings.Count > 0)
                ? EndingEvaluator.EvaluateEnding(stateMachine.RunState, catalog.Endings.Values)
                : null;

            int decisionsCount = stateMachine.RunState?.decisionHistory?.Count ?? 0;
            int finalPopularity = stateMachine.RunState?.stats?.popularApproval ?? 0;

            profileService?.RecordRunCompleted(
                termination.IsVictory,
                evaluatedEnding?.id ?? string.Empty,
                decisionsCount,
                finalPopularity
            );

            SaveSystem.DeleteRunSave();

            bindings?.CameraEffects?.ApplyWarEffects(finalSnapshot?.War, instant: false);
            bindings?.DecisionOverlayPresenter?.ClearChoices();
            bindings?.DecisionOverlayPresenter?.SetVisible(false);
            bindings?.PaperPresenter?.SetPaperActive(false);
            bindings?.PaperPresenter?.SetPaperInteractable(false);
            bindings?.PresentationCoordinator?.DismissCurrentProposal();

            StartRunEndingSequence(termination, finalSnapshot);
        }

        public void StartRunEndingSequence(RunTermination termination, RunSnapshot finalSnapshot)
        {
            Transform camTarget = GetPosCameraPalmas();
            Transform spawnTarget = GetPosPalmas();
            GameObject prefab = GetPalmasPrefab();

            // 1. Move a câmera para a posição do objeto posCameraPalmas
            var camFocus = bindings?.CameraFocus ?? CameraFocusManager.Instance ?? FindFirstObjectByType<CameraFocusManager>();
            if (camFocus != null && camTarget != null)
            {
                camFocus.FocusPoint(camTarget, targetFov: -1f, duration: 1.0f);
            }
            else if (camTarget != null && Camera.main != null)
            {
                Camera.main.transform.SetPositionAndRotation(camTarget.position, camTarget.rotation);
            }

            // 2. Instancia o prefab "Palmas" na posição do objeto posPalmas
            if (prefab != null && spawnTarget != null && activePalmasInstance == null)
            {
                activePalmasInstance = Instantiate(prefab, spawnTarget.position, spawnTarget.rotation);
            }

            // 3. Exibe o diálogo parabenizando o jogador (mesma UI do tutorial via NpcSpeechBubblePresenter)
            var speechBubble = bindings?.SpeechBubblePresenter ?? FindFirstObjectByType<NpcSpeechBubblePresenter>(FindObjectsInactive.Include);
            if (speechBubble != null)
            {
                string speaker = !string.IsNullOrWhiteSpace(endDialogueSpeaker)
                    ? endDialogueSpeaker
                    : (!string.IsNullOrWhiteSpace(tutorialManager?.TutorialNpcName) ? tutorialManager.TutorialNpcName : "ASSESSOR");

                string dialogueText = termination.IsVictory
                    ? (!string.IsNullOrWhiteSpace(victoryEndDialogueText) ? victoryEndDialogueText : "Parabéns, Presidente! Você concluiu seu mandato com sucesso e liderou a nação através de grandes reformas e desafios.")
                    : (!string.IsNullOrWhiteSpace(defeatEndDialogueText) ? defeatEndDialogueText : "Parabéns pelo empenho e dedicação, Presidente! Governar o país é uma missão árdua e você enfrentou grandes decisões até o fim do mandato.");

                speechBubble.Show(speaker, dialogueText, useTypewriter: false, onComplete: () =>
                {
                    // 4. Ao clicar em continuar, fecha o diálogo e abre o relatório com as palmas no fundo
                    speechBubble.Hide();
                    var endScreen = bindings?.EndScreenPresenter ?? FindFirstObjectByType<EndScreenPresenter>(FindObjectsInactive.Include);
                    endScreen?.ShowEndScreen(termination, finalSnapshot);
                });
            }
            else
            {
                var endScreen = bindings?.EndScreenPresenter ?? FindFirstObjectByType<EndScreenPresenter>(FindObjectsInactive.Include);
                endScreen?.ShowEndScreen(termination, finalSnapshot);
            }
        }

        public Transform GetPosCameraPalmas()
        {
            if (posCameraPalmas != null) return posCameraPalmas;
            if (bindings?.PosCameraPalmas != null) return bindings.PosCameraPalmas;
            var obj = GameObject.Find("posCameraPalmas");
            if (obj != null) return obj.transform;
            return null;
        }

        public Transform GetPosPalmas()
        {
            if (posPalmas != null) return posPalmas;
            if (bindings?.PosPalmas != null) return bindings.PosPalmas;
            var obj = GameObject.Find("posPalmas");
            if (obj != null) return obj.transform;
            return null;
        }

        public GameObject GetPalmasPrefab()
        {
            if (palmasPrefab != null) return palmasPrefab;
            if (bindings?.PalmasPrefab != null) return bindings.PalmasPrefab;
#if UNITY_EDITOR
            var loaded = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Palmas.prefab");
            if (loaded != null) return loaded;
#endif
            return null;
        }

        private void CleanupEndingVisuals()
        {
            if (activePalmasInstance != null)
            {
                Destroy(activePalmasInstance);
                activePalmasInstance = null;
            }
        }

        private void SaveCurrentRunState()
        {
            if (stateMachine?.RunState == null) return;
            try
            {
                var saveData = RunSaveData.FromRuntime(stateMachine.RunState, stateMachine.DeckState);
                SaveSystem.SaveRun(saveData);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[RunFlowCoordinator] Erro ao salvar estado da partida: {ex.Message}");
            }
        }

        private void SyncPresenters(RunSnapshot snapshot = null, ResolutionReport report = null, bool applyCameraEffects = true, bool instantCamera = false)
        {
            if (stateMachine?.RunState == null || bindings == null) return;

            var runState = stateMachine.RunState;
            string displayDate = runState.calendar.DisplayDate;
            int monthIndex = runState.calendar.CurrentMonthIndex;
            snapshot ??= runState.GetSnapshot();

            if (bindings.RetroMonitorPresenter != null)
            {
                bindings.RetroMonitorPresenter.UpdateSnapshot(snapshot, report);
                bindings.RetroMonitorPresenter.UpdateDateDisplay(displayDate);
            }

            if (bindings.DecisionOverlayPresenter != null)
            {
                bindings.DecisionOverlayPresenter.SetCorruptionLevel(runState.stats.corruption);
                bindings.DecisionOverlayPresenter.UpdateDateDisplay(displayDate, monthIndex);
                bindings.DecisionOverlayPresenter.RefreshActivePerks(runState.activePerkIds, catalog?.Perks);
            }

            if (applyCameraEffects && bindings.CameraEffects != null)
            {
                bindings.CameraEffects.ApplyAttributeEffects(runState.stats, instant: instantCamera);
                bindings.CameraEffects.ApplyWarEffects(snapshot?.War, instant: instantCamera);
            }

            var calendar = bindings.CalendarPresenter ?? CalendarWallPresenter.Instance;
            if (calendar != null)
            {
                calendar.SyncWithRun(runState, catalog?.Events, catalog?.Perks, stateMachine?.CurrentCard);
            }
        }

        public void RefreshPerksUI()
        {
            if (bindings?.DecisionOverlayPresenter == null || stateMachine?.RunState == null) return;
            bindings.DecisionOverlayPresenter.RefreshActivePerks(stateMachine.RunState.activePerkIds, catalog?.Perks);
        }

        public bool IsTutorialCard(CardDefinition card)
        {
            if (tutorialManager != null) return tutorialManager.IsTutorialCard(card);
            if (card == null) return false;
            if (card.isTutorial) return true;
            return (catalog != null && catalog.IsTutorialCardId(card.id)) ||
                   card.categoryTag.IndexOf("Tutorial", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   card.title.IndexOf("Tutorial", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public bool HasRemainingTutorialCards()
        {
            if (tutorialManager != null) return tutorialManager.HasRemainingTutorialCards();
            if (stateMachine?.DeckState?.priorityDrawPile == null || catalog == null) return false;

            foreach (var cardId in stateMachine.DeckState.priorityDrawPile)
            {
                if (catalog.IsTutorialCardId(cardId)) return true;
            }
            return false;
        }

        public void RestartRun()
        {
            CleanupEndingVisuals();
            if (Application.isPlaying)
            {
                SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            }
        }

        public void ReturnToMenu()
        {
            CleanupEndingVisuals();
            if (!string.IsNullOrEmpty(mainMenuSceneName) && Application.isPlaying)
            {
                SceneManager.LoadScene(mainMenuSceneName);
            }
        }

        public void SubmitWarInvestment(WarAttribute attribute)
        {
            if (stateMachine?.RunState == null) return;
            var result = WarRules.Invest(stateMachine.RunState, attribute);
            if (result.success)
            {
                if (bindings?.RetroMonitorPresenter != null)
                {
                    bindings.RetroMonitorPresenter.SetLogVariantClass("log-positive");
                    int remainingClicks = WarRules.GetRemainingInvestmentClicks(stateMachine.RunState);
                    string clicksSuffix = $" ({remainingClicks} investimento(s) restante(s) no mes)";
                    if (attribute == WarAttribute.Technology)
                        bindings.RetroMonitorPresenter.LogMessage($"> [DEFESA] TECNOLOGIA subiu 1 nivel (-{result.economySpent}% Economia).{clicksSuffix}");
                    else
                        bindings.RetroMonitorPresenter.LogMessage($"> [DEFESA] TROPAS subiram 1 nivel (-{result.popularitySpent}% Populacao).{clicksSuffix}");
                }
                SyncPresenters();
                SaveCurrentRunState();
            }
        }
        public void SurrenderWar()
        {
            if (stateMachine?.RunState == null) return;
            if (WarRules.Surrender(stateMachine.RunState))
            {
                if (bindings?.RetroMonitorPresenter != null)
                {
                    bindings.RetroMonitorPresenter.SetLogVariantClass("log-alert");
                    bindings.RetroMonitorPresenter.LogMessage("> [DEFESA] RENDICAO ASSINADA: O pais tornou-se um protetorado estrangeiro.");
                }
                SyncPresenters();
                SaveCurrentRunState();
            }
        }
    }
}
