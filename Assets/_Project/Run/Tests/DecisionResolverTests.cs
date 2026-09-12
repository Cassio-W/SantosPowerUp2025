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
            var left = new ChoiceDefinition("Aprovar", new StatBlock(10, 5, 10, -15, 10), corruptionMods: true)
            {
                deltaPoliticalX = -3,
                deltaPoliticalY = 2,
                injectCardIds = new List<string> { "card_consequence_1" },
                grantPerkId = "perk_reformista",
                presentationCue = "palmas"
            };

            var right = new ChoiceDefinition("Vetar", new StatBlock(0, 0, -5, 10, 0))
            {
                deltaPoliticalX = 2,
                deltaPoliticalY = -1
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
            Assert.AreEqual("Aprovar", report.choiceLabel);
            Assert.AreEqual(50, report.statsBefore.economy);
            Assert.AreEqual(35, report.statsAfter.economy);
            Assert.AreEqual(62, report.statsAfter.popularApproval);

            Assert.AreEqual(-3, run.politicalAxis.x);
            Assert.AreEqual(2, run.politicalAxis.y);
            Assert.AreEqual(1, run.decisionHistory.Count);

            // Injeção de carta e concessão de perk
            Assert.IsTrue(deck.priorityDrawPile.Contains("card_consequence_1") || deck.drawPile.Contains("card_consequence_1"));
            Assert.Contains("card_consequence_1", report.injectedCardIds);
            Assert.IsTrue(run.activePerkIds.Contains("perk_reformista"));
        }

        [Test]
        public void Resolve_RightChoice_InjectsCardsAndGrantsPerks()
        {
            var run = new RunState();
            var deck = new DeckState();

            ResolutionReport report = DecisionResolver.Resolve(run, deck, testCard, choiceIndex: 1);

            Assert.IsNotNull(report);
            Assert.AreEqual("Vetar", report.choiceLabel);

            // Eixo político
            Assert.AreEqual(2, run.politicalAxis.x);
            Assert.AreEqual(-1, run.politicalAxis.y);
            Assert.AreEqual(60, report.statsAfter.economy);
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

            var phases = new List<RunPhase>();
            stateMachine.OnPhaseChanged += p => phases.Add(p);
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
            Assert.IsTrue(phases.Contains(RunPhase.AdvancingTime));
            Assert.AreEqual(RunPhase.PreparingRun, stateMachine.CurrentPhase);
            Assert.AreEqual(2, stateMachine.RunState.calendar.currentMonthIndex);
        }

        [Test]
        public void Resolve_InvalidChoiceIndex_ReturnsNull()
        {
            var run = new RunState();
            var deck = new DeckState();

            Assert.IsNull(DecisionResolver.Resolve(run, deck, testCard, choiceIndex: -1));
            Assert.IsNull(DecisionResolver.Resolve(run, deck, testCard, choiceIndex: 2));
            Assert.IsNull(DecisionResolver.Resolve(run, deck, testCard, choiceIndex: 99));
        }

        [Test]
        public void Resolve_WhenClimateDropsToZero_WithReservaFlorestal_RestoresTo35AndConsumesPerk()
        {
            var run = new RunState();
            var deck = new DeckState();

            // Configura clima inicial em 20
            run.stats.climaticChanges = 20;

            // Concede ReservaFlorestal
            var perkReserva = PerkDefinition.CreateRescuePerk(
                "ReservaFlorestal",
                "Reserva Florestal",
                "Protege contra colapso climático",
                StatId.ClimaticChanges,
                35
            );
            var perkCatalog = new Dictionary<string, PerkDefinition> { { perkReserva.id, perkReserva } };
            run.GrantPerk("ReservaFlorestal");

            // Carta que reduz clima em -50 (levando de 20 para 0 ou menos)
            var fatalCard = CardDefinition.CreateRuntimeInstance(
                "card_fatal_climate",
                "Desastre Iminente",
                "Impacto severo no clima",
                left: new ChoiceDefinition("Desmatar", new StatBlock(-50, 0, 0, 0, 0)),
                right: new ChoiceDefinition("Proteger", new StatBlock(0, 0, 0, 0, 0))
            );

            var report = DecisionResolver.Resolve(run, deck, fatalCard, choiceIndex: 0, perkCatalog: perkCatalog);

            Assert.IsNotNull(report);
            Assert.IsTrue(report.rescuedByPerkIds.Contains("ReservaFlorestal"));
            Assert.AreEqual(35, run.stats.climaticChanges);
            Assert.AreEqual(35, report.statsAfter.climaticChanges);
            Assert.IsFalse(run.activePerkIds.Contains("ReservaFlorestal"), "Perk de uso único de resgate deve ser consumido.");
            Assert.IsTrue(run.termination.IsOngoing, "A partida não deve encerrar em derrota quando resgatada pelo perk.");
        }

        [Test]
        public void Resolve_WithMultiplePerks_WhereReservaFlorestalIsFirst_SuccessfullyRestoresClimateAndPreservesSecondPerk()
        {
            var run = new RunState();
            var deck = new DeckState();

            run.stats.climaticChanges = 15;

            var perkReserva = PerkDefinition.CreateRescuePerk("ReservaFlorestal", "Reserva Florestal", "", StatId.ClimaticChanges, 35);
            var perkCripto = PerkDefinition.CreateRuntimeInstance("Cripto", "Cripto", "", new StatBlock(0, 0, 1, 0, 1));

            var perkCatalog = new Dictionary<string, PerkDefinition>
            {
                { perkReserva.id, perkReserva },
                { perkCripto.id, perkCripto }
            };

            // Jogador adquire primeiro a ReservaFlorestal, e depois o Cripto
            run.GrantPerk("ReservaFlorestal");
            run.GrantPerk("Cripto");

            Assert.AreEqual(2, run.activePerkIds.Count);
            Assert.AreEqual("ReservaFlorestal", run.activePerkIds[0]);
            Assert.AreEqual("Cripto", run.activePerkIds[1]);

            var fatalCard = CardDefinition.CreateRuntimeInstance(
                "card_fatal_climate_2",
                "Queimada",
                "Impacto",
                left: new ChoiceDefinition("Queimar", new StatBlock(-30, 0, 0, 0, 0)),
                right: new ChoiceDefinition("Apagar", new StatBlock(0, 0, 0, 0, 0))
            );

            var report = DecisionResolver.Resolve(run, deck, fatalCard, choiceIndex: 0, perkCatalog: perkCatalog);

            Assert.IsNotNull(report);
            Assert.IsTrue(report.rescuedByPerkIds.Contains("ReservaFlorestal"));
            Assert.AreEqual(35, run.stats.climaticChanges);
            Assert.IsFalse(run.activePerkIds.Contains("ReservaFlorestal"), "ReservaFlorestal deve ser consumida.");
            Assert.IsTrue(run.activePerkIds.Contains("Cripto"), "O segundo perk (Cripto) deve permanecer ativo.");
            Assert.IsTrue(run.termination.IsOngoing);
        }

        [Test]
        public void Resolve_WhenRelationsDropToZero_WithAliancaEUA_RestoresTo30AndConsumesPerk()
        {
            var run = new RunState();
            var deck = new DeckState();

            run.stats.internationalRelations = 10;

            var perkAlianca = PerkDefinition.CreateRescuePerk("AliancaEUA", "Aliança EUA", "", StatId.InternationalRelations, 30);
            var perkCatalog = new Dictionary<string, PerkDefinition> { { perkAlianca.id, perkAlianca } };

            run.GrantPerk("AliancaEUA");

            var fatalCard = CardDefinition.CreateRuntimeInstance(
                "card_fatal_rel",
                "Crise Diplomática",
                "Impacto",
                left: new ChoiceDefinition("Hostilizar", new StatBlock(0, -40, 0, 0, 0)),
                right: new ChoiceDefinition("Dialogar", new StatBlock(0, 0, 0, 0, 0))
            );

            var report = DecisionResolver.Resolve(run, deck, fatalCard, choiceIndex: 0, perkCatalog: perkCatalog);

            Assert.IsNotNull(report);
            Assert.IsTrue(report.rescuedByPerkIds.Contains("AliancaEUA"));
            Assert.AreEqual(30, run.stats.internationalRelations);
            Assert.IsFalse(run.activePerkIds.Contains("AliancaEUA"));
            Assert.IsTrue(run.termination.IsOngoing);
        }
    }
}
