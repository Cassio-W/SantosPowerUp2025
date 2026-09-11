using NUnit.Framework;
using Mandato.Core;
using Mandato.Content;
using UnityEngine;

namespace Mandato.Content.Tests
{
    public class LegacyDealAdapterTests
    {
        // Classe simulada com os mesmos nomes de campo do Deal antigo
        private class MockLegacyDeal : ScriptableObject
        {
            public string Description = "Proposta antiga de teste. Segunda linha.";
            public string leftAnswer = "Aceitar tudo";
            public string rightAnswer = "Não aceitar";
            public bool hasCorruptionMods = true;
            public string tag = "Ambiente";

            public MockAttributes impactsLeft = new MockAttributes { climaticChanges = 20, economy = -15, corruption = 10 };
            public MockAttributes impactsRight = new MockAttributes { climaticChanges = -10, economy = 5 };
        }

        private class MockAttributes
        {
            public int climaticChanges = 0;
            public int internationalRelations = 0;
            public int populationalApproval = 0;
            public int economy = 0;
            public int corruption = 0;
        }

        [Test]
        public void ConvertToCardDefinition_MapsAllLegacyFieldsAccurately()
        {
            var mockDeal = ScriptableObject.CreateInstance<MockLegacyDeal>();
            mockDeal.name = "Deal_Preservacao";

            CardDefinition converted = LegacyDealAdapter.ConvertToCardDefinition(mockDeal);

            Assert.IsNotNull(converted);
            Assert.AreEqual("Deal_Preservacao", converted.id);
            Assert.AreEqual("Deal_Preservacao", converted.title);
            Assert.AreEqual("Proposta antiga de teste. Segunda linha.", converted.description);
            Assert.AreEqual("Ambiente", converted.categoryTag);

            // Left choice (Aprovar / Aceitar)
            Assert.AreEqual("Aceitar tudo", converted.leftChoice.label);
            Assert.AreEqual(20, converted.leftChoice.statImpacts.climaticChanges);
            Assert.AreEqual(-15, converted.leftChoice.statImpacts.economy);
            Assert.AreEqual(10, converted.leftChoice.statImpacts.corruption);
            Assert.IsTrue(converted.leftChoice.hasCorruptionMods);

            // Right choice (Recusar / Rejeitar)
            Assert.AreEqual("Não aceitar", converted.rightChoice.label);
            Assert.AreEqual(-10, converted.rightChoice.statImpacts.climaticChanges);
            Assert.AreEqual(5, converted.rightChoice.statImpacts.economy);
            Assert.IsTrue(converted.rightChoice.hasCorruptionMods);
        }

        [Test]
        public void ConvertToCardDefinition_NullDeal_ReturnsNull()
        {
            CardDefinition result = LegacyDealAdapter.ConvertToCardDefinition(null);
            Assert.IsNull(result);
        }
    }
}
