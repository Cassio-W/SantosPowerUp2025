using System;
using System.Collections.Generic;
using Mandato.Content;
using Mandato.Core;
using UnityEngine;

namespace Mandato.Infrastructure
{
    public class RunCatalog
    {
        private readonly Dictionary<string, CardDefinition> cards = new Dictionary<string, CardDefinition>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, PerkDefinition> perks = new Dictionary<string, PerkDefinition>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, RunEventDefinition> events = new Dictionary<string, RunEventDefinition>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, QuestDefinition> quests = new Dictionary<string, QuestDefinition>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, EndingDefinition> endings = new Dictionary<string, EndingDefinition>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, FlipPhoneActionDefinition> actions = new Dictionary<string, FlipPhoneActionDefinition>(StringComparer.OrdinalIgnoreCase);

        private readonly List<string> tutorialCardIds = new List<string>();
        private readonly List<string> mainDeckCardIds = new List<string>();

        public IReadOnlyDictionary<string, CardDefinition> Cards => cards;
        public IReadOnlyDictionary<string, PerkDefinition> Perks => perks;
        public IReadOnlyDictionary<string, RunEventDefinition> Events => events;
        public IReadOnlyDictionary<string, QuestDefinition> Quests => quests;
        public IReadOnlyDictionary<string, EndingDefinition> Endings => endings;
        public IReadOnlyDictionary<string, FlipPhoneActionDefinition> Actions => actions;

        public IReadOnlyList<string> TutorialCardIds => tutorialCardIds;
        public IReadOnlyList<string> MainDeckCardIds => mainDeckCardIds;

        public bool IsTutorialCardId(string cardId)
        {
            if (string.IsNullOrEmpty(cardId)) return false;
            return tutorialCardIds.Contains(cardId);
        }

        /// <summary>
        /// Constrói o catálogo a partir de CardDefinition nativos.
        /// tutorialCards  → entram no deck com prioridade (tutorial)
        /// startingCards  → entram no deck principal
        /// catalogCards   → registrados no catálogo mas NÃO no deck inicial (injetáveis via injectCardId)
        /// </summary>
        public void Build(
            IEnumerable<CardDefinition> tutorialCards,
            IEnumerable<CardDefinition> startingCards,
            IEnumerable<CardDefinition> catalogCards,
            IEnumerable<PerkDefinition> perksList,
            IEnumerable<RunEventDefinition> eventsList,
            IEnumerable<QuestDefinition> questsList,
            IEnumerable<EndingDefinition> endingsList,
            IEnumerable<FlipPhoneActionDefinition> startingActionsList,
            bool playTutorial = true)
        {
            cards.Clear();
            perks.Clear();
            events.Clear();
            quests.Clear();
            endings.Clear();
            actions.Clear();
            tutorialCardIds.Clear();
            mainDeckCardIds.Clear();

            // 1. Cartas de tutorial (entram no deck com prioridade)
            if (playTutorial && tutorialCards != null)
            {
                foreach (var card in tutorialCards)
                {
                    if (card == null || string.IsNullOrEmpty(card.id)) continue;
                    card.isTutorial = true;
                    cards[card.id] = card;
                    if (!tutorialCardIds.Contains(card.id))
                        tutorialCardIds.Add(card.id);
                }
            }

            // 2. Cartas do baralho principal (entram no deck inicial)
            if (startingCards != null)
            {
                foreach (var card in startingCards)
                {
                    if (card == null || string.IsNullOrEmpty(card.id)) continue;
                    cards[card.id] = card;
                    if (!mainDeckCardIds.Contains(card.id))
                        mainDeckCardIds.Add(card.id);
                }
            }

            // 3. Cartas de catálogo/injetáveis (NonStarting): registradas mas NÃO no deck inicial
            if (catalogCards != null)
            {
                foreach (var card in catalogCards)
                {
                    if (card == null || string.IsNullOrEmpty(card.id)) continue;
                    cards[card.id] = card;
                }
            }

            // 4. Perks
            if (perksList != null)
            {
                foreach (var p in perksList)
                {
                    if (p != null && !string.IsNullOrEmpty(p.id)) perks[p.id] = p;
                }
            }

            // 5. Eventos
            if (eventsList != null)
            {
                foreach (var ev in eventsList)
                {
                    if (ev != null && !string.IsNullOrEmpty(ev.id)) events[ev.id] = ev;
                }
            }

            // 6. Quests
            if (questsList != null)
            {
                foreach (var q in questsList)
                {
                    if (q != null && !string.IsNullOrEmpty(q.id)) quests[q.id] = q;
                }
            }

            // 7. Finais
            if (endingsList != null)
            {
                foreach (var end in endingsList)
                {
                    if (end != null && !string.IsNullOrEmpty(end.id)) endings[end.id] = end;
                }
            }

            // 8. Ações do Flip-Phone
            if (startingActionsList != null)
            {
                foreach (var act in startingActionsList)
                {
                    if (act != null && !string.IsNullOrEmpty(act.id)) actions[act.id] = act;
                }
            }
        }

        /// <summary>
        /// Registra um CardDefinition avulso no catálogo.
        /// Uso por ferramentas de editor ou testes. Não afeta o deck inicial.
        /// </summary>
        public void RegisterCard(CardDefinition card, bool isTutorial = false)
        {
            if (card == null || string.IsNullOrEmpty(card.id)) return;
            if (isTutorial) card.isTutorial = true;
            cards[card.id] = card;
        }

        /// <summary>
        /// Registra um ScriptableObject legado via LegacyDealAdapter.
        /// Uso restrito a ferramentas de editor e testes de compatibilidade.
        /// NÃO deve ser chamado no fluxo V2 de produção.
        /// </summary>
        public CardDefinition RegisterLegacyAsset(ScriptableObject asset, bool isTutorial = false)
        {
            if (asset == null) return null;

            CardDefinition card = asset is CardDefinition cd ? cd : LegacyDealAdapter.ConvertToCardDefinition(asset);

            if (card != null && !string.IsNullOrEmpty(card.id))
            {
                if (isTutorial) card.isTutorial = true;
                cards[card.id] = card;
            }

            return card;
        }
    }
}
