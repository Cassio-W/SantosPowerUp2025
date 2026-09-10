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
            Assert.AreEqual("c_injected", deck.drawPile[0]);

            deck.RemoveCard("c1");
            Assert.IsFalse(deck.drawPile.Contains("c1"));
            Assert.IsTrue(deck.removedCardIds.Contains("c1"));
        }
    }
}
