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

        public event Action<RunPhase> OnPhaseChanged;
        public event Action<CardDefinition> OnProposalReady;
        public event Action<ResolutionReport> OnConsequencesReady;
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

            CurrentCard = DeckState.DrawNextCard(catalog, RunState.stats, RunState.calendar.currentMonthIndex, rng);
            if (CurrentCard == null)
            {
                return false;
            }

            SetPhase(RunPhase.PresentingProposal);
            OnProposalReady?.Invoke(CurrentCard);
            SetPhase(RunPhase.AwaitingChoice);
            return true;
        }

        public ResolutionReport SubmitChoice(int choiceIndex)
        {
            if (CurrentPhase != RunPhase.AwaitingChoice || CurrentCard == null)
                return null;

            SetPhase(RunPhase.ResolvingChoice);

            LastResolutionReport = DecisionResolver.Resolve(RunState, DeckState, CurrentCard, choiceIndex);

            SetPhase(RunPhase.PresentingConsequences);
            OnConsequencesReady?.Invoke(LastResolutionReport);

            if (LastResolutionReport.IsRunTerminated)
            {
                SetPhase(RunPhase.Terminated);
                OnRunTerminated?.Invoke(LastResolutionReport.resultingTermination);
            }

            return LastResolutionReport;
        }

        public void CompleteTurnAndAdvance()
        {
            if (CurrentPhase != RunPhase.PresentingConsequences)
                return;

            if (RunState.termination.IsDefeat || RunState.termination.IsVictory)
            {
                SetPhase(RunPhase.Terminated);
                OnRunTerminated?.Invoke(RunState.termination);
                return;
            }

            // Não avança o mês caso a proposta seja do tutorial
            bool isTutorialCard = CurrentCard != null && (
                CurrentCard.id.IndexOf("Tutorial", StringComparison.OrdinalIgnoreCase) >= 0 ||
                CurrentCard.categoryTag.IndexOf("Tutorial", StringComparison.OrdinalIgnoreCase) >= 0 ||
                CurrentCard.title.IndexOf("Tutorial", StringComparison.OrdinalIgnoreCase) >= 0);

            if (!isTutorialCard)
            {
                SetPhase(RunPhase.AdvancingTime);
                RunState.AdvanceMonth();

                if (RunState.termination.IsDefeat || RunState.termination.IsVictory)
                {
                    SetPhase(RunPhase.Terminated);
                    OnRunTerminated?.Invoke(RunState.termination);
                    return;
                }
            }

            SetPhase(RunPhase.PreparingRun);
        }

        private void SetPhase(RunPhase phase)
        {
            CurrentPhase = phase;
            OnPhaseChanged?.Invoke(phase);
        }
    }
}
