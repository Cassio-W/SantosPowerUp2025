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

        [Test]
        public void Resolve_AcceptProposal_IncreasesNpcRelationByFixedDefault()
        {
            var run = new RunState();
            var deck = new DeckState();

            ResolutionReport report = DecisionResolver.Resolve(run, deck, testCard, choiceIndex: 0);

            Assert.IsNotNull(report);
            Assert.AreEqual("Ministro", report.npcId);
            Assert.AreEqual(0, report.npcRelationBefore);
            Assert.AreEqual(DecisionResolver.DefaultAcceptNpcRelationDelta, report.npcRelationDelta);
            Assert.AreEqual(5, report.npcRelationAfter);
            Assert.AreEqual(5, run.GetNpcRelation("Ministro"));

            var npcState = run.GetOrCreateNpcState("Ministro");
            Assert.IsTrue(npcState.isMet);
            Assert.AreEqual(1, npcState.interactionCount);
        }

        [Test]
        public void Resolve_RejectProposal_DecreasesNpcRelationByFixedDefault()
        {
            var run = new RunState();
            var deck = new DeckState();

            ResolutionReport report = DecisionResolver.Resolve(run, deck, testCard, choiceIndex: 1);

            Assert.IsNotNull(report);
            Assert.AreEqual("Ministro", report.npcId);
            Assert.AreEqual(0, report.npcRelationBefore);
            Assert.AreEqual(DecisionResolver.DefaultRejectNpcRelationDelta, report.npcRelationDelta);
            Assert.AreEqual(-5, report.npcRelationAfter);
            Assert.AreEqual(-5, run.GetNpcRelation("Ministro"));

            var npcState = run.GetOrCreateNpcState("Ministro");
            Assert.IsTrue(npcState.isMet);
            Assert.AreEqual(1, npcState.interactionCount);
        }

        [Test]
        public void Resolve_SuccessiveDecisions_AccumulatesNpcRelationCorrectly()
        {
            var run = new RunState();
            var deck = new DeckState();

            // Aceita 1ª proposta (+5)
            var rep1 = DecisionResolver.Resolve(run, deck, testCard, choiceIndex: 0);
            Assert.AreEqual(0, rep1.npcRelationBefore);
            Assert.AreEqual(5, rep1.npcRelationAfter);

            // Aceita 2ª proposta (+5 -> 10)
            var rep2 = DecisionResolver.Resolve(run, deck, testCard, choiceIndex: 0);
            Assert.AreEqual(5, rep2.npcRelationBefore);
            Assert.AreEqual(10, rep2.npcRelationAfter);

            // Recusa 3ª proposta (-5 -> 5)
            var rep3 = DecisionResolver.Resolve(run, deck, testCard, choiceIndex: 1);
            Assert.AreEqual(10, rep3.npcRelationBefore);
            Assert.AreEqual(5, rep3.npcRelationAfter);

            // Recusa 4ª proposta (-5 -> 0)
            var rep4 = DecisionResolver.Resolve(run, deck, testCard, choiceIndex: 1);
            Assert.AreEqual(5, rep4.npcRelationBefore);
            Assert.AreEqual(0, rep4.npcRelationAfter);

            // Recusa 5ª proposta (-5 -> -5)
            var rep5 = DecisionResolver.Resolve(run, deck, testCard, choiceIndex: 1);
            Assert.AreEqual(0, rep5.npcRelationBefore);
            Assert.AreEqual(-5, rep5.npcRelationAfter);
            Assert.AreEqual(5, run.GetOrCreateNpcState("Ministro").interactionCount);
        }

        [Test]
        public void Resolve_CustomChoiceDeltaNpcRelation_OverridesDefaultDelta()
        {
            var run = new RunState();
            var deck = new DeckState();

            var customCard = CardDefinition.CreateRuntimeInstance(
                id: "card_custom_relation",
                title: "Tratado Especial",
                description: "Proposta com relação customizada.",
                left: new ChoiceDefinition("Grande Acordo", new StatBlock(), deltaNpcRelation: 15),
                right: new ChoiceDefinition("Grande Ofensa", new StatBlock(), deltaNpcRelation: -20),
                npcId: "npc_diplomata"
            );

            // Aceita com delta customizado +15
            var rep1 = DecisionResolver.Resolve(run, deck, customCard, choiceIndex: 0);
            Assert.AreEqual(15, rep1.npcRelationDelta);
            Assert.AreEqual(15, rep1.npcRelationAfter);
            Assert.AreEqual(15, run.GetNpcRelation("npc_diplomata"));

            // Recusa com delta customizado -20 (15 - 20 = -5)
            var rep2 = DecisionResolver.Resolve(run, deck, customCard, choiceIndex: 1);
            Assert.AreEqual(-20, rep2.npcRelationDelta);
            Assert.AreEqual(-5, rep2.npcRelationAfter);
            Assert.AreEqual(-5, run.GetNpcRelation("npc_diplomata"));
        }

        [Test]
        public void Resolve_NpcRelation_ClampsAtMaxAndMinLimits()
        {
            var run = new RunState();
            var deck = new DeckState();
            run.GetOrCreateNpcState("Ministro").relationScore = 98;

            // +5 em 98 deve travar em 100
            var repMax = DecisionResolver.Resolve(run, deck, testCard, choiceIndex: 0);
            Assert.AreEqual(100, repMax.npcRelationAfter);
            Assert.AreEqual(100, run.GetNpcRelation("Ministro"));

            run.GetOrCreateNpcState("Ministro").relationScore = -98;
            // -5 em -98 deve travar em -100
            var repMin = DecisionResolver.Resolve(run, deck, testCard, choiceIndex: 1);
            Assert.AreEqual(-100, repMin.npcRelationAfter);
            Assert.AreEqual(-100, run.GetNpcRelation("Ministro"));
        }

        [Test]
        public void Resolve_CardWithoutNpc_DoesNotFailAndLeavesNpcFieldsEmpty()
        {
            var run = new RunState();
            var deck = new DeckState();

            var noNpcCard = CardDefinition.CreateNeutralRoutineCard();
            var rep = DecisionResolver.Resolve(run, deck, noNpcCard, choiceIndex: 0);

            Assert.IsNotNull(rep);
            Assert.IsEmpty(rep.npcId);
            Assert.AreEqual(0, rep.npcRelationDelta);
            Assert.AreEqual(0, rep.npcRelationBefore);
            Assert.AreEqual(0, rep.npcRelationAfter);
        }

        [Test]
        public void Resolve_AcceptProposal_MovesPoliticalAxisTowardsNpcBiasByFixedProposalStep()
        {
            var run = new RunState();
            var deck = new DeckState();

            var npc = NpcDefinition.CreateRuntimeInstance("min_agro", "Ministro Agro", biasX: 6, biasY: 2);
            var cardWithNpc = CardDefinition.CreateRuntimeInstance(
                id: "card_agro",
                title: "Subsídio ao Agro",
                description: "Proposta do agro",
                left: new ChoiceDefinition("Aprovar"),
                right: new ChoiceDefinition("Rejeitar")
            );
            cardWithNpc.npc = npc;

            // Run começa em (0, 0)
            Assert.AreEqual(0, run.politicalAxis.x);
            Assert.AreEqual(0, run.politicalAxis.y);

            // Aceita proposta (choiceIndex = 0)
            var rep = DecisionResolver.Resolve(run, deck, cardWithNpc, choiceIndex: 0);

            Assert.IsNotNull(rep);
            Assert.AreEqual(2, rep.npcPoliticalDeltaX);
            Assert.AreEqual(2, rep.npcPoliticalDeltaY);
            Assert.AreEqual(2, rep.deltaPoliticalX);
            Assert.AreEqual(2, rep.deltaPoliticalY);
            Assert.AreEqual(2, run.politicalAxis.x);
            Assert.AreEqual(2, run.politicalAxis.y);
        }

        [Test]
        public void Resolve_RejectProposal_DoesNotMovePoliticalAxisTowardsNpcBias()
        {
            var run = new RunState();
            var deck = new DeckState();

            var npc = NpcDefinition.CreateRuntimeInstance("min_agro", "Ministro Agro", biasX: 6, biasY: 2);
            var cardWithNpc = CardDefinition.CreateRuntimeInstance(
                id: "card_agro",
                title: "Subsídio ao Agro",
                description: "Proposta do agro",
                left: new ChoiceDefinition("Aprovar"),
                right: new ChoiceDefinition("Rejeitar")
            );
            cardWithNpc.npc = npc;

            // Recusa proposta (choiceIndex = 1)
            var rep = DecisionResolver.Resolve(run, deck, cardWithNpc, choiceIndex: 1);

            Assert.IsNotNull(rep);
            Assert.AreEqual(0, rep.npcPoliticalDeltaX);
            Assert.AreEqual(0, rep.npcPoliticalDeltaY);
            Assert.AreEqual(0, run.politicalAxis.x);
            Assert.AreEqual(0, run.politicalAxis.y);
            Assert.AreEqual(-5, rep.npcRelationDelta); // Relação cai por padrão
        }

        [Test]
        public void Resolve_AcceptProposal_ResolvesNpcViaCatalog_WhenCardNpcFieldIsNull()
        {
            var run = new RunState();
            var deck = new DeckState();

            var npc = NpcDefinition.CreateRuntimeInstance("min_educacao", "Ministra da Educação", biasX: -6, biasY: -4);
            var npcCatalog = new Dictionary<string, NpcDefinition> { { npc.id, npc } };

            var card = CardDefinition.CreateRuntimeInstance(
                id: "card_escolas",
                title: "Construção de Escolas",
                description: "Verba educacional",
                left: new ChoiceDefinition("Aceitar"),
                right: new ChoiceDefinition("Recusar"),
                npcId: "min_educacao"
            );

            var rep = DecisionResolver.Resolve(run, deck, card, choiceIndex: 0, npcCatalog: npcCatalog);

            Assert.IsNotNull(rep);
            Assert.AreEqual(-2, rep.npcPoliticalDeltaX);
            Assert.AreEqual(-2, rep.npcPoliticalDeltaY);
            Assert.AreEqual(-2, run.politicalAxis.x);
            Assert.AreEqual(-2, run.politicalAxis.y);
        }

        [Test]
        public void RunState_ModifyNpcRelation_PositiveDelta_MovesPoliticalAxisTowardsNpcByFixedRelationStep()
        {
            var run = new RunState();
            var npc = NpcDefinition.CreateRuntimeInstance("min_fazenda", "Ministro da Fazenda", biasX: 5, biasY: -3);
            var npcCatalog = new Dictionary<string, NpcDefinition> { { npc.id, npc } };

            // Aumento de relação (+10) deve deslocar 1 ponto em direção a (5, -3)
            var (dx, dy) = run.ModifyNpcRelation("min_fazenda", delta: 10, npcCatalog);

            Assert.AreEqual(1, dx);
            Assert.AreEqual(-1, dy);
            Assert.AreEqual(1, run.politicalAxis.x);
            Assert.AreEqual(-1, run.politicalAxis.y);
            Assert.AreEqual(10, run.GetNpcRelation("min_fazenda"));
        }

        [Test]
        public void RunState_ModifyNpcRelation_NegativeDelta_DoesNotMovePoliticalAxis()
        {
            var run = new RunState();
            var npc = NpcDefinition.CreateRuntimeInstance("min_fazenda", "Ministro da Fazenda", biasX: 5, biasY: -3);
            var npcCatalog = new Dictionary<string, NpcDefinition> { { npc.id, npc } };

            var (dx, dy) = run.ModifyNpcRelation("min_fazenda", delta: -10, npcCatalog);

            Assert.AreEqual(0, dx);
            Assert.AreEqual(0, dy);
            Assert.AreEqual(0, run.politicalAxis.x);
            Assert.AreEqual(0, run.politicalAxis.y);
            Assert.AreEqual(-10, run.GetNpcRelation("min_fazenda"));
        }

        [Test]
        public void Resolve_AcceptProposal_WhenAxisLocked_DoesNotMovePoliticalAxis()
        {
            var run = new RunState();
            var deck = new DeckState();
            run.LockPoliticalAxis();

            var npc = NpcDefinition.CreateRuntimeInstance("min_agro", "Ministro Agro", biasX: 6, biasY: 2);
            var cardWithNpc = CardDefinition.CreateRuntimeInstance(
                id: "card_agro",
                title: "Subsídio ao Agro",
                description: "Proposta",
                left: new ChoiceDefinition("Aprovar"),
                right: new ChoiceDefinition("Rejeitar")
            );
            cardWithNpc.npc = npc;

            var rep = DecisionResolver.Resolve(run, deck, cardWithNpc, choiceIndex: 0);

            Assert.AreEqual(0, rep.npcPoliticalDeltaX);
            Assert.AreEqual(0, rep.npcPoliticalDeltaY);
            Assert.AreEqual(0, run.politicalAxis.x);
            Assert.AreEqual(0, run.politicalAxis.y);
        }
    }
}
