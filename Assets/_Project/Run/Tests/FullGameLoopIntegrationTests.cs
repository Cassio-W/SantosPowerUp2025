using System.Collections.Generic;
using NUnit.Framework;
using Mandato.Content;
using Mandato.Core;
using Mandato.Run;

namespace Mandato.Run.Tests
{
    public class FullGameLoopIntegrationTests
    {
        [Test]
        public void Full48MonthGameLoop_RunsToCompletion_AndEvaluatesEnding()
        {
            // 1. Cria catálogo com propostas de teste
            var catalog = new Dictionary<string, CardDefinition>();
            var initialDeckIds = new List<string>();

            for (int i = 1; i <= 5; i++)
            {
                string id = $"card_prop_{i}";
                var card = CardDefinition.CreateRuntimeInstance(
                    id: id,
                    title: $"Proposta {i}",
                    description: $"Descrição da proposta {i}.",
                    left: new ChoiceDefinition("Aprovar", new StatBlock(3, -1, 2, -2, 1)),
                    right: new ChoiceDefinition("Vetar", new StatBlock(-2, 1, -1, 3, 0))
                );
                catalog[id] = card;
                initialDeckIds.Add(id);
            }

            // 2. Inicializa máquina de estados
            var stateMachine = new RunStateMachine(seed: 42);
            stateMachine.StartRun(initialDeckIds, seed: 42);

            var profile = new ProfileState();
            int turnsExecuted = 0;

            // 3. Simula 48 turnos completos
            while (stateMachine.CurrentPhase != RunPhase.Terminated && turnsExecuted < 100)
            {
                bool drawn = stateMachine.DrawAndPresentProposal(catalog);
                if (!drawn) break;

                // Alterna entre escolha 0 e 1 de forma equilibrada
                int choice = turnsExecuted % 2;
                var report = stateMachine.SubmitChoice(choice);
                Assert.IsNotNull(report);

                stateMachine.CompleteTurnAndAdvance();
                turnsExecuted++;
            }

            // Garante que a simulação alcançou vitória dos 48 meses
            Assert.IsTrue(stateMachine.RunState.termination.IsVictory, "A run deveria ter sido concluída com vitória após 48 meses.");
            Assert.AreEqual(RunStatus.Victory, stateMachine.RunState.termination.status);

            // 4. Salva no perfil e valida integridade da persistência
            profile.RecordRunCompleted(stateMachine.RunState.termination.IsVictory, "ending_pacifista");
            Assert.AreEqual(1, profile.totalRunsPlayed);
            Assert.AreEqual(1, profile.totalVictories);
            Assert.IsTrue(profile.discoveredEndingIds.Contains("ending_pacifista"));
        }

        [Test]
        public void TutorialDeck_PrecedesMainDeck_WithoutMonthAdvancement()
        {
            var catalog = new Dictionary<string, CardDefinition>();
            var tutorialIds = new List<string> { "tut_1", "tut_2" };
            var mainDeckIds = new List<string> { "main_card_1", "main_card_2" };

            // Cria cartas de tutorial
            catalog["tut_1"] = CardDefinition.CreateRuntimeInstance("tut_1", "Tutorial 1", "Bem-vindo!", new ChoiceDefinition("Continuar"), new ChoiceDefinition("Continuar"), isTutorial: true);
            catalog["tut_2"] = CardDefinition.CreateRuntimeInstance("tut_2", "Tutorial 2", "Entendido!", new ChoiceDefinition("Continuar"), new ChoiceDefinition("Continuar"), isTutorial: true);

            // Cria cartas do deck principal
            catalog["main_card_1"] = CardDefinition.CreateRuntimeInstance("main_card_1", "Decreto 1", "Economia", new ChoiceDefinition("Aprovar"), new ChoiceDefinition("Vetar"));
            catalog["main_card_2"] = CardDefinition.CreateRuntimeInstance("main_card_2", "Decreto 2", "Saúde", new ChoiceDefinition("Aprovar"), new ChoiceDefinition("Vetar"));

            var stateMachine = new RunStateMachine(seed: 123);
            stateMachine.StartRun(mainDeckIds, seed: 123, priorityCardIds: tutorialIds);

            // 1. Primeira carta puxada DEVE ser o Tutorial 1
            bool drawn1 = stateMachine.DrawAndPresentProposal(catalog);
            Assert.IsTrue(drawn1);
            Assert.AreEqual("tut_1", stateMachine.CurrentCard.id);
            Assert.AreEqual(1, stateMachine.RunState.calendar.currentMonthIndex);
            stateMachine.SubmitChoice(1);
            stateMachine.CompleteTurnAndAdvance();
            Assert.AreEqual(1, stateMachine.RunState.calendar.currentMonthIndex);

            // 2. Segunda carta puxada DEVE ser o Tutorial 2
            bool drawn2 = stateMachine.DrawAndPresentProposal(catalog);
            Assert.IsTrue(drawn2);
            Assert.AreEqual("tut_2", stateMachine.CurrentCard.id);
            Assert.AreEqual(1, stateMachine.RunState.calendar.currentMonthIndex);
            stateMachine.SubmitChoice(1);
            stateMachine.CompleteTurnAndAdvance();
            Assert.AreEqual(1, stateMachine.RunState.calendar.currentMonthIndex);

            // 3. Terceira carta puxada DEVE ser uma do baralho principal
            bool drawn3 = stateMachine.DrawAndPresentProposal(catalog);
            Assert.IsTrue(drawn3);
            Assert.IsTrue(stateMachine.CurrentCard.id.StartsWith("main_card_"));
            stateMachine.SubmitChoice(1);
            stateMachine.CompleteTurnAndAdvance();
            Assert.AreEqual(2, stateMachine.RunState.calendar.currentMonthIndex);
        }

        [Test]
        public void EconomyReachingZero_TriggersDefeatTermination_AndTerminatesRun()
        {
            var catalog = new Dictionary<string, CardDefinition>();
            var initialDeckIds = new List<string> { "crisis_card" };

            catalog["crisis_card"] = CardDefinition.CreateRuntimeInstance(
                "crisis_card",
                "Crise Severa",
                "A economia vai colapsar!",
                new ChoiceDefinition("Gastar Tudo", new StatBlock(0, 0, 0, -60, 0)),
                new ChoiceDefinition("Recusar", new StatBlock(0, 0, 0, 0, 0))
            );

            var stateMachine = new RunStateMachine(seed: 123);
            stateMachine.StartRun(initialDeckIds, seed: 123);

            bool terminatedEventFired = false;
            RunTermination receivedTermination = default;
            stateMachine.OnRunTerminated += term =>
            {
                terminatedEventFired = true;
                receivedTermination = term;
            };

            bool drawn = stateMachine.DrawAndPresentProposal(catalog);
            Assert.IsTrue(drawn);

            // Submete escolha que drena 60 de economia (economia cai de 50 para 0)
            var report = stateMachine.SubmitChoice(0);

            Assert.IsNotNull(report);
            Assert.IsTrue(report.IsRunTerminated);
            Assert.IsTrue(report.resultingTermination.IsDefeat);
            Assert.IsTrue(stateMachine.RunState.termination.IsDefeat);
            Assert.AreEqual(RunPhase.Terminated, stateMachine.CurrentPhase);
            Assert.IsTrue(terminatedEventFired);
            Assert.IsTrue(receivedTermination.IsDefeat);
            Assert.IsTrue(receivedTermination.reason.Contains("Colapso Econômico") || receivedTermination.reason.Contains("falência"));
        }
    }
}
