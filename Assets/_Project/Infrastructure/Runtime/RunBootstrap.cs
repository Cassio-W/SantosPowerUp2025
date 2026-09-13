using System;
using System.Collections.Generic;
using Mandato.Content;
using Mandato.Core;
using Mandato.Run;

namespace Mandato.Infrastructure
{
    public class RunBootstrapResult
    {
        public RunCatalog Catalog { get; }
        public RunProfileService ProfileService { get; }
        public RunStateMachine StateMachine { get; }
        public int Seed { get; }

        public RunBootstrapResult(RunCatalog catalog, RunProfileService profileService, RunStateMachine stateMachine, int seed)
        {
            Catalog = catalog;
            ProfileService = profileService;
            StateMachine = stateMachine;
            Seed = seed;
        }
    }

    public static class RunBootstrap
    {
        public static RunBootstrapResult CreateAndInitializeRun(
            IEnumerable<CardDefinition> tutorialCards,
            IEnumerable<CardDefinition> startingCards,
            IEnumerable<CardDefinition> catalogCards,
            IEnumerable<PerkDefinition> perks,
            IEnumerable<RunEventDefinition> events,
            IEnumerable<QuestDefinition> quests,
            IEnumerable<EndingDefinition> endings,
            IEnumerable<FlipPhoneActionDefinition> startingActions,
            bool playTutorial = true,
            int customSeed = 0)
        {
            var catalog = new RunCatalog();
            catalog.Build(
                tutorialCards,
                startingCards,
                catalogCards,
                perks,
                events,
                quests,
                endings,
                startingActions,
                playTutorial
            );

            var profileService = new RunProfileService();
            profileService.InitializeProfile();

            int seed = customSeed != 0 ? customSeed : new Random().Next(1, 100000);
            var priorityIds = (playTutorial && catalog.TutorialCardIds.Count > 0) ? catalog.TutorialCardIds : null;

            var stateMachine = new RunStateMachine(seed: seed);
            stateMachine.StartRun(catalog.MainDeckCardIds, seed, priorityIds);

            return new RunBootstrapResult(catalog, profileService, stateMachine, seed);
        }
    }
}
