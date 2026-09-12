using System;
using System.Collections.Generic;
using NUnit.Framework;
using Mandato.Content;
using Mandato.Core;
using Mandato.Run;

namespace Mandato.Run.Tests
{
    public class DeckStateTests
    {
        private Dictionary<string, CardDefinition> catalog;

        [SetUp]
        public void Setup()
        {
            catalog = new Dictionary<string, CardDefinition>
            {
                ["c1"] = CardDefinition.CreateRuntimeInstance("c1", "Carta 1", "Desc 1", null, null),
                ["c2"] = CardDefinition.CreateRuntimeInstance("c2", "Carta 2", "Desc 2", null, null),
                ["c3"] = CardDefinition.CreateRuntimeInstance("c3", "Carta 3", "Desc 3", null, null)
            };
        }

        [Test]
        public void Initialization_SetsDrawPileCorrectly()
        {
            var deck = new DeckState(new[] { "c1", "c2", "c3" });

            Assert.AreEqual(3, deck.drawPile.Count);
            Assert.AreEqual(0, deck.discardPile.Count);
            Assert.AreEqual(3, deck.TotalActiveCards);
        }

        [Test]
        public void DrawNextCard_MovesCardFromDrawToDiscard()
        {
            var deck = new DeckState(new[] { "c1", "c2" });
            var stats = new StatBlock();
            var rng = new Random(42);

            CardDefinition drawn = deck.DrawNextCard(catalog, stats, 1, rng);

            Assert.IsNotNull(drawn);
            Assert.AreEqual(1, deck.drawPile.Count);
            Assert.AreEqual(1, deck.discardPile.Count);
            Assert.AreEqual(drawn.id, deck.discardPile[0]);
        }

        [Test]
        public void DrawNextCard_ReshufflesDiscard_WhenDrawPileEmpty()
        {
            var deck = new DeckState(new[] { "c1" });
            var stats = new StatBlock();
            var rng = new Random(42);

            // Primeira compra esvazia o draw pile
            CardDefinition first = deck.DrawNextCard(catalog, stats, 1, rng);
            Assert.AreEqual(0, deck.drawPile.Count);
            Assert.AreEqual(1, deck.discardPile.Count);

            // Segunda compra força reembaralhamento do descarte
            CardDefinition second = deck.DrawNextCard(catalog, stats, 1, rng);
            Assert.IsNotNull(second);
            Assert.AreEqual(first.id, second.id);
            Assert.AreEqual(0, deck.drawPile.Count);
            Assert.AreEqual(1, deck.discardPile.Count);
        }

        [Test]
        public void InjectAndRemoveCard_ModifiesDeckAccurately()
        {
            var deck = new DeckState(new[] { "c1" });

            deck.InjectCard("c_injected", onTop: true);
            Assert.IsTrue(deck.priorityDrawPile.Contains("c_injected"));

            deck.RemoveCard("c1");
            Assert.IsFalse(deck.drawPile.Contains("c1"));
            Assert.IsTrue(deck.removedCardIds.Contains("c1"));
        }

        [Test]
        public void DrawNextCard_FiltersIneligibleCards_AndReturnsNull_WhenNoCardEligible()
        {
            var cardIneligible = CardDefinition.CreateRuntimeInstance("c_blocked", "Bloqueada", "Desc", null, null);
            cardIneligible.conditions.Add(new CardCondition
            {
                minMonth = 20 // Estamos no mês 1
            });

            var localCatalog = new Dictionary<string, CardDefinition>
            {
                ["c_blocked"] = cardIneligible
            };

            var deck = new DeckState(new[] { "c_blocked" });
            var stats = new StatBlock();
            var rng = new Random(123);

            // Não deve retornar carta com condição falha
            CardDefinition drawn = deck.DrawNextCard(localCatalog, stats, currentMonth: 1, rng: rng);
            Assert.IsNull(drawn);
            Assert.AreEqual(1, deck.drawPile.Count);
            Assert.AreEqual(0, deck.discardPile.Count);
        }

        [Test]
        public void DrawNextCard_UsesWeightedSelection_DeterministicallyWithSeed()
        {
            var heavyCard = CardDefinition.CreateRuntimeInstance("c_heavy", "Pesada", "Desc", null, null);
            heavyCard.baseWeight = 1000;

            var lightCard = CardDefinition.CreateRuntimeInstance("c_light", "Leve", "Desc", null, null);
            lightCard.baseWeight = 1;

            var localCatalog = new Dictionary<string, CardDefinition>
            {
                ["c_heavy"] = heavyCard,
                ["c_light"] = lightCard
            };

            int heavyDrawnCount = 0;
            int totalTrials = 100;

            for (int i = 0; i < totalTrials; i++)
            {
                var deck = new DeckState(new[] { "c_heavy", "c_light" });
                var rng = new Random(i + 1);
                var drawn = deck.DrawNextCard(localCatalog, new StatBlock(), 1, rng);
                if (drawn.id == "c_heavy") heavyDrawnCount++;
            }

            // Com peso 1000 vs 1, a carta pesada deve ser sorteada na imensa maioria das vezes (>90%)
            Assert.GreaterOrEqual(heavyDrawnCount, 90);
        }

        [Test]
        public void InjectCard_DoesNotReinject_PermanentlyRemovedCard_UnlessForced()
        {
            var deck = new DeckState(new[] { "c1", "c2" });
            deck.RemoveCard("c1");

            Assert.IsTrue(deck.removedCardIds.Contains("c1"));

            // Tentativa padrão de reinjeção é bloqueada
            deck.InjectCard("c1", onTop: true, forceRestore: false);
            Assert.IsFalse(deck.priorityDrawPile.Contains("c1") || deck.drawPile.Contains("c1"));

            // Tentativa com forceRestore restaura
            deck.InjectCard("c1", onTop: true, forceRestore: true);
            Assert.IsTrue(deck.priorityDrawPile.Contains("c1") || deck.drawPile.Contains("c1"));
            Assert.IsFalse(deck.removedCardIds.Contains("c1"));
        }
    }
}
