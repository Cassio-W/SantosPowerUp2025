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

        public void Build(
            IEnumerable<ScriptableObject> tutorialDealsOrCards,
            IEnumerable<ScriptableObject> startingDealsOrCards,
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

            // 1. Processa cartas de tutorial
            if (playTutorial && tutorialDealsOrCards != null)
            {
                foreach (var asset in tutorialDealsOrCards)
                {
                    if (asset == null) continue;
                    var card = RegisterCard(asset, isTutorial: true);
                    if (card != null && !tutorialCardIds.Contains(card.id))
                    {
                        tutorialCardIds.Add(card.id);
                    }
                }
            }

            // 2. Processa cartas do baralho principal
            if (startingDealsOrCards != null)
            {
                foreach (var asset in startingDealsOrCards)
                {
                    if (asset == null) continue;
                    var card = RegisterCard(asset, isTutorial: false);
                    if (card != null && !mainDeckCardIds.Contains(card.id))
                    {
                        mainDeckCardIds.Add(card.id);
                    }
                }
            }

            // 3. Processa Catálogos de Modificadores e Narrativa
            if (perksList != null)
            {
                foreach (var p in perksList)
                {
                    if (p != null && !string.IsNullOrEmpty(p.id)) perks[p.id] = p;
                }
            }

            if (perks.Count == 0)
            {
                CreateDefaultPerks();
            }

            if (eventsList != null)
            {
                foreach (var ev in eventsList)
                {
                    if (ev != null && !string.IsNullOrEmpty(ev.id)) events[ev.id] = ev;
                }
            }

            if (questsList != null)
            {
                foreach (var q in questsList)
                {
                    if (q != null && !string.IsNullOrEmpty(q.id)) quests[q.id] = q;
                }
            }

            if (endingsList != null)
            {
                foreach (var end in endingsList)
                {
                    if (end != null && !string.IsNullOrEmpty(end.id)) endings[end.id] = end;
                }
            }

            // 4. Processa Ações do Flip-Phone
            if (startingActionsList != null)
            {
                foreach (var act in startingActionsList)
                {
                    if (act != null && !string.IsNullOrEmpty(act.id)) actions[act.id] = act;
                }
            }

            if (actions.Count == 0)
            {
                CreateDefaultActions();
            }
        }

        private void CreateDefaultPerks()
        {
            var cripto = PerkDefinition.CreateRuntimeInstance(
                "Cripto",
                "Hub de Criptoativos",
                "Incentivos à economia digital e blockchain aumentam a inovação econômica.",
                new StatBlock(0, 0, 1, 0, 1)
            );
            perks[cripto.id] = cripto;

            var alianca = PerkDefinition.CreateRescuePerk(
                "AliancaEUA",
                "Aliança Estratégica com os EUA",
                "Acordo diplomático bilateral. Se as Relações Internacionais chegarem a 0, restaura para 30 e consome o acordo.",
                StatId.InternationalRelations,
                30
            );
            perks[alianca.id] = alianca;

            var usina = PerkDefinition.CreateRuntimeInstance(
                "InvestimentoUsina",
                "Subsídio Energético Nacional",
                "Investimento massivo no setor energético impulsiona a economia.",
                new StatBlock(-1, 2, 0, 0, 0)
            );
            perks[usina.id] = usina;

            var reserva = PerkDefinition.CreateRescuePerk(
                "ReservaFlorestal",
                "Reserva Florestal Protegida",
                "Garante a preservação de biomas estratégicos. Se o Meio Ambiente chegar a 0, restaura para 35 e consome a reserva.",
                StatId.ClimaticChanges,
                35
            );
            perks[reserva.id] = reserva;

            var tratado = PerkDefinition.CreateRuntimeInstance(
                "TratadoInternacional",
                "Pacto de Cooperação Global",
                "Tratado multilateral que eleva o prestígio internacional do país.",
                new StatBlock(0, 0, 2, 0, 0)
            );
            perks[tratado.id] = tratado;
        }

        public CardDefinition RegisterCard(ScriptableObject asset, bool isTutorial = false)
        {
            if (asset == null) return null;

            CardDefinition card = null;
            if (asset is CardDefinition cardDef)
            {
                card = cardDef;
            }
            else
            {
                card = LegacyDealAdapter.ConvertToCardDefinition(asset);
            }

            if (card != null && !string.IsNullOrEmpty(card.id))
            {
                if (isTutorial) card.isTutorial = true;
                cards[card.id] = card;
            }

            return card;
        }

        private void CreateDefaultActions()
        {
            var callAdvisor = FlipPhoneActionDefinition.CreateRuntimeInstance(
                "action_ligar_conselheiro",
                "Ligar para o Conselheiro",
                "Consulta a base governista para alinhar o discurso e tranquilizar a opinião pública.",
                FlipPhoneCooldownType.Turns,
                cooldownTurns: 2
            );
            callAdvisor.categoryTag = "Contatos";
            callAdvisor.effects.Add(FlipPhoneEffect.CreateStatImpact(new StatBlock(0, 0, 5, 5, 0)));
            actions[callAdvisor.id] = callAdvisor;

            var emergencyStimulus = FlipPhoneActionDefinition.CreateRuntimeInstance(
                "action_pacote_emergencial",
                "Decreto de Estímulo Financeiro",
                "Injeta capital em setores estratégicos ao custo de concessões duvidosas.",
                FlipPhoneCooldownType.Turns,
                cooldownTurns: 3
            );
            emergencyStimulus.categoryTag = "Gabinete";
            emergencyStimulus.effects.Add(FlipPhoneEffect.CreateStatImpact(new StatBlock(0, 15, 0, -5, 10)));
            actions[emergencyStimulus.id] = emergencyStimulus;

            var dismissProposal = FlipPhoneActionDefinition.CreateRuntimeInstance(
                "action_engavetar_proposta",
                "Engavetar Documento",
                "Recusa o trâmite do documento atual sem se comprometer publicamente.",
                FlipPhoneCooldownType.Turns,
                cooldownTurns: 2
            );
            dismissProposal.categoryTag = "Ações";
            dismissProposal.effects.Add(FlipPhoneEffect.CreateDismissProposal());
            dismissProposal.effects.Add(FlipPhoneEffect.CreateStatImpact(new StatBlock(0, 0, 0, 0, 5)));
            actions[dismissProposal.id] = dismissProposal;
        }
    }
}
