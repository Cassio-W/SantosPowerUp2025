using System.Collections.Generic;
using System.Linq;
using Mandato.Content;
using Mandato.Core;
using Mandato.Infrastructure;
using NUnit.Framework;
using UnityEngine;

namespace Mandato.Infrastructure.Tests
{
    [TestFixture]
    public class RunCatalogTests
    {
        private CardDefinition CreateCard(string id, string title, bool isTutorial = false)
        {
            return CardDefinition.CreateRuntimeInstance(
                id,
                title,
                "Description",
                new ChoiceDefinition("Sim", new StatBlock(5, 0, 0, 0, 0)),
                new ChoiceDefinition("Não", new StatBlock(-5, 0, 0, 0, 0)),
                isTutorial: isTutorial
            );
        }

        private PerkDefinition CreatePerk(string id, string title)
        {
            return PerkDefinition.CreateRuntimeInstance(id, title, "Descrição", new StatBlock(1, 0, 0, 0, 0));
        }

        // Helper que preenche os parâmetros não necessários com null
        private static void BuildMinimal(RunCatalog catalog,
            IEnumerable<CardDefinition> tutorial = null,
            IEnumerable<CardDefinition> starting = null,
            IEnumerable<CardDefinition> catalogC = null,
            IEnumerable<PerkDefinition> perks = null,
            IEnumerable<RunEventDefinition> events = null,
            IEnumerable<QuestDefinition> quests = null,
            IEnumerable<EndingDefinition> endings = null,
            IEnumerable<FlipPhoneActionDefinition> actions = null,
            bool playTutorial = true)
        {
            catalog.Build(tutorial, starting, catalogC, perks, events, quests, endings, actions, playTutorial);
        }

        [Test]
        public void Build_WithTutorialAndMainCards_RegistersCorrectlyAndSetsTutorialFlag()
        {
            var catalog = new RunCatalog();
            var tutCard = CreateCard("tut_1", "Tutorial 1");
            var mainCard = CreateCard("main_1", "Proposta Principal");

            BuildMinimal(catalog, tutorial: new[] { tutCard }, starting: new[] { mainCard });

            Assert.AreEqual(2, catalog.Cards.Count);
            Assert.IsTrue(catalog.TutorialCardIds.Contains("tut_1"));
            Assert.IsTrue(catalog.MainDeckCardIds.Contains("main_1"));
            Assert.IsTrue(catalog.Cards["tut_1"].isTutorial);
            Assert.IsFalse(catalog.Cards["main_1"].isTutorial);
        }

        [Test]
        public void Build_WithCatalogCards_RegistersInCatalogButNotInDeck()
        {
            var catalog = new RunCatalog();
            var mainCard = CreateCard("main_1", "Proposta Principal");
            var injectCard = CreateCard("inject_1", "Injetável NonStarting");

            BuildMinimal(catalog, starting: new[] { mainCard }, catalogC: new[] { injectCard });

            Assert.AreEqual(2, catalog.Cards.Count, "Catálogo deve ter 2 cartas registradas");
            Assert.IsTrue(catalog.Cards.ContainsKey("inject_1"), "Carta injetável deve estar no catálogo");
            Assert.IsFalse(catalog.MainDeckCardIds.Contains("inject_1"), "Carta injetável NÃO deve estar no deck inicial");
            Assert.IsTrue(catalog.MainDeckCardIds.Contains("main_1"));
        }

        [Test]
        public void Build_WithPerksAndQuests_RegistersInRespectiveCatalogs()
        {
            var catalog = new RunCatalog();
            var perk = PerkDefinition.CreateRuntimeInstance("perk_agro", "Subsídio Agro", "Descrição", new StatBlock(5, 0, 0, 0, 0));
            var quest = QuestDefinition.CreateRuntimeInstance("quest_cop30", "npc_agro", "Sede da COP30", "Descrição");

            BuildMinimal(catalog, perks: new[] { perk }, quests: new[] { quest }, playTutorial: false);

            Assert.IsTrue(catalog.Perks.ContainsKey("perk_agro"));
            Assert.IsTrue(catalog.Quests.ContainsKey("quest_cop30"));
        }

        [Test]
        public void Build_WithNoActionsProvided_ActionsIsEmpty()
        {
            var catalog = new RunCatalog();
            BuildMinimal(catalog, playTutorial: false);

            // Sem fallback: catálogo vazio é responsabilidade da configuração de cena
            Assert.AreEqual(0, catalog.Actions.Count, "Sem ações configuradas, catálogo deve estar vazio");
        }

        [Test]
        public void Build_WhenPlayTutorialFalse_TutorialCardsNotInDeck()
        {
            var catalog = new RunCatalog();
            var tutCard = CreateCard("tut_1", "Tutorial 1");
            var mainCard = CreateCard("main_1", "Proposta");

            BuildMinimal(catalog, tutorial: new[] { tutCard }, starting: new[] { mainCard }, playTutorial: false);

            Assert.IsFalse(catalog.TutorialCardIds.Contains("tut_1"), "Tutorial desativado: carta tutorial NÃO deve entrar no deck");
            Assert.AreEqual(1, catalog.Cards.Count, "Apenas a carta principal deve estar registrada");
        }

        [Test]
        public void RegisterCard_AddsToCardsButNotToDeck()
        {
            var catalog = new RunCatalog();
            BuildMinimal(catalog, playTutorial: false);

            var extra = CreateCard("extra_1", "Extra");
            catalog.RegisterCard(extra);

            Assert.IsTrue(catalog.Cards.ContainsKey("extra_1"));
            Assert.IsFalse(catalog.MainDeckCardIds.Contains("extra_1"));
        }

        [Test]
        public void CriptoPerk_WhenActive_AppliesMonthlyStatsCorrectly()
        {
            var catalog = new RunCatalog();
            var cripto = PerkDefinition.CreateRuntimeInstance(
                "Cripto",
                "Hub de Criptoativos",
                "Incentivos à economia digital.",
                new StatBlock(0, 1, 0, 0, 1)
            );
            BuildMinimal(catalog, perks: new[] { cripto }, playTutorial: false);

            var runState = new Mandato.Run.RunState();
            int initialRel = runState.stats.internationalRelations;
            int initialCorrupt = runState.stats.corruption;

            runState.GrantPerk("Cripto");
            Assert.IsTrue(runState.activePerkIds.Contains("Cripto"));

            var report = Mandato.Run.MonthlyEffectsResolver.ResolveMonth(runState, catalog.Perks, catalog.Events);

            Assert.IsTrue(report.activePerkIdsApplied.Contains("Cripto"));
            Assert.AreEqual(initialRel + 1, runState.stats.internationalRelations);
            Assert.AreEqual(initialCorrupt + 1, runState.stats.corruption);
        }
    }
}
