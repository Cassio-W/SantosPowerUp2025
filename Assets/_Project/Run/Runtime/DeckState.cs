using System;
using System.Collections.Generic;
using Mandato.Content;
using Mandato.Core;

namespace Mandato.Run
{
    [Serializable]
    public class DeckState
    {
        public List<string> drawPile = new List<string>();
        public List<string> discardPile = new List<string>();
        public List<string> removedCardIds = new List<string>();

        public int TotalActiveCards => drawPile.Count + discardPile.Count;

        public DeckState() { }

        public DeckState(IEnumerable<string> initialCardIds, int seed = 0, IEnumerable<string> priorityCardIds = null)
        {
            Initialize(initialCardIds, seed, priorityCardIds);
        }

        public void Initialize(IEnumerable<string> initialCardIds, int seed = 0, IEnumerable<string> priorityCardIds = null)
        {
            drawPile.Clear();
            discardPile.Clear();
            removedCardIds.Clear();

            if (initialCardIds != null)
            {
                drawPile.AddRange(initialCardIds);
            }

            if (seed != 0)
            {
                Shuffle(new Random(seed));
            }

            // Insere as cartas prioritárias (ex: tutorial) garantidamente no topo na ordem correta
            if (priorityCardIds != null)
            {
                var priorityList = new List<string>(priorityCardIds);
                for (int i = priorityList.Count - 1; i >= 0; i--)
                {
                    drawPile.Insert(0, priorityList[i]);
                }
            }
        }

        public void Shuffle(Random rng)
        {
            if (rng == null || drawPile.Count <= 1) return;

            for (int i = drawPile.Count - 1; i > 0; i--)
            {
                int k = rng.Next(i + 1);
                (drawPile[i], drawPile[k]) = (drawPile[k], drawPile[i]);
            }
        }

        public CardDefinition DrawNextCard(
            IReadOnlyDictionary<string, CardDefinition> catalog,
            StatBlock stats,
            int currentMonth,
            Random rng)
        {
            if (catalog == null || (drawPile.Count == 0 && discardPile.Count == 0))
                return null;

            // Se a pilha de compra esvaziar, reembaralha o descarte
            if (drawPile.Count == 0 && discardPile.Count > 0)
            {
                ReshuffleDiscardIntoDraw(rng);
            }

            // Procura a primeira carta da pilha cujas condições sejam satisfeitas
            for (int i = 0; i < drawPile.Count; i++)
            {
                string cardId = drawPile[i];
                if (catalog.TryGetValue(cardId, out CardDefinition card) && card != null)
                {
                    if (card.AreConditionsMet(stats, currentMonth))
                    {
                        drawPile.RemoveAt(i);
                        discardPile.Add(cardId);
                        return card;
                    }
                }
            }

            // Fallback: se nenhuma carta da pilha passou nas condições, pega a primeira disponível
            if (drawPile.Count > 0)
            {
                string fallbackId = drawPile[0];
                drawPile.RemoveAt(0);
                discardPile.Add(fallbackId);
                catalog.TryGetValue(fallbackId, out CardDefinition fallbackCard);
                return fallbackCard;
            }

            return null;
        }

        public void InjectCard(string cardId, bool onTop = true)
        {
            if (string.IsNullOrEmpty(cardId)) return;

            if (onTop)
            {
                drawPile.Insert(0, cardId);
            }
            else
            {
                drawPile.Add(cardId);
            }
        }

        public void RemoveCard(string cardId)
        {
            if (string.IsNullOrEmpty(cardId)) return;

            drawPile.RemoveAll(id => id == cardId);
            discardPile.RemoveAll(id => id == cardId);

            if (!removedCardIds.Contains(cardId))
            {
                removedCardIds.Add(cardId);
            }
        }

        public void ReshuffleDiscardIntoDraw(Random rng)
        {
            if (discardPile.Count == 0) return;

            drawPile.AddRange(discardPile);
            discardPile.Clear();
            Shuffle(rng);
        }

        public DeckState Clone()
        {
            var clone = new DeckState();
            clone.drawPile.AddRange(drawPile);
            clone.discardPile.AddRange(discardPile);
            clone.removedCardIds.AddRange(removedCardIds);
            return clone;
        }
    }
}
