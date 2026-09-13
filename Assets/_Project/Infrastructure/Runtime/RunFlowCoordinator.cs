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

        [Header("Configuração de Fluxo")]
        [SerializeField] private bool requireSpaceToCallNextNpc = true;
        [SerializeField] private KeyCode callNextNpcKey = KeyCode.Space;
        [SerializeField] private float delayBetweenProposals = 1.5f;
        [SerializeField] private string mainMenuSceneName = "MenuV2";

        private bool isAwaitingSpaceForNextNpc = false;
        private bool isPlayerHandRaised = false;

        public bool IsAwaitingSpaceForNextNpc => isAwaitingSpaceForNextNpc;
        public UIModalCoordinator ModalCoordinator => modalCoordinator;

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
            }

            // 5. Modais do Celular
            if (bindings.FlipPhonePresenter != null)
            {
                bindings.FlipPhonePresenter.OnPhoneOpened += () => modalCoordinator.SetModalState(UIModalCoordinator.MODAL_FLIP_PHONE, true);
                bindings.FlipPhonePresenter.OnPhoneClosed += () => modalCoordinator.SetModalState(UIModalCoordinator.MODAL_FLIP_PHONE, false);
            }

            // 6. Botões de Fim de Jogo
            if (bindings.EndScreenPresenter != null)
            {
                bindings.EndScreenPresenter.OnRestartRequested += RestartRun;
                bindings.EndScreenPresenter.OnMainMenuRequested += ReturnToMenu;
            }
        }

        private void Update()
        {
            if (isAwaitingSpaceForNextNpc)
            {
                if (modalCoordinator != null && !modalCoordinator.CanCallNextVisitor()) return;

                bool spaceOrEnter = Input.GetKeyDown(callNextNpcKey) ||
                                    Input.GetKeyDown(KeyCode.Space) ||
                                    Input.GetKeyDown(KeyCode.Return) ||
                                    Input.GetKeyDown(KeyCode.KeypadEnter);

                if (spaceOrEnter)
                {
                    AuthorizeNextVisitor();
                }
            }
        }

        public void StartFlow()
        {
            if (bindings.RetroMonitorPresenter != null && stateMachine != null)
            {
                bindings.RetroMonitorPresenter.UpdateSnapshot(stateMachine.RunState.GetSnapshot());
            }

            if (bindings.DecisionOverlayPresenter != null && stateMachine != null)
            {
                bindings.DecisionOverlayPresenter.SetCorruptionLevel(stateMachine.RunState.stats.corruption);
            }

            RefreshPerksUI();
            if (stateMachine != null)
            {
                bindings.CameraEffects?.ApplyAttributeEffects(stateMachine.RunState.stats, instant: true);
            }
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
            if (!isAwaitingSpaceForNextNpc) return;
            if (bindings.FlipPhonePresenter != null && bindings.FlipPhonePresenter.IsOpen) return;
            if (bindings.EndScreenPresenter != null && bindings.EndScreenPresenter.IsVisible) return;

            isAwaitingSpaceForNextNpc = false;
            StartCoroutine(DrawNextProposalRoutine(0.05f));
        }

        private void HandleProposalReadyOnDesk(CardDefinition card)
        {
            if (card == null || stateMachine == null) return;

            string displayDate = stateMachine.RunState.calendar.DisplayDate;

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

            // 3. Exibe botões de decisão
            if (bindings.DecisionOverlayPresenter != null)
            {
                bindings.DecisionOverlayPresenter.PresentChoices(card);
            }

            RefreshPerksUI();

            // 4. Notifica monitor retrô CRT
            if (bindings.RetroMonitorPresenter != null)
            {
                bindings.RetroMonitorPresenter.NotifyNewProposal(card);
                bindings.RetroMonitorPresenter.UpdateDateDisplay(displayDate);
            }
        }

        private void HandlePlayerChoiceSubmitted(int choiceIndex)
        {
            if (stateMachine == null) return;

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

            if (monthlyReport != null && bindings.RetroMonitorPresenter != null)
            {
                bindings.RetroMonitorPresenter.UpdateSnapshot(stateMachine.RunState.GetSnapshot());
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
                // No tutorial: o papel permanece ativo na mesa e avança diretamente sem exigir Espaço
                isAwaitingSpaceForNextNpc = false;
                StartCoroutine(DrawNextProposalRoutine(0.05f));
            }
            else
            {
                // Fora do tutorial (ou fim do tutorial): recolhe o papel e aguarda a chamada do próximo visitante
                if (bindings.PaperPresenter != null)
                {
                    bindings.PaperPresenter.SetPaperActive(false);
                }

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

            profileService.RecordRunCompleted(
                termination.IsVictory,
                evaluatedEnding?.id ?? string.Empty
            );

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
            if (card == null) return false;
            if (card.isTutorial) return true;
            return catalog.IsTutorialCardId(card.id) ||
                   card.categoryTag.IndexOf("Tutorial", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   card.title.IndexOf("Tutorial", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public bool HasRemainingTutorialCards()
        {
            if (stateMachine == null || stateMachine.DeckState == null) return false;

            if (stateMachine.DeckState.priorityDrawPile != null)
            {
                foreach (var cardId in stateMachine.DeckState.priorityDrawPile)
                {
                    if (catalog.IsTutorialCardId(cardId)) return true;
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
