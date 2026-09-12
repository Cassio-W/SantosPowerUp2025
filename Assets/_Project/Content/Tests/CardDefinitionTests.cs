using NUnit.Framework;
using Mandato.Core;
using Mandato.Content;

namespace Mandato.Content.Tests
{
    public class CardDefinitionTests
    {
        [Test]
        public void CardCreation_PopulatesFieldsCorrectly()
        {
            var left = new ChoiceDefinition("Aceitar", new StatBlock(10, 5, 10, -15, 5), corruptionMods: true);
            var right = new ChoiceDefinition("Recusar", new StatBlock(0, 0, -5, 10, 0));

            var card = CardDefinition.CreateRuntimeInstance(
                id: "card_reforma_tributaria",
                title: "Reforma Tributária",
                description: "O Congresso propõe simplificar impostos.",
                left: left,
                right: right,
                npcId: "MinistroFazenda",
                tag: "Economia"
            );

            Assert.AreEqual("card_reforma_tributaria", card.id);
            Assert.AreEqual("Reforma Tributária", card.title);
            Assert.AreEqual("MinistroFazenda", card.npcId);
            Assert.AreEqual("Economia", card.categoryTag);
            Assert.AreEqual("Aceitar", card.leftChoice.label);
            Assert.AreEqual(-15, card.leftChoice.statImpacts.economy);
            Assert.IsTrue(card.leftChoice.hasCorruptionMods);
            Assert.AreEqual("Recusar", card.rightChoice.label);
            Assert.AreEqual(10, card.rightChoice.statImpacts.economy);
        }

        [Test]
        public void FormattedDescription_SplitsSentences_AndPreservesAbbreviations()
        {
            string raw = "O Sr. Ministro enviou uma proposta. A inflação subiu. O PIB caiu.";
            string formatted = CardDefinition.FormatSentenceBreaks(raw);

            // "Sr." não deve quebrar a linha, mas os pontos finais de frase devem
            Assert.IsTrue(formatted.Contains("O Sr. Ministro enviou uma proposta.\n"));
            Assert.IsTrue(formatted.Contains("A inflação subiu.\n"));
            Assert.IsTrue(formatted.EndsWith("O PIB caiu."));
        }

        [Test]
        public void CardConditions_EvaluateCorrectly()
        {
            var card = CardDefinition.CreateRuntimeInstance("test", "Teste", "Desc", null, null);
            card.conditions.Add(new CardCondition
            {
                minMonth = 6,
                maxMonth = 24,
                checkStat = true,
                requiredStat = StatId.Economy,
                minStatValue = 40,
                maxStatValue = 100
            });

            var lowEcoStats = new StatBlock(50, 50, 50, 30, 0);
            var healthyEcoStats = new StatBlock(50, 50, 50, 60, 0);

            // Mês fora do intervalo (mês 2)
            Assert.IsFalse(card.AreConditionsMet(healthyEcoStats, 2));

            // Mês dentro do intervalo, mas economia muito baixa
            Assert.IsFalse(card.AreConditionsMet(lowEcoStats, 10));

            // Mês dentro e economia adequada
            Assert.IsTrue(card.AreConditionsMet(healthyEcoStats, 10));
        }

        [Test]
        public void CardConditions_EvaluateRequiredPerk_Correctly()
        {
            var card = CardDefinition.CreateRuntimeInstance("test_perk", "Teste Perk", "Desc", null, null);
            card.conditions.Add(new CardCondition
            {
                requiredPerkId = "perk_alianca_centro"
            });

            var stats = new StatBlock(50, 50, 50, 50, 0);

            // Sem perks ativos
            Assert.IsFalse(card.AreConditionsMet(stats, 1, null));
            Assert.IsFalse(card.AreConditionsMet(stats, 1, new string[] { }));

            // Com perk diferente
            Assert.IsFalse(card.AreConditionsMet(stats, 1, new[] { "perk_outro" }));

            // Com o perk correto
            Assert.IsTrue(card.AreConditionsMet(stats, 1, new[] { "perk_alianca_centro" }));
            // Case-insensitive
            Assert.IsTrue(card.AreConditionsMet(stats, 1, new[] { "PERK_ALIANCA_CENTRO" }));
        }
    }
}
