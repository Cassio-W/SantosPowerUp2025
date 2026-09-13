using System.Collections.Generic;
using NUnit.Framework;
using Mandato.Content;
using Mandato.Core;
using Mandato.Run;

namespace Mandato.Run.Tests
{
    public class Phase2IntegrationTests
    {
        [Test]
        public void SubmitChoice_WithInvalidIndex_PreservesAwaitingChoiceAndEmitsNoConsequences()
        {
            var card = CardDefinition.CreateRuntimeInstance(
                "card_test_invalid",
                "Decisão de Teste",
                "Descrição",
                new ChoiceDefinition("Opção A"),
                new ChoiceDefinition("Opção B")
            );

            var catalog = new Dictionary<string, CardDefinition> { { card.id, card } };
            var stateMachine = new RunStateMachine();
            stateMachine.StartRun(new[] { card.id });

            bool drawn = stateMachine.DrawAndPresentProposal(catalog);
            Assert.IsTrue(drawn);
            Assert.AreEqual(RunPhase.AwaitingChoice, stateMachine.CurrentPhase);

            bool consequencesFired = false;
            stateMachine.OnConsequencesReady += _ => consequencesFired = true;

            // Tentativa com índice negativo (-1)
            var reportNegative = stateMachine.SubmitChoice(-1);
            Assert.IsNull(reportNegative);
            Assert.AreEqual(RunPhase.AwaitingChoice, stateMachine.CurrentPhase, "Fase deve permanecer AwaitingChoice após índice inválido (-1).");
            Assert.IsFalse(consequencesFired, "Nenhuma consequência deve ser emitida para índice inválido.");

            // Tentativa com índice fora do intervalo (2)
            var reportTooHigh = stateMachine.SubmitChoice(2);
            Assert.IsNull(reportTooHigh);
            Assert.AreEqual(RunPhase.AwaitingChoice, stateMachine.CurrentPhase, "Fase deve permanecer AwaitingChoice após índice inválido (2).");
            Assert.IsFalse(consequencesFired, "Nenhuma consequência deve ser emitida para índice inválido.");

            // Escolha válida (0) submetida com sucesso
            var validReport = stateMachine.SubmitChoice(0);
            Assert.IsNotNull(validReport);
            Assert.AreEqual(RunPhase.PresentingConsequences, stateMachine.CurrentPhase);
            Assert.IsTrue(consequencesFired);
        }

        [Test]
        public void DrawAndPresentProposal_WhenNoCardsEligible_ProvidesRoutineDispatchCard()
        {
            var stateMachine = new RunStateMachine();
            // Inicia com baralho vazio
            stateMachine.StartRun(new string[0]);

            var emptyCatalog = new Dictionary<string, CardDefinition>();
            bool drawn = stateMachine.DrawAndPresentProposal(emptyCatalog);

            Assert.IsTrue(drawn);
            Assert.IsNotNull(stateMachine.CurrentCard);
            Assert.AreEqual(CardDefinition.NeutralRoutineCardId, stateMachine.CurrentCard.id);
            Assert.AreEqual(RunPhase.AwaitingChoice, stateMachine.CurrentPhase);

            var report = stateMachine.SubmitChoice(0);
            Assert.IsNotNull(report);
            Assert.AreEqual(RunPhase.PresentingConsequences, stateMachine.CurrentPhase);
        }

        [Test]
        public void PerkDuration_RespectedWhenGrantedFromChoice_WithPerkCatalog()
        {
            var runState = new RunState();
            var deckState = new DeckState();

            var tempPerk = PerkDefinition.CreateRuntimeInstance(
                "perk_subsidio_temporario",
                "Subsídio Temporário",
                "Dura 4 meses",
                monthlyDeltas: new StatBlock(0, 0, 0, 5, 0),
                duration: 4
            );

            var perkCatalog = new Dictionary<string, PerkDefinition> { { tempPerk.id, tempPerk } };

            var choice = new ChoiceDefinition("Aprovar Subsídio")
            {
                grantPerkId = tempPerk.id
            };

            var card = CardDefinition.CreateRuntimeInstance("card_subsidio", "Subsídio", "Descrição", choice, new ChoiceDefinition("Recusar"));

            var report = DecisionResolver.Resolve(runState, deckState, card, choiceIndex: 0, perkCatalog: perkCatalog);

            Assert.IsNotNull(report);
            Assert.IsTrue(runState.activePerkIds.Contains(tempPerk.id));

            var activePerkState = runState.activePerks.Find(p => p.perkId == tempPerk.id);
            Assert.IsNotNull(activePerkState);
            Assert.AreEqual(4, activePerkState.remainingMonths, "Perk deve ter durationMonths respeitado (4 meses).");
        }

        [Test]
        public void PerkDuration_RespectedWhenGrantedFromQuestReward_WithPerkCatalog()
        {
            var runState = new RunState();
            var deckState = new DeckState();

            var rewardPerk = PerkDefinition.CreateRuntimeInstance(
                "perk_recompensa_quest",
                "Prestígio Diplomático",
                "Dura 6 meses",
                monthlyDeltas: new StatBlock(0, 5, 0, 0, 0),
                duration: 6
            );

            var perkCatalog = new Dictionary<string, PerkDefinition> { { rewardPerk.id, rewardPerk } };

            var quest = QuestDefinition.CreateRuntimeInstance("quest_diplomacia", "Embaixador", "Missão Diplomática");
            quest.rewardPerkId = rewardPerk.id;
            quest.steps = new List<QuestStepDefinition>
            {
                new QuestStepDefinition { stepIndex = 0, triggerCardId = "card_step_1", requiredChoiceIndex = 0 }
            };

            var questCatalog = new Dictionary<string, QuestDefinition> { { quest.id, quest } };

            var card = CardDefinition.CreateRuntimeInstance("card_step_1", "Etapa 1", "Texto", new ChoiceDefinition("Aceitar"), new ChoiceDefinition("Recusar"));

            var report = DecisionResolver.Resolve(runState, deckState, card, choiceIndex: 0, questCatalog: questCatalog, perkCatalog: perkCatalog);

            Assert.IsNotNull(report);
            Assert.IsTrue(report.completedQuestIds.Contains(quest.id));
            Assert.IsTrue(report.grantedRewardPerkIds.Contains(rewardPerk.id));

            var activePerkState = runState.activePerks.Find(p => p.perkId == rewardPerk.id);
            Assert.IsNotNull(activePerkState);
            Assert.AreEqual(6, activePerkState.remainingMonths, "Recompensa de quest deve ter durationMonths respeitado (6 meses).");
        }

        [Test]
        public void FlipPhone_GrantPerk_RespectsCatalogDurationWhenDurationNotExplicit()
        {
            var runState = new RunState();
            var deckState = new DeckState();

            var perk = PerkDefinition.CreateRuntimeInstance("perk_phone_bonus", "Bônus", "", new StatBlock(0, 0, 2, 0, 0), duration: 3);
            var perkCatalog = new Dictionary<string, PerkDefinition> { { perk.id, perk } };

            var action = FlipPhoneActionDefinition.CreateRuntimeInstance("action_call", "Telefonema", "");
            action.effects = new List<FlipPhoneEffect>
            {
                FlipPhoneEffect.CreateGrantPerk(perk.id, duration: 0) // duration 0 -> busca do catálogo
            };

            var report = FlipPhoneResolver.ResolveUse(runState, deckState, action, perkCatalog: perkCatalog);

            Assert.IsTrue(report.success);
            Assert.IsTrue(runState.activePerkIds.Contains(perk.id));
            var activePerkState = runState.activePerks.Find(p => p.perkId == perk.id);
            Assert.IsNotNull(activePerkState);
            Assert.AreEqual(3, activePerkState.remainingMonths, "Ação do FlipPhone deve buscar duração de 3 meses do catálogo.");
        }

        [Test]
        public void MainCard_InjectsNonStartingCard_AndNonStartingCardBecomesPlayableNext()
        {
            var mainCard = CardDefinition.CreateRuntimeInstance(
                "main_start_01",
                "Investigação Preliminar",
                "Descobrimos algo suspeito.",
                left: new ChoiceDefinition("Investigar a fundo")
                {
                    injectCardIds = new List<string> { "escandaloDasJoias" }
                },
                right: new ChoiceDefinition("Ignorar")
            );

            var nonStartingCard = CardDefinition.CreateRuntimeInstance(
                "escandaloDasJoias",
                "Escândalo das Jóias",
                "O escândalo estourou na mídia!",
                left: new ChoiceDefinition("Renunciar ao cargo", new StatBlock(0, 0, -20, 0, 15)),
                right: new ChoiceDefinition("Negar veementemente", new StatBlock(0, 0, -10, 0, 5))
            );

            var catalog = new Dictionary<string, CardDefinition>
            {
                { mainCard.id, mainCard },
                { nonStartingCard.id, nonStartingCard }
            };

            var stateMachine = new RunStateMachine(seed: 42);
            stateMachine.StartRun(new[] { mainCard.id });

            // 1. Puxa a carta inicial Main
            bool drawn1 = stateMachine.DrawAndPresentProposal(catalog);
            Assert.IsTrue(drawn1);
            Assert.AreEqual(mainCard.id, stateMachine.CurrentCard.id);

            // 2. Escolhe a opção que injeta a NonStarting no topo
            var report1 = stateMachine.SubmitChoice(0);
            Assert.IsNotNull(report1);
            Assert.IsTrue(report1.injectedCardIds.Contains(nonStartingCard.id));
            stateMachine.CompleteTurnAndAdvance();

            // 3. Próxima carta DEVE ser a carta injetada NonStarting
            bool drawn2 = stateMachine.DrawAndPresentProposal(catalog);
            Assert.IsTrue(drawn2);
            Assert.AreEqual(nonStartingCard.id, stateMachine.CurrentCard.id, "A carta injetada NonStarting deve ser puxada no turno seguinte.");
        }

        [Test]
        public void EndingEvaluator_SelectsSpecificEnding_WhenQuadrantAndCompletedQuestMatch()
        {
            var runState = new RunState();
            runState.politicalAxis.x = 6;
            runState.politicalAxis.y = -5; // Direita Liberal
            runState.stats.economy = 70;
            runState.stats.climaticChanges = 60;
            runState.termination = RunTermination.CreateVictory("Mandato de 4 anos completo.");

            var quest = runState.GetOrCreateQuestState("quest_ferrovia");
            quest.isCompleted = true;

            var genericEnding = EndingDefinition.CreateRuntimeInstance("ending_generic", "Fim Comum", "Fim comum.", victory: true, priority: 0);

            var specificEnding = EndingDefinition.CreateRuntimeInstance("ending_liberal_rail", "Crescimento Ferroviário", "Epílogo específico.", victory: true, quadrant: "Direita Liberal", priority: 10);
            specificEnding.requiredCompletedQuestId = "quest_ferrovia";
            specificEnding.minEconomy = 50;

            var endingsCatalog = new List<EndingDefinition> { genericEnding, specificEnding };

            var evaluated = EndingEvaluator.EvaluateEnding(runState, endingsCatalog);

            Assert.IsNotNull(evaluated);
            Assert.AreEqual("ending_liberal_rail", evaluated.id);
            Assert.AreEqual("Crescimento Ferroviário", evaluated.title);
        }
    }
}
