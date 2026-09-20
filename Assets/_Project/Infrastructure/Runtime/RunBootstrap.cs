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
        /// <summary>
        /// Cria e inicializa uma run completa.
        /// Se <paramref name="selectedCharacter"/> for fornecida, aplica seus overrides de stats,
        /// eixo político, perks iniciais e ações extras ao RunState antes de começar.
        /// </summary>
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
            int customSeed = 0,
            CharacterDefinition selectedCharacter = null)
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

            // Aplica overrides do personagem escolhido após a inicialização padrão
            if (selectedCharacter != null)
            {
                ApplyCharacterToRunState(stateMachine.RunState, selectedCharacter);
            }

            return new RunBootstrapResult(catalog, profileService, stateMachine, seed);
        }

        /// <summary>
        /// Aplica os dados da CharacterDefinition ao RunState já criado:
        /// stats iniciais, eixo político, perks de partida, ações extras e id do personagem.
        /// </summary>
        private static void ApplyCharacterToRunState(RunState runState, CharacterDefinition character)
        {
            if (runState == null || character == null) return;

            runState.activeCharacterId = character.id ?? string.Empty;

            if (character.overrideInitialStats)
            {
                runState.stats = character.initialStats.Clone();
            }

            if (character.overridePoliticalAxis)
            {
                runState.politicalAxis = new PoliticalAxis(character.initialPoliticalX, character.initialPoliticalY, character.lockPoliticalAxis);
            }

            if (character.startingPerkIds != null)
            {
                foreach (var perkId in character.startingPerkIds)
                {
                    if (!string.IsNullOrEmpty(perkId) && !runState.activePerkIds.Contains(perkId))
                        runState.activePerkIds.Add(perkId);
                }
            }

            if (character.extraUnlockedActionIds != null)
            {
                foreach (var actionId in character.extraUnlockedActionIds)
                {
                    if (!string.IsNullOrEmpty(actionId) && !runState.unlockedActionIds.Contains(actionId))
                        runState.unlockedActionIds.Add(actionId);
                }
            }

            if (character.lockedActionIds != null)
            {
                foreach (var actionId in character.lockedActionIds)
                {
                    if (!string.IsNullOrEmpty(actionId))
                        runState.unlockedActionIds.Remove(actionId);
                }
            }

            if (character.initialNpcRelations != null)
            {
                foreach (var rel in character.initialNpcRelations)
                {
                    if (string.IsNullOrEmpty(rel.npcId)) continue;
                    var npcState = runState.GetOrCreateNpcState(rel.npcId);
                    if (npcState != null)
                        npcState.relationship = rel.initialRelation;
                }
            }
        }
    }
}

