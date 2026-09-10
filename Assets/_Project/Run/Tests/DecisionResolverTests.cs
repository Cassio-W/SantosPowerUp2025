using System.Collections.Generic;
using NUnit.Framework;
using Mandato.Content;
using Mandato.Core;
using Mandato.Run;

namespace Mandato.Run.Tests
{
    public class DecisionResolverTests
    {
        private CardDefinition testCard;

        [SetUp]
        public void Setup()
        {
            var left = new ChoiceDefinition("Vetar", new StatBlock(0, 0, -5, 10, 0))
            {
                deltaPoliticalX = 2,
                deltaPoliticalY = -1
            };

            var right = new ChoiceDefinition("Aprovar", new StatBlock(10, 5, 10, -15, 10), corruptionMods: true)
            {
                deltaPoliticalX = -3,
                deltaPoliticalY = 2,
                injectCardIds = new List<string> { "card_consequence_1" },
                grantPerkId = "perk_reformista",
                presentationCue = "palmas"
            };

            testCard = CardDefinition.CreateRuntimeInstance(
                id: "card_reforma",
                title: "Reforma Estrutural",
                description: "Proposta de reforma.",
                left: left,
                right: right,
                npcId: "Ministro",
                tag: "Economia"
            );
        }

        [Test]
        public void Resolve_LeftChoice_AppliesStatsAndPoliticalMovement()
        {
            var run = new RunState();
            var deck = new DeckState();

            ResolutionReport report = DecisionResolver.Resolve(run, deck, testCard, choiceIndex: 0);

            Assert.IsNotNull(report);
            Assert.AreEqual("Vetar", report.choiceLabel);
            Assert.AreEqual(50, report.statsBefore.economy);
            Assert.AreEqual(60, report.statsAfter.economy);
            Assert.AreEqual(45, report.statsAfter.popularApproval);

            Assert.AreEqual(2, run.politicalAxis.x);
            Assert.AreEqual(-1, run.politicalAxis.y);
            Assert.AreEqual(1, run.decisionHistory.Count);
        }

        [Test]
        public void Resolve_RightChoice_InjectsCardsAndGrantsPerks()
        {
            var run = new RunState();
            var deck = new DeckState();

            ResolutionReport report = DecisionResolver.Resolve(run, deck, testCard, choiceIndex: 1);

            Assert.IsNotNull(report);
            Assert.AreEqual("Aprovar", report.choiceLabel);
            Assert.AreEqual("palmas", report.presentationCue);

            // Injeção de carta no deck
            Assert.IsTrue(deck.drawPile.Contains("card_consequence_1"));
            Assert.Contains("card_consequence_1", report.injectedCardIds);

            // Concessão de perk
            Assert.IsTrue(run.activePerkIds.Contains("perk_reformista"));

            // Eixo político
            Assert.AreEqual(-3, run.politicalAxis.x);
            Assert.AreEqual(2, run.politicalAxis.y);
        }

        [Test]
        public void RunStateMachine_ExecutesTurnCycleDeterministically()
        {
            var catalog = new Dictionary<string, CardDefinition>
            {
                [testCard.id] = testCard
            };

            var stateMachine = new RunStateMachine(seed: 999);
            stateMachine.StartRun(new[] { testCard.id });

            bool cardPresented = false;
            ResolutionReport reportReceived = null;

            stateMachine.OnProposalReady += card => cardPresented = true;
            stateMachine.OnConsequencesReady += rep => reportReceived = rep;

            // 1. Puxa proposta
            bool drawn = stateMachine.DrawAndPresentProposal(catalog);
            Assert.IsTrue(drawn);
            Assert.IsTrue(cardPresented);
            Assert.AreEqual(RunPhase.AwaitingChoice, stateMachine.CurrentPhase);

            // 2. Jogador escolhe Opção 0
            ResolutionReport rep = stateMachine.SubmitChoice(0);
            Assert.IsNotNull(rep);
            Assert.AreEqual(reportReceived, rep);
            Assert.AreEqual(RunPhase.PresentingConsequences, stateMachine.CurrentPhase);

            // 3. Conclui apresentação visual e avança o mês
            stateMachine.CompleteTurnAndAdvance();
            Assert.AreEqual(RunPhase.AdvancingTime, stateMachine.CurrentPhase);
            Assert.AreEqual(2, stateMachine.RunState.calendar.currentMonthIndex);
        }
    }
}
