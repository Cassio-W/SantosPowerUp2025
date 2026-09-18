using System;
using System.Collections;
using System.Collections.Generic;
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
        [Tooltip("Se verdadeiro, o hover dos botões de decisão exibe as setas de impacto no monitor (para perks/habilidades futuras).")]
        [SerializeField] private bool enableDecisionHoverPreview = false;

        private bool isAwaitingSpaceForNextNpc = false;
        private bool isPlayerHandRaised = false;
        private bool isPaperFocused = false;
        private bool isDismissingProposal = false;
        private bool isPcFocused = false;

        public bool IsAwaitingSpaceForNextNpc => isAwaitingSpaceForNextNpc;
        public bool IsPlayerHandRaised => isPlayerHandRaised;
        public bool IsPaperFocused => isPaperFocused;
        public bool IsDismissingProposal => isDismissingProposal;
        public bool IsPcFocused => isPcFocused;
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

            this.tutorialManager = (bindings != null && bindings.TutorialManager != null)
                ? bindings.TutorialManager
                : GetComponent<TutorialManager>() ?? FindFirstObjectByType<TutorialManager>(FindObjectsInactive.Include) ?? gameObject.AddComponent<TutorialManager>();

            var speechBubble = (bindings != null && bindings.SpeechBubblePresenter != null)
                ? bindings.SpeechBubblePresenter
                : FindFirstObjectByType<NpcSpeechBubblePresenter>(FindObjectsInactive.Include);

            var cameraFocus = (bindings != null && bindings.CameraFocus != null)
                ? bindings.CameraFocus
                : CameraFocusManager.Instance ?? FindFirstObjectByType<CameraFocusManager>(FindObjectsInactive.Include);

            this.tutorialManager.Initialize(
                stateMachine,
                catalog,
                speechBubble,
                bindings?.PaperPresenter,
                bindings?.DecisionOverlayPresenter,
                cameraFocus
            );

            Bind();
        }

        private void Bind()
        {
            if (stateMachine == null || bindings == null) return;

            // 1. Apresentação 3D
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

            // 2. Consequências da Máquina de Estados
            stateMachine.OnConsequencesReady += HandleConsequencesResolved;

            // 3. Fim de Run
            stateMachine.OnRunTerminated += HandleRunTerminated;

            // 4. Decisões do Jogador
            if (bindings.DecisionOverlayPresenter != null)
            {
                bindings.DecisionOverlayPresenter.SetModalCoordinator(modalCoordinator);
                bindings.DecisionOverlayPresenter.OnChoiceSelected += HandlePlayerChoiceSubmitted;
                bindings.DecisionOverlayPresenter.OnChoiceHovered += HandlePlayerChoiceHovered;
                bindings.DecisionOverlayPresenter.OnChoiceUnhovered += HandlePlayerChoiceUnhovered;
                bindings.DecisionOverlayPresenter.OnBackRequested += HandleDecisionBackRequested;
            }

            // 5. Modais e Ações do Celular
            if (bindings.FlipPhonePresenter != null)
            {
                bindings.FlipPhonePresenter.OnPhoneOpened += () => modalCoordinator.SetModalState(UIModalCoordinator.MODAL_FLIP_PHONE, true);
                bindings.FlipPhonePresenter.OnPhoneClosed += () => modalCoordinator.SetModalState(UIModalCoordinator.MODAL_FLIP_PHONE, false);
            }

            if (flipPhoneCoordinator != null)
            {
                flipPhoneCoordinator.OnActionExecuted += HandleFlipPhoneActionExecuted;
            }

            // 6. Botões de Fim de Jogo
            if (bindings.EndScreenPresenter != null)
            {
                bindings.EndScreenPresenter.OnRestartRequested += RestartRun;
                bindings.EndScreenPresenter.OnMainMenuRequested += ReturnToMenu;
            }

            // 7. Botão de Mesa (Chamar Visitante)
            if (bindings.DeskCallButton != null)
            {
                bindings.DeskCallButton.OnCallRequested += AuthorizeNextVisitor;
            }

            // 8. Foco de Câmera (PC / Monitor / Papel)
            if (bindings.CameraFocus != null)
            {
                bindings.CameraFocus.OnObjectFocusChanged += HandleObjectFocusChanged;
            }
        }

        private void OnDestroy()
        {
            if (bindings != null && bindings.DecisionOverlayPresenter != null)
            {
                bindings.DecisionOverlayPresenter.OnBackRequested -= HandleDecisionBackRequested;
            }

            if (flipPhoneCoordinator != null)
            {
                flipPhoneCoordinator.OnActionExecuted -= HandleFlipPhoneActionExecuted;
            }

            if (bindings != null && bindings.DeskCallButton != null)
            {
                bindings.DeskCallButton.OnCallRequested -= AuthorizeNextVisitor;
            }

            if (bindings != null && bindings.CameraFocus != null)
            {
                bindings.CameraFocus.OnObjectFocusChanged -= HandleObjectFocusChanged;
            }
        }

        private void HandleDecisionBackRequested()
        {
            if (CameraFocusManager.Instance != null && CameraFocusManager.Instance.HasActiveFocus)
            {
                CameraFocusManager.Instance.Unfocus();
            }
            else if (bindings != null && bindings.CameraFocus != null && bindings.CameraFocus.HasActiveFocus)
            {
                bindings.CameraFocus.Unfocus();
            }
        }

        public void HandleObjectFocusChanged(FocusableObject focusedObject)
        {
            bool isPc = IsMonitorFocusable(focusedObject);
            SetPcFocusState(isPc);

            bool isPaper = focusedObject is PaperFocusableObject;
            bool isPaperOrNonPc = isPaper || (focusedObject != null && !isPc);

            if (isPaper)
            {
                isPaperFocused = true;
                // Desativa a animação do braço levantado instantaneamente ao focar no papel
                if (bindings != null)
                {
                    bindings.ResetPlayerHandImmediate();
                    isPlayerHandRaised = false;
                }
            }
            else if (focusedObject == null)
            {
                // Só restaura a mão levantada se estávamos especificamente com foco ativo no papel,
                // e a proposta continua ativa aguardando decisão na partida (não sendo descartada/respondida)
                if (isPaperFocused)
                {
                    isPaperFocused = false;

                    if (IsProposalActive() && !isPcFocused && !isDismissingProposal)
                    {
                        if (bindings != null && !isPlayerHandRaised)
                        {
                            bindings.PlayDealAnimation();
                            isPlayerHandRaised = true;
                        }
                    }
                }
            }
            else
            {
                isPaperFocused = false;
            }

            if (bindings != null && bindings.DecisionOverlayPresenter != null)
            {
                bindings.DecisionOverlayPresenter.SetBackVisible(isPaper);
            }
        }

        /// <summary>
        /// Verifica se há uma proposta ativa na mesa aguardando decisão do jogador.
        /// </summary>
        public bool IsProposalActive()
        {
            if (stateMachine == null || stateMachine.CurrentCard == null)
                return false;

            if (stateMachine.RunState != null && (stateMachine.RunState.termination.IsDefeat || stateMachine.RunState.termination.IsVictory))
                return false;

            if (isAwaitingSpaceForNextNpc || isDismissingProposal)
                return false;

            if (tutorialManager != null && tutorialManager.IsTutorialActive)
                return false;

            if (IsTutorialCard(stateMachine.CurrentCard))
                return false;

            return stateMachine.CurrentPhase == RunPhase.PresentingProposal ||
                   stateMachine.CurrentPhase == RunPhase.AwaitingChoice;
        }

        public bool IsMonitorFocusable(FocusableObject obj)
        {
            if (obj == null) return false;

            if (bindings != null && bindings.RetroMonitorPresenter != null)
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
            if (isPcFocused == isFocused) return;

            isPcFocused = isFocused;
            modalCoordinator?.SetModalState(UIModalCoordinator.MODAL_PC_FOCUS, isFocused);

            if (isFocused)
            {
                // 1. Celular desativado: se estiver aberto, fecha e bloqueia novas aberturas
                flipPhoneCoordinator?.ClosePhone();
                if (bindings != null)
                {
                    if (bindings.FlipPhonePresenter != null)
                    {
                        bindings.FlipPhonePresenter.SetInteractable(false);
                    }

                    // 2. DecisionUI desativada
                    if (bindings.DecisionOverlayPresenter != null)
                    {
                        bindings.DecisionOverlayPresenter.SetVisible(false);
                    }

                    // 3. Botão de mesa (chamar visitante) desativado
                    if (bindings.DeskCallButton != null)
                    {
                        bindings.DeskCallButton.SetInteractable(false);
                    }
                }
            }
            else
            {
                // 1. Reativa celular (permite abrir quando o jogador desejar, mas NÃO abre automaticamente)
                if (bindings != null)
                {
                    if (bindings.FlipPhonePresenter != null)
                    {
                        bindings.FlipPhonePresenter.SetInteractable(true);
                    }

                    // 2. Reativa DecisionUI
                    if (bindings.DecisionOverlayPresenter != null)
                    {
                        bindings.DecisionOverlayPresenter.SetVisible(true);
                    }

                    // 3. Reativa botão de mesa
                    if (bindings.DeskCallButton != null)
                    {
                        bindings.DeskCallButton.SetInteractable(true);
                    }
                }
            }
        }

        private void HandleFlipPhoneActionExecuted(FlipPhoneUseReport report)
        {
            if (report == null || !report.success) return;

            if (report.dismissedCurrentProposal)
            {
                DismissCurrentProposal();
            }
        }

        public void DismissCurrentProposal()
        {
            if (stateMachine == null || stateMachine.CurrentCard == null) return;

            isDismissingProposal = true;
            isPaperFocused = false;

            // 1. Limpa monitor e botões de decisão
            bindings.RetroMonitorPresenter?.ClearPreviewImpacts();
            if (bindings.DecisionOverlayPresenter != null)
            {
                bindings.DecisionOverlayPresenter.ClearChoices();
            }

            // 2. Fecha celular
            flipPhoneCoordinator?.ClosePhone();

            // 3. Abaixa a mão do jogador e desativa papel 3D
            bindings.PlayDealAnimationReverse();
            isPlayerHandRaised = false;

            if (bindings.PaperPresenter != null)
            {
                bindings.PaperPresenter.SetPaperInteractable(false);
                bindings.PaperPresenter.SetPaperActive(false);
            }

            bindings.CameraFocus?.Unfocus();

            // 4. Apresentação do NPC saindo ou avanço direto
            if (bindings.PresentationCoordinator != null)
            {
                bindings.PresentationCoordinator.DismissCurrentProposal(false, () =>
                {
                    FinishProposalDismissal();
                });
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
            {
                return;
            }

            string displayDate = stateMachine.RunState.calendar.DisplayDate;
            int monthIndex = stateMachine.RunState.calendar.CurrentMonthIndex;

            if (bindings.DecisionOverlayPresenter != null)
            {
                bindings.DecisionOverlayPresenter.UpdateDateDisplay(displayDate, monthIndex);
            }

            if (bindings.RetroMonitorPresenter != null)
            {
                bindings.RetroMonitorPresenter.UpdateSnapshot(stateMachine.RunState.GetSnapshot());
                bindings.RetroMonitorPresenter.UpdateDateDisplay(displayDate);
            }

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
                // Se o monitor estiver focado, modal aberto, celular ou tela final abertos, ignora
                if (isPcFocused) return;
                if (modalCoordinator != null && !modalCoordinator.CanCallNextVisitor()) return;
                if (bindings.EndScreenPresenter != null && bindings.EndScreenPresenter.IsVisible) return;
                if (bindings.FlipPhonePresenter != null && bindings.FlipPhonePresenter.IsOpen) return;

                // Executa os efeitos audiovisuais (som + animação) do botão físico apenas se o botão for interativo
                bindings.DeskCallButton?.PlayPressEffects();

                // Se o fluxo estiver aguardando o próximo visitante, avança
                if (isAwaitingSpaceForNextNpc)
                {
                    AuthorizeNextVisitor();
                }
            }
        }

        public void StartFlow()
        {
            if (stateMachine != null)
            {
                string displayDate = stateMachine.RunState.calendar.DisplayDate;
                int monthIndex = stateMachine.RunState.calendar.CurrentMonthIndex;

                if (bindings.RetroMonitorPresenter != null)
                {
                    bindings.RetroMonitorPresenter.UpdateSnapshot(stateMachine.RunState.GetSnapshot());
                    bindings.RetroMonitorPresenter.UpdateDateDisplay(displayDate);
                }

                if (bindings.DecisionOverlayPresenter != null)
                {
                    bindings.DecisionOverlayPresenter.SetCorruptionLevel(stateMachine.RunState.stats.corruption);
                    bindings.DecisionOverlayPresenter.UpdateDateDisplay(displayDate, monthIndex);
                }
                bindings.CameraEffects?.ApplyAttributeEffects(stateMachine.RunState.stats, instant: true);
            }

            RefreshPerksUI();
            DrawFirstProposal();
        }

        public void DrawFirstProposal()
        {
            if (stateMachine != null && catalog.Cards.Count > 0)
            {
                stateMachine.DrawAndPresentProposal(catalog.Cards);
            }
        }

        public void AuthorizeNextVisitor()
        {
            if (isPcFocused) return;
            if (!isAwaitingSpaceForNextNpc) return;
            if (bindings.FlipPhonePresenter != null && bindings.FlipPhonePresenter.IsOpen) return;
            if (bindings.EndScreenPresenter != null && bindings.EndScreenPresenter.IsVisible) return;
            if (modalCoordinator != null && !modalCoordinator.CanCallNextVisitor()) return;

            isAwaitingSpaceForNextNpc = false;
            bindings.DeskCallButton?.PlayPressEffects();
            StartCoroutine(DrawNextProposalRoutine(0.05f));
        }

        private void HandleProposalReadyOnDesk(CardDefinition card)
        {
            if (card == null || stateMachine == null) return;

            string displayDate = stateMachine.RunState.calendar.DisplayDate;
            int monthIndex = stateMachine.RunState.calendar.CurrentMonthIndex;

            // 0. Propostas de tutorial são exibidas via TutorialManager no NpcSpeechBubblePresenter
            if (tutorialManager != null && tutorialManager.IsTutorialCard(card))
            {
                isDismissingProposal = false;
                isPaperFocused = false;

                if (isPlayerHandRaised)
                {
                    bindings.PlayDealAnimationReverse();
                    isPlayerHandRaised = false;
                }

                tutorialManager.PresentTutorialStep(card);

                if (bindings.RetroMonitorPresenter != null)
                {
                    bindings.RetroMonitorPresenter.NotifyNewProposal(card);
                    bindings.RetroMonitorPresenter.UpdateDateDisplay(displayDate);
                }
                return;
            }

            // Garante finalização do tutorial se passou para carta normal
            if (tutorialManager != null && tutorialManager.IsTutorialActive)
            {
                tutorialManager.CompleteTutorial();
            }

            isDismissingProposal = false;
            isPaperFocused = false;

            // 1. Exibe papel físico 3D
            if (bindings.PaperPresenter != null)
            {
                bindings.PaperPresenter.SetProposal(card, displayDate);
            }

            // 2. Animação do jogador levantando a mão
            if (!isPlayerHandRaised)
            {
                bindings.PlayDealAnimation();
                isPlayerHandRaised = true;
            }

            // 3. Exibe botões de decisão e atualiza data no overlay
            if (bindings.DecisionOverlayPresenter != null)
            {
                bindings.DecisionOverlayPresenter.SetVisible(true);
                bindings.DecisionOverlayPresenter.PresentChoices(card);
                bindings.DecisionOverlayPresenter.UpdateDateDisplay(displayDate, monthIndex);
            }

            RefreshPerksUI();

            // 4. Notifica monitor retrô CRT
            if (bindings.RetroMonitorPresenter != null)
            {
                bindings.RetroMonitorPresenter.NotifyNewProposal(card);
                bindings.RetroMonitorPresenter.UpdateDateDisplay(displayDate);
            }
        }

        private void HandlePlayerChoiceHovered(ChoiceDefinition choice)
        {
            if (!enableDecisionHoverPreview) return;

            if (choice != null && bindings.RetroMonitorPresenter != null)
            {
                bindings.RetroMonitorPresenter.ShowPreviewImpacts(choice.statImpacts);
            }
        }

        private void HandlePlayerChoiceUnhovered()
        {
            if (bindings.RetroMonitorPresenter != null)
            {
                bindings.RetroMonitorPresenter.ClearPreviewImpacts();
            }
        }

        private void HandlePlayerChoiceSubmitted(int choiceIndex)
        {
            if (stateMachine == null) return;

            isDismissingProposal = true;
            isPaperFocused = false;

            bindings.RetroMonitorPresenter?.ClearPreviewImpacts();

            bool isTutorial = IsTutorialCard(stateMachine.CurrentCard);
            bool hasMoreTutorial = isTutorial && HasRemainingTutorialCards();

            if (!isTutorial || !hasMoreTutorial)
            {
                bindings.PlayDealAnimationReverse();
                isPlayerHandRaised = false;

                if (bindings.PaperPresenter != null)
                {
                    bindings.PaperPresenter.SetPaperInteractable(false);
                }

                bindings.CameraFocus?.Unfocus();
            }
            else
            {
                if (bindings.PaperPresenter != null)
                {
                    bindings.PaperPresenter.SetPaperInteractable(true);
                }
            }

            stateMachine.SubmitChoice(choiceIndex, catalog.Quests, catalog.Perks);
        }

        private void HandleConsequencesResolved(ResolutionReport report)
        {
            if (report == null || stateMachine == null) return;

            if (bindings.DecisionOverlayPresenter != null)
            {
                bindings.DecisionOverlayPresenter.ClearChoices();
            }

            if (bindings.RetroMonitorPresenter != null)
            {
                bindings.RetroMonitorPresenter.UpdateSnapshot(stateMachine.RunState.GetSnapshot(), report);
            }

            if (bindings.DecisionOverlayPresenter != null)
            {
                bindings.DecisionOverlayPresenter.SetCorruptionLevel(stateMachine.RunState.stats.corruption);
            }

            RefreshPerksUI();
            bindings.CameraEffects?.ApplyAttributeEffects(stateMachine.RunState.stats);

            // Se não há coordenador 3D para animar saída, avança diretamente
            if (bindings.PresentationCoordinator == null)
            {
                HandleConsequencesFinishedAndAdvance(report);
            }
        }

        private void HandleConsequencesFinishedAndAdvance(ResolutionReport report = null)
        {
            if (stateMachine == null) return;

            if (stateMachine.RunState.termination.IsDefeat || stateMachine.RunState.termination.IsVictory)
            {
                return;
            }

            bool wasTutorial = IsTutorialCard(stateMachine.CurrentCard);
            bool hasMoreTutorial = HasRemainingTutorialCards();

            var monthlyReport = stateMachine.CompleteTurnAndAdvance(catalog.Perks, catalog.Events);
            string displayDate = stateMachine.RunState.calendar.DisplayDate;
            int monthIndex = stateMachine.RunState.calendar.CurrentMonthIndex;

            if (bindings.DecisionOverlayPresenter != null)
            {
                bindings.DecisionOverlayPresenter.UpdateDateDisplay(displayDate, monthIndex);
            }

            if (monthlyReport != null && bindings.RetroMonitorPresenter != null)
            {
                bindings.RetroMonitorPresenter.UpdateSnapshot(stateMachine.RunState.GetSnapshot());
                bindings.RetroMonitorPresenter.UpdateDateDisplay(displayDate);
            }


            if (monthlyReport != null && stateMachine != null)
            {
                bindings.CameraEffects?.ApplyAttributeEffects(stateMachine.RunState.stats, instant: false);
            }

            if (stateMachine.RunState.termination.IsDefeat || stateMachine.RunState.termination.IsVictory)
            {
                return;
            }

            if (wasTutorial && hasMoreTutorial)
            {
                // No tutorial: avança diretamente para a próxima fala do tutorial
                isAwaitingSpaceForNextNpc = false;
                StartCoroutine(DrawNextProposalRoutine(0.05f));
            }
            else
            {
                if (wasTutorial && !hasMoreTutorial)
                {
                    tutorialManager?.CompleteTutorial();
                }

                // Fora do tutorial: recolhe o papel e aguarda a chamada do próximo visitante
                if (bindings.PaperPresenter != null)
                {
                    bindings.PaperPresenter.SetPaperActive(false);
                }

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
            if (stateMachine != null && catalog.Cards.Count > 0)
            {
                stateMachine.DrawAndPresentProposal(catalog.Cards);
            }
        }

        private void HandleRunTerminated(RunTermination termination)
        {
            if (stateMachine == null) return;

            isAwaitingSpaceForNextNpc = false;

            RunSnapshot finalSnapshot = stateMachine.RunState.GetSnapshot();
            EndingDefinition evaluatedEnding = null;

            if (catalog.Endings.Count > 0)
            {
                evaluatedEnding = EndingEvaluator.EvaluateEnding(stateMachine.RunState, catalog.Endings.Values);
            }

            int decisionsCount = stateMachine.RunState?.decisionHistory?.Count ?? 0;
            int finalPopularity = stateMachine.RunState?.stats?.popularApproval ?? 0;

            profileService.RecordRunCompleted(
                termination.IsVictory,
                evaluatedEnding?.id ?? string.Empty,
                decisionsCount,
                finalPopularity
            );

            SaveSystem.DeleteRunSave();

            if (bindings.DecisionOverlayPresenter != null)
            {
                bindings.DecisionOverlayPresenter.ClearChoices();
            }

            if (bindings.EndScreenPresenter != null)
            {
                bindings.EndScreenPresenter.ShowEndScreen(termination, finalSnapshot);
            }
        }

        public void RefreshPerksUI()
        {
            if (bindings.DecisionOverlayPresenter == null || stateMachine == null) return;

            bindings.DecisionOverlayPresenter.RefreshActivePerks(stateMachine.RunState.activePerkIds, catalog.Perks);
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
            if (stateMachine == null || stateMachine.DeckState == null) return false;

            if (stateMachine.DeckState.priorityDrawPile != null)
            {
                foreach (var cardId in stateMachine.DeckState.priorityDrawPile)
                {
                    if (catalog != null && catalog.IsTutorialCardId(cardId)) return true;
                }
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
