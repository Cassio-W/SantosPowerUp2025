using System;
using System.Collections.Generic;
using Mandato.Content;
using Mandato.Core;

namespace Mandato.Run
{
    [Serializable]
    public class DeckState
    {
        public List<string> priorityDrawPile = new List<string>();
        public List<string> drawPile = new List<string>();
        public List<string> discardPile = new List<string>();
        public List<string> removedCardIds = new List<string>();

        public int TotalActiveCards => priorityDrawPile.Count + drawPile.Count + discardPile.Count;

        public DeckState() { }

        public DeckState(IEnumerable<string> initialCardIds, int seed = 0, IEnumerable<string> priorityCardIds = null)
        {
            Initialize(initialCardIds, seed, priorityCardIds);
        }

        public void Initialize(IEnumerable<string> initialCardIds, int seed = 0, IEnumerable<string> priorityCardIds = null)
        {
            priorityDrawPile.Clear();
            drawPile.Clear();
            discardPile.Clear();
            removedCardIds.Clear();

            if (priorityCardIds != null)
            {
                priorityDrawPile.AddRange(priorityCardIds);
            }

            if (initialCardIds != null)
            {
                drawPile.AddRange(initialCardIds);
            }

            if (seed != 0)
            {
                Shuffle(new Random(seed));
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
            Random rng,
            IEnumerable<string> activePerkIds = null,
            PoliticalAxis politicalAxis = null,
            Func<string, int> getNpcRelation = null,
            Func<string, (int step, bool completed, bool failed)> getQuestState = null)
        {
            if (catalog == null)
                return null;

            if (rng == null)
            {
                rng = new Random();
            }

            // 1. Primeiro verifica se há cartas na fila prioritária (ex: Tutorial ou injetadas no topo)
            for (int i = 0; i < priorityDrawPile.Count; i++)
            {
                string priorityId = priorityDrawPile[i];
                if (catalog.TryGetValue(priorityId, out CardDefinition priorityCard) && priorityCard != null)
                {
                    if (priorityCard.AreConditionsMet(stats, currentMonth, activePerkIds, politicalAxis, getNpcRelation, getQuestState))
                    {
                        priorityDrawPile.RemoveAt(i);
                        discardPile.Add(priorityId);
                        return priorityCard;
                    }
                }
            }

            if (drawPile.Count == 0 && discardPile.Count == 0)
                return null;

            // 2. Se a pilha de compra padrão esvaziar, reembaralha o descarte
            if (drawPile.Count == 0 && discardPile.Count > 0)
            {
                ReshuffleDiscardIntoDraw(rng);
            }

            CardDefinition drawn = TryDrawEligibleCard(catalog, stats, currentMonth, rng, activePerkIds, politicalAxis, getNpcRelation, getQuestState);
            if (drawn != null)
            {
                return drawn;
            }

            // Se não encontrou na pilha de compra mas há descarte, tenta reembaralhar o descarte
            if (discardPile.Count > 0)
            {
                ReshuffleDiscardIntoDraw(rng);
                return TryDrawEligibleCard(catalog, stats, currentMonth, rng, activePerkIds, politicalAxis, getNpcRelation, getQuestState);
            }

            return null;
        }

        private CardDefinition TryDrawEligibleCard(
            IReadOnlyDictionary<string, CardDefinition> catalog,
            StatBlock stats,
            int currentMonth,
            Random rng,
            IEnumerable<string> activePerkIds,
            PoliticalAxis politicalAxis,
            Func<string, int> getNpcRelation,
            Func<string, (int step, bool completed, bool failed)> getQuestState)
        {
            if (drawPile.Count == 0) return null;

            var eligibleIndices = new List<int>();
            var eligibleWeights = new List<int>();
            int totalWeight = 0;

            for (int i = 0; i < drawPile.Count; i++)
            {
                string cardId = drawPile[i];
                if (catalog.TryGetValue(cardId, out CardDefinition card) && card != null)
                {
                    if (card.AreConditionsMet(stats, currentMonth, activePerkIds, politicalAxis, getNpcRelation, getQuestState))
                    {
                        eligibleIndices.Add(i);
                        int weight = Math.Max(1, card.baseWeight);
                        eligibleWeights.Add(weight);
                        totalWeight += weight;
                    }
                }
            }

            if (eligibleIndices.Count == 0 || totalWeight <= 0)
            {
                return null;
            }

            // Sorteio ponderado pelo peso base da carta
            int roll = rng.Next(totalWeight);
            int accumulated = 0;
            int chosenEligibleIndex = 0;

            for (int k = 0; k < eligibleWeights.Count; k++)
            {
                accumulated += eligibleWeights[k];
                if (roll < accumulated)
                {
                    chosenEligibleIndex = k;
                    break;
                }
            }

            int drawIndex = eligibleIndices[chosenEligibleIndex];
            string chosenId = drawPile[drawIndex];
            drawPile.RemoveAt(drawIndex);
            discardPile.Add(chosenId);
            return catalog[chosenId];
        }

        public void InjectCard(string cardId, bool onTop = true, bool forceRestore = false)
        {
            if (string.IsNullOrEmpty(cardId)) return;

            if (removedCardIds.Contains(cardId))
            {
                if (!forceRestore)
                {
                    return;
                }
                removedCardIds.Remove(cardId);
            }

            if (onTop)
            {
                priorityDrawPile.Insert(0, cardId);
            }
            else
            {
                drawPile.Add(cardId);
            }
        }

        public void RemoveCard(string cardId)
        {
            if (string.IsNullOrEmpty(cardId)) return;

            priorityDrawPile.RemoveAll(id => string.Equals(id, cardId, StringComparison.OrdinalIgnoreCase));
            drawPile.RemoveAll(id => string.Equals(id, cardId, StringComparison.OrdinalIgnoreCase));
            discardPile.RemoveAll(id => string.Equals(id, cardId, StringComparison.OrdinalIgnoreCase));

            if (!removedCardIds.Contains(cardId))
            {
                removedCardIds.Add(cardId);
            }
        }

        public List<string> RemoveCardsByNpc(string npcId, IReadOnlyDictionary<string, CardDefinition> catalog)
        {
            if (string.IsNullOrEmpty(npcId) || catalog == null) return new List<string>();

            var toRemove = new List<string>();

            foreach (var cardId in drawPile)
            {
                if (catalog.TryGetValue(cardId, out CardDefinition card) && card != null)
                {
                    if (string.Equals(card.npcId, npcId, StringComparison.OrdinalIgnoreCase) && !toRemove.Contains(cardId))
                    {
                        toRemove.Add(cardId);
                    }
                }
            }

            foreach (var cardId in priorityDrawPile)
            {
                if (catalog.TryGetValue(cardId, out CardDefinition card) && card != null)
                {
                    if (string.Equals(card.npcId, npcId, StringComparison.OrdinalIgnoreCase) && !toRemove.Contains(cardId))
                    {
                        toRemove.Add(cardId);
                    }
                }
            }

            foreach (var cardId in discardPile)
            {
                if (catalog.TryGetValue(cardId, out CardDefinition card) && card != null)
                {
                    if (string.Equals(card.npcId, npcId, StringComparison.OrdinalIgnoreCase) && !toRemove.Contains(cardId))
                    {
                        toRemove.Add(cardId);
                    }
                }
            }

            foreach (var cardId in toRemove)
            {
                RemoveCard(cardId);
            }

            return toRemove;
        }

        public void ReshuffleDiscardIntoDraw(Random rng)
        {
            if (discardPile.Count == 0) return;

            drawPile.AddRange(discardPile);
            discardPile.Clear();
            Shuffle(rng);
        }
    }
}
