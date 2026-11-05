using System.Collections.Generic;
using NUnit.Framework;
using Mandato.Content;
using Mandato.Core;
using Mandato.Run;

namespace Mandato.Run.Tests
{
    public class CharacterApplicatorTests
    {
        [Test]
        public void Apply_SetsActiveCharacterId()
        {
            var character = CharacterDefinition.CreateRuntimeInstance("char_economist", "Economista Pragmatico");
            var state = new RunState();

            CharacterApplicator.Apply(character, state);

            Assert.AreEqual("char_economist", state.activeCharacterId);
        }

        [Test]
        public void Apply_WithCustomStats_OverridesRunStats()
        {
            var customStats = new StatBlock(
                climate: 30,
                relations: 70,
                approval: 45,
                eco: 80,
                corrupt: 10
            );

            var character = CharacterDefinition.CreateRuntimeInstance(
                "char_tech",
                "Tecnocrata",
                stats: customStats
            );

            var state = new RunState();
            CharacterApplicator.Apply(character, state);

            Assert.AreEqual(30, state.stats.climaticChanges);
            Assert.AreEqual(70, state.stats.internationalRelations);
            Assert.AreEqual(45, state.stats.popularApproval);
            Assert.AreEqual(80, state.stats.economy);
            Assert.AreEqual(10, state.stats.corruption);
        }

        [Test]
        public void Apply_WithPoliticalAxis_OverridesAndLocksIfConfigured()
        {
            var character = CharacterDefinition.CreateRuntimeInstance(
                "char_diplomat",
                "Diplomata",
                polX: 4,
                polY: -3,
                lockPol: true
            );

            var state = new RunState();
            CharacterApplicator.Apply(character, state);

            Assert.AreEqual(4, state.politicalAxis.x);
            Assert.AreEqual(-3, state.politicalAxis.y);
            Assert.IsTrue(state.politicalAxis.isLocked);

            // Tentar aplicar delta não deve alterar devido ao lock
            state.ApplyPoliticalDelta(5, 5);
            Assert.AreEqual(4, state.politicalAxis.x);
            Assert.AreEqual(-3, state.politicalAxis.y);
        }

        [Test]
        public void Apply_WithStartingPerks_GrantsPerksExceptBlockedOnes()
        {
            var character = CharacterDefinition.CreateRuntimeInstance("char_green", "Ambientalista");
            character.startingPerkIds = new List<string> { "PerkEcoBonus", "PerkDiplomacia", "PerkProibido" };
            character.blockedPerkIds = new List<string> { "PerkProibido" };

            var state = new RunState();
            CharacterApplicator.Apply(character, state);

            Assert.IsTrue(state.activePerkIds.Contains("PerkEcoBonus"));
            Assert.IsTrue(state.activePerkIds.Contains("PerkDiplomacia"));
            Assert.IsFalse(state.activePerkIds.Contains("PerkProibido"));
        }

        [Test]
        public void Apply_WithExtraAndLockedActions_UpdatesUnlockedActionIds()
        {
            var character = CharacterDefinition.CreateRuntimeInstance("char_hawk", "Linha-Dura");
            character.extraUnlockedActionIds = new List<string> { "action_military_aid", "action_press_leak" };
            character.lockedActionIds = new List<string> { "action_popular_rally" };

            var state = new RunState();
            // Simula uma ação que vinha desbloqueada por padrão
            state.UnlockAction("action_popular_rally");
            Assert.IsTrue(state.IsActionUnlocked("action_popular_rally"));

            CharacterApplicator.Apply(character, state);

            Assert.IsTrue(state.IsActionUnlocked("action_military_aid"));
            Assert.IsTrue(state.IsActionUnlocked("action_press_leak"));
            Assert.IsFalse(state.IsActionUnlocked("action_popular_rally"));
        }

        [Test]
        public void Apply_WithNpcRelationOverrides_SetsInitialRelationScores()
        {
            var character = CharacterDefinition.CreateRuntimeInstance("char_negotiator", "Articulador");
            character.initialNpcRelations = new List<NpcRelationOverride>
            {
                new NpcRelationOverride("npc_deputado", 40),
                new NpcRelationOverride("npc_sindicalista", -20)
            };

            var state = new RunState();
            CharacterApplicator.Apply(character, state);

            Assert.AreEqual(40, state.GetNpcRelation("npc_deputado"));
            Assert.AreEqual(-20, state.GetNpcRelation("npc_sindicalista"));
            Assert.AreEqual(0, state.GetNpcRelation("npc_desconhecido"));
        }

        [Test]
        public void SaveAndLoad_PreservesActiveCharacterId()
        {
            var character = CharacterDefinition.CreateRuntimeInstance("char_leader", "Líder Popular");
            var state = new RunState();
            CharacterApplicator.Apply(character, state);

            var deck = new DeckState();
            var saveData = RunSaveData.FromRuntime(state, deck);

            Assert.AreEqual("char_leader", saveData.activeCharacterId);

            var restoredState = new RunState();
            var restoredDeck = new DeckState();
            saveData.ApplyToRuntime(restoredState, restoredDeck);

            Assert.AreEqual("char_leader", restoredState.activeCharacterId);
        }

        [Test]
        public void Apply_WithStartingActionCharges_SetsMultiChargeSupport()
        {
            var character = CharacterDefinition.CreateRuntimeInstance("char_buxo", "Buxollini");
            character.startingActionCharges = new List<StartingActionCharge>
            {
                new StartingActionCharge { actionId = "action_policia_federal", charges = 3 },
                new StartingActionCharge { actionId = "action_assassino_aluguel", charges = 1 }
            };

            var state = new RunState();
            CharacterApplicator.Apply(character, state);

            Assert.IsTrue(state.IsActionUnlocked("action_policia_federal"));
            Assert.AreEqual(3, state.GetActionCharges("action_policia_federal"));
            Assert.IsTrue(state.IsActionUnlocked("action_assassino_aluguel"));
            Assert.AreEqual(1, state.GetActionCharges("action_assassino_aluguel"));
        }

        [Test]
        public void Apply_WithRandomization_RandomizesStatsPerksAndActions()
        {
            var character = CharacterDefinition.CreateRuntimeInstance("char_caneta", "Caneta Vermelha");
            character.randomizeInitialStats = true;
            character.minRandomStatValue = 30;
            character.maxRandomStatValue = 70;
            character.initialStats.corruption = 0;
            character.randomStartingPerksCount = 1;
            character.randomStartingActionsCount = 1;

            var perkCatalog = new Dictionary<string, PerkDefinition>
            {
                { "Cripto", PerkDefinition.CreateRuntimeInstance("Cripto", "Cripto", "Desc") },
                { "Imorrivel", PerkDefinition.CreateRuntimeInstance("Imorrivel", "Imorrível", "Desc") }
            };
            var actionCatalog = new Dictionary<string, FlipPhoneActionDefinition>
            {
                { "action_pf", FlipPhoneActionDefinition.CreateRuntimeInstance("action_pf", "PF", "Desc", unlockByDefault: false) },
                { "action_influencers", FlipPhoneActionDefinition.CreateRuntimeInstance("action_influencers", "Influencers", "Desc", unlockByDefault: false) }
            };

            var state = new RunState(seed: 999);
            CharacterApplicator.Apply(character, state, perkCatalog, actionCatalog);

            Assert.GreaterOrEqual(state.stats.climaticChanges, 30);
            Assert.LessOrEqual(state.stats.climaticChanges, 70);
            Assert.GreaterOrEqual(state.stats.economy, 30);
            Assert.LessOrEqual(state.stats.economy, 70);
            Assert.AreEqual(0, state.stats.corruption);
            Assert.AreEqual(1, state.activePerks.Count);
            Assert.AreEqual(1, state.unlockedActionIds.Count);
        }

        [Test]
        public void Apply_WithRandomCorruption_RandomizesCorruptionInRange()
        {
            var character = CharacterDefinition.CreateRuntimeInstance("char_caneta_corrupt", "Caneta Vermelha");
            character.randomizeInitialStats = true;
            character.minRandomStatValue = 20;
            character.maxRandomStatValue = 80;
            character.randomizeCorruption = true;
            character.minRandomCorruptionValue = 10;
            character.maxRandomCorruptionValue = 40;

            var state = new RunState(seed: 12345);
            CharacterApplicator.Apply(character, state);

            Assert.GreaterOrEqual(state.stats.climaticChanges, 20);
            Assert.LessOrEqual(state.stats.climaticChanges, 80);
            Assert.GreaterOrEqual(state.stats.corruption, 10);
            Assert.LessOrEqual(state.stats.corruption, 40);
        }
    }
}
