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

        private bool isAwaitingSpaceForNextNpc = false;
        private bool isPlayerHandRaised = false;
        private bool isPaperFocused = false;
        private bool isDismissingProposal = false;
        private bool isPcFocused = false;
        private bool isProcessingDecision = false;

        public bool IsAwaitingSpaceForNextNpc => isAwaitingSpaceForNextNpc;
        public bool IsPlayerHandRaised => isPlayerHandRaised;
        public bool IsPaperFocused => isPaperFocused;
        public bool IsDismissingProposal => isDismissingProposal;
        public bool IsPcFocused => isPcFocused;
        public bool IsProcessingDecision => isProcessingDecision;
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
            UIModalCoordinator modalCoordinator = null)
        {
            this.stateMachine = stateMachine;
            this.catalog = catalog;
            this.profileService = profileService;
            this.bindings = bindings;
            this.flipPhoneCoordinator = flipPhoneCoordinator;
            this.modalCoordinator = modalCoordinator ?? new UIModalCoordinator();
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
                bindings.PresentationCoordinator.Bind(stateMachine, catalog.Cards);
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
        }

        private void OnDestroy()
        {
            if (modalCoordinator != null)
                modalCoordinator.OnContextChanged -= HandleContextChanged;

            if (bindings?.DecisionOverlayPresenter != null)
                bindings.DecisionOverlayPresenter.OnBackRequested -= HandleDecisionBackRequested;

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

                case InteractionContext.TutorialStep:
                    bindings?.StampTool?.SetInspectActive(false);
                    if (bindings != null)
                    {
                        bindings.FlipPhonePresenter?.SetInteractable(false);
                        bindings.DeskCallButton?.SetInteractable(false);
                        bindings.DecisionOverlayPresenter?.SetBackVisible(false);
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
                if (modalCoordinator != null && (modalCoordinator.CurrentContext == InteractionContext.PcTerminal || modalCoordinator.CurrentContext == InteractionContext.PaperInspect))
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
            if (report != null && report.success && report.dismissedCurrentProposal)
            {
                DismissCurrentProposal();
            }
        }

        public void DismissCurrentProposal()
        {
            if (stateMachine?.CurrentCard == null) return;

            isDismissingProposal = true;
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

            stateMachine.DismissCurrentProposal(advanceMonth: false, catalog.Perks, catalog.Events);

            if (stateMachine.RunState.termination.IsDefeat || stateMachine.RunState.termination.IsVictory)
                return;

            SyncPresenters(applyCameraEffects: false);
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

        private void Update()
        {
            bool spaceOrEnter = Input.GetKeyDown(callNextNpcKey) ||
                                Input.GetKeyDown(KeyCode.Space) ||
                                Input.GetKeyDown(KeyCode.Return) ||
                                Input.GetKeyDown(KeyCode.KeypadEnter);

            if (spaceOrEnter)
            {
                if (modalCoordinator != null && !modalCoordinator.CanCallNextVisitor()) return;
                if (bindings?.EndScreenPresenter != null && bindings.EndScreenPresenter.IsVisible) return;

                bindings?.DeskCallButton?.PlayPressEffects();

                if (isAwaitingSpaceForNextNpc)
                {
                    AuthorizeNextVisitor();
                }
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
            bindings?.DeskCallButton?.PlayPressEffects();
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

            stateMachine.SubmitChoice(choiceIndex, catalog.Quests, catalog.Perks);
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

            var monthlyReport = stateMachine.CompleteTurnAndAdvance(catalog.Perks, catalog.Events);
            SyncPresenters(applyCameraEffects: (monthlyReport != null), instantCamera: false);

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
            if (stateMachine != null && catalog?.Cards.Count > 0)
            {
                stateMachine.DrawAndPresentProposal(catalog.Cards);
            }
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

            bindings?.DecisionOverlayPresenter?.ClearChoices();
            bindings?.EndScreenPresenter?.ShowEndScreen(termination, finalSnapshot);
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
            if (Application.isPlaying)
            {
                SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            }
        }

        public void ReturnToMenu()
        {
            if (!string.IsNullOrEmpty(mainMenuSceneName) && Application.isPlaying)
            {
                SceneManager.LoadScene(mainMenuSceneName);
            }
        }
    }
}
