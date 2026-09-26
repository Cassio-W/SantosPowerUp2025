using System;
using System.Collections.Generic;
using Mandato.Content;
using Mandato.Core;

namespace Mandato.Run
{
    public enum RunPhase
    {
        PreparingRun,
        PresentingProposal,
        AwaitingChoice,
        ResolvingChoice,
        PresentingConsequences,
        RunningEvent,       // Evento interativo em execução — jogo aguarda callback do evento
        AdvancingTime,
        Terminated
    }

    public class RunStateMachine
    {
        public RunState RunState { get; private set; }
        public DeckState DeckState { get; private set; }
        public RunPhase CurrentPhase { get; private set; } = RunPhase.PreparingRun;
        public CardDefinition CurrentCard { get; private set; }
        public ResolutionReport LastResolutionReport { get; private set; }
        public MonthlyEffectsReport LastMonthlyReport { get; private set; }

        public event Action<RunPhase> OnPhaseChanged;
        public event Action<CardDefinition> OnProposalReady;
        public event Action<ResolutionReport> OnConsequencesReady;
        public event Action<MonthlyEffectsReport> OnMonthAdvanced;
        public event Action<RunTermination> OnRunTerminated;

        private Random rng;

        public RunStateMachine(RunState runState = null, DeckState deckState = null, int seed = 0)
        {
            RunState = runState ?? new RunState(seed);
            DeckState = deckState ?? new DeckState();
            rng = seed != 0 ? new Random(seed) : new Random();
        }

        public void StartRun(IEnumerable<string> initialDeckCardIds, int seed = 0, IEnumerable<string> priorityCardIds = null)
        {
            if (seed != 0)
            {
                RunState.seed = seed;
                rng = new Random(seed);
            }

            RunState.Reset();
            DeckState.Initialize(initialDeckCardIds, seed, priorityCardIds);

            SetPhase(RunPhase.PreparingRun);
        }

        public bool DrawAndPresentProposal(IReadOnlyDictionary<string, CardDefinition> catalog)
        {
            if (RunState.termination.IsDefeat || RunState.termination.IsVictory)
            {
                SetPhase(RunPhase.Terminated);
                OnRunTerminated?.Invoke(RunState.termination);
                return false;
            }

            CurrentCard = DeckState.DrawNextCard(
                catalog,
                RunState.stats,
                RunState.calendar.currentMonthIndex,
                rng,
                RunState.activePerkIds,
                RunState.politicalAxis,
                id => RunState.GetNpcRelation(id),
                id => RunState.GetQuestState(id),
                id => RunState.IsNpcAvailable(id)
            );

            if (CurrentCard == null)
            {
                if (RunState.calendar.currentMonthIndex >= RunCalendar.DefaultTotalMonths)
                {
                    RunState.UpdateTermination();
                    if (RunState.termination.IsVictory || RunState.termination.IsDefeat)
                    {
                        SetPhase(RunPhase.Terminated);
                        OnRunTerminated?.Invoke(RunState.termination);
                        return false;
                    }
                }

                // Fallback de contingência explícito: despacho administrativo de rotina
                CurrentCard = CardDefinition.CreateNeutralRoutineCard();
            }

            SetPhase(RunPhase.PresentingProposal);
            OnProposalReady?.Invoke(CurrentCard);
            SetPhase(RunPhase.AwaitingChoice);
            return true;
        }

        public ResolutionReport SubmitChoice(
            int choiceIndex,
            IReadOnlyDictionary<string, QuestDefinition> questCatalog = null,
            IReadOnlyDictionary<string, PerkDefinition> perkCatalog = null)
        {
            if (CurrentPhase != RunPhase.AwaitingChoice || CurrentCard == null)
                return null;

            if (choiceIndex < 0 || choiceIndex > 1)
                return null;

            if (CurrentCard.GetChoice(choiceIndex) == null)
                return null;

            SetPhase(RunPhase.ResolvingChoice);

            LastResolutionReport = DecisionResolver.Resolve(RunState, DeckState, CurrentCard, choiceIndex, questCatalog, perkCatalog);
            if (LastResolutionReport == null)
            {
                SetPhase(RunPhase.AwaitingChoice);
                return null;
            }

            SetPhase(RunPhase.PresentingConsequences);
            OnConsequencesReady?.Invoke(LastResolutionReport);

            if (LastResolutionReport.IsRunTerminated)
            {
                SetPhase(RunPhase.Terminated);
                OnRunTerminated?.Invoke(LastResolutionReport.resultingTermination);
            }

            return LastResolutionReport;
        }

        public MonthlyEffectsReport CompleteTurnAndAdvance(
            IReadOnlyDictionary<string, PerkDefinition> perkCatalog = null,
            IReadOnlyDictionary<string, RunEventDefinition> eventCatalog = null)
        {
            // Aceita PresentingConsequences (fluxo normal de proposta) e
            // RunningEvent (fluxo de evento interativo — o evento substituiu a proposta)
            if (CurrentPhase != RunPhase.PresentingConsequences && CurrentPhase != RunPhase.RunningEvent)
                return null;

            if (RunState.termination.IsDefeat || RunState.termination.IsVictory)
            {
                SetPhase(RunPhase.Terminated);
                OnRunTerminated?.Invoke(RunState.termination);
                return null;
            }

            // Não avança o mês caso a proposta seja do tutorial
            bool isTutorialCard = CurrentCard != null && (
                CurrentCard.isTutorial ||
                CurrentCard.id.IndexOf("Tutorial", StringComparison.OrdinalIgnoreCase) >= 0 ||
                CurrentCard.categoryTag.IndexOf("Tutorial", StringComparison.OrdinalIgnoreCase) >= 0 ||
                CurrentCard.title.IndexOf("Tutorial", StringComparison.OrdinalIgnoreCase) >= 0);

            if (!isTutorialCard)
            {
                SetPhase(RunPhase.AdvancingTime);
                LastMonthlyReport = RunState.AdvanceMonth(perkCatalog, eventCatalog);
                OnMonthAdvanced?.Invoke(LastMonthlyReport);

                if (RunState.termination.IsDefeat || RunState.termination.IsVictory)
                {
                    SetPhase(RunPhase.Terminated);
                    OnRunTerminated?.Invoke(RunState.termination);
                    return LastMonthlyReport;
                }
            }

            SetPhase(RunPhase.PreparingRun);
            return LastMonthlyReport;
        }

        public void DismissCurrentProposal(bool advanceMonth = false, IReadOnlyDictionary<string, PerkDefinition> perkCatalog = null, IReadOnlyDictionary<string, RunEventDefinition> eventCatalog = null)
        {
            CurrentCard = null;

            if (RunState.termination.IsDefeat || RunState.termination.IsVictory)
            {
                SetPhase(RunPhase.Terminated);
                OnRunTerminated?.Invoke(RunState.termination);
                return;
            }

            if (advanceMonth)
            {
                SetPhase(RunPhase.AdvancingTime);
                LastMonthlyReport = RunState.AdvanceMonth(perkCatalog, eventCatalog);
                OnMonthAdvanced?.Invoke(LastMonthlyReport);

                if (RunState.termination.IsDefeat || RunState.termination.IsVictory)
                {
                    SetPhase(RunPhase.Terminated);
                    OnRunTerminated?.Invoke(RunState.termination);
                    return;
                }
            }

            SetPhase(RunPhase.PreparingRun);
        }

        /// <summary>
        /// Transiciona para RunningEvent, indicando que um evento interativo assumiu o controle.
        /// Chamado pelo RunFlowCoordinator antes de entregar o controle ao evento.
        /// </summary>
        public void BeginEventPhase() => SetPhase(RunPhase.RunningEvent);

        /// <summary>
        /// Retorna à fase PreparingRun após a conclusão de um evento interativo.
        /// Chamado pelo RunFlowCoordinator depois de aplicar o InteractiveEventResult.
        /// </summary>
        public void EndEventPhase() => SetPhase(RunPhase.PreparingRun);

        private void SetPhase(RunPhase phase)
        {
            CurrentPhase = phase;
            OnPhaseChanged?.Invoke(phase);
        }
    }
}
