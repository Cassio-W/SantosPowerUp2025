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
            var card = CardDefinition.CreateRuntimeInstance(
                id,
                title,
                "Description",
                new ChoiceDefinition("Sim", new StatBlock(5, 0, 0, 0, 0)),
                new ChoiceDefinition("Não", new StatBlock(-5, 0, 0, 0, 0)),
                isTutorial: isTutorial
            );
            return card;
        }

        [Test]
        public void Build_WithTutorialAndMainCards_RegistersCorrectlyAndSetsTutorialFlag()
        {
            var catalog = new RunCatalog();
            var tutCard = CreateCard("tut_1", "Tutorial 1");
            var mainCard = CreateCard("main_1", "Proposta Principal");

            catalog.Build(
                new List<ScriptableObject> { tutCard },
                new List<ScriptableObject> { mainCard },
                null, null, null, null, null,
                playTutorial: true
            );

            Assert.AreEqual(2, catalog.Cards.Count);
            Assert.IsTrue(catalog.TutorialCardIds.Contains("tut_1"));
            Assert.IsTrue(catalog.MainDeckCardIds.Contains("main_1"));
            Assert.IsTrue(catalog.Cards["tut_1"].isTutorial);
            Assert.IsFalse(catalog.Cards["main_1"].isTutorial);
        }

        [Test]
        public void Build_WhenNoActionsProvided_GeneratesDefaultActions()
        {
            var catalog = new RunCatalog();
            catalog.Build(null, null, null, null, null, null, null, playTutorial: false);

            Assert.IsTrue(catalog.Actions.Count >= 3);
            Assert.IsTrue(catalog.Actions.ContainsKey("action_ligar_conselheiro"));
            Assert.IsTrue(catalog.Actions.ContainsKey("action_pacote_emergencial"));
            Assert.IsTrue(catalog.Actions.ContainsKey("action_engavetar_proposta"));
        }

        [Test]
        public void Build_WithPerksAndQuests_RegistersInRespectiveCatalogs()
        {
            var catalog = new RunCatalog();
            var perk = PerkDefinition.CreateRuntimeInstance("perk_agro", "Subsídio Agro", "Descrição", new StatBlock(5, 0, 0, 0, 0));
            var quest = QuestDefinition.CreateRuntimeInstance("quest_cop30", "npc_agro", "Sede da COP30", "Descrição");

            catalog.Build(
                null,
                null,
                new List<PerkDefinition> { perk },
                null,
                new List<QuestDefinition> { quest },
                null,
                null,
                playTutorial: false
            );

            Assert.IsTrue(catalog.Perks.ContainsKey("perk_agro"));
            Assert.IsTrue(catalog.Quests.ContainsKey("quest_cop30"));
        }
    }
}
