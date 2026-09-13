using System.Collections.Generic;
using NUnit.Framework;
using Mandato.Content;
using Mandato.Core;
using Mandato.Infrastructure;
using Mandato.Run;

namespace Mandato.Infrastructure.Tests
{
    public class RunBootstrapTests
    {
        [Test]
        public void CreateAndInitializeRun_BuildsCatalogAndInitializesStateMachine()
        {
            var tutorialCard = CardDefinition.CreateRuntimeInstance("tut_01", "Tutorial 1", "Texto", new ChoiceDefinition("Ok"), new ChoiceDefinition("Ok"), isTutorial: true);
            var mainCard1 = CardDefinition.CreateRuntimeInstance("main_01", "Carta 1", "Texto", new ChoiceDefinition("Sim"), new ChoiceDefinition("Não"));
            var mainCard2 = CardDefinition.CreateRuntimeInstance("main_02", "Carta 2", "Texto", new ChoiceDefinition("Sim"), new ChoiceDefinition("Não"));
            var catalogCard = CardDefinition.CreateRuntimeInstance("inject_01", "Carta Injetável", "Texto", new ChoiceDefinition("Sim"), new ChoiceDefinition("Não"));

            var perk = PerkDefinition.CreateRuntimeInstance("perk_01", "Perk 1", "Texto");
            var ev = RunEventDefinition.CreateRuntimeInstance("ev_01", "Evento 1", "Texto");
            var quest = QuestDefinition.CreateRuntimeInstance("quest_01", "npc_01", "Quest 1");
            var ending = EndingDefinition.CreateRuntimeInstance("ending_01", "Final 1", "Texto");
            var action = FlipPhoneActionDefinition.CreateRuntimeInstance("act_01", "Ação 1", "Texto");

            var result = RunBootstrap.CreateAndInitializeRun(
                tutorialCards: new[] { tutorialCard },
                startingCards: new[] { mainCard1, mainCard2 },
                catalogCards: new[] { catalogCard },
                perks: new[] { perk },
                events: new[] { ev },
                quests: new[] { quest },
                endings: new[] { ending },
                startingActions: new[] { action },
                playTutorial: true,
                customSeed: 12345
            );

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Catalog);
            Assert.IsNotNull(result.StateMachine);
            Assert.IsNotNull(result.ProfileService);
            Assert.AreEqual(12345, result.Seed);

            // Valida catálogo
            Assert.AreEqual(4, result.Catalog.Cards.Count);
            Assert.IsTrue(result.Catalog.Cards.ContainsKey("tut_01"));
            Assert.IsTrue(result.Catalog.Cards.ContainsKey("main_01"));
            Assert.IsTrue(result.Catalog.Cards.ContainsKey("inject_01"));
            Assert.AreEqual(1, result.Catalog.Perks.Count);
            Assert.AreEqual(1, result.Catalog.Events.Count);
            Assert.AreEqual(1, result.Catalog.Quests.Count);
            Assert.AreEqual(1, result.Catalog.Endings.Count);
            Assert.AreEqual(1, result.Catalog.Actions.Count);

            // Valida deck da máquina de estados
            Assert.AreEqual(1, result.StateMachine.DeckState.priorityDrawPile.Count);
            Assert.AreEqual("tut_01", result.StateMachine.DeckState.priorityDrawPile[0]);
            Assert.AreEqual(2, result.StateMachine.DeckState.drawPile.Count);
            Assert.IsFalse(result.StateMachine.DeckState.drawPile.Contains("inject_01"), "Carta NonStarting não deve entrar no deck inicial.");
        }

        [Test]
        public void ScenePresentationBindings_Validate_DetectsMissingReferences()
        {
            var bindings = new ScenePresentationBindings();
            bool isValid = bindings.Validate(out var errors);

            Assert.IsFalse(isValid);
            Assert.IsTrue(errors.Count > 0);
            Assert.IsTrue(errors.Exists(e => e.Contains("DecisionOverlayPresenter")));
            Assert.IsTrue(errors.Exists(e => e.Contains("EndScreenPresenter")));
            Assert.IsTrue(errors.Exists(e => e.Contains("PaperDocumentPresenter")));
            Assert.IsTrue(errors.Exists(e => e.Contains("RetroMonitorPresenter")));
            Assert.IsTrue(errors.Exists(e => e.Contains("FlipPhonePresenter")));
            Assert.IsTrue(errors.Exists(e => e.Contains("RunPresentationCoordinator")));
        }
    }
}
