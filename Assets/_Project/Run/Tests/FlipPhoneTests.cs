using System.Collections.Generic;
using NUnit.Framework;
using Mandato.Content;
using Mandato.Core;
using Mandato.Run;

namespace Mandato.Run.Tests
{
    public class FlipPhoneTests
    {
        private RunState runState;
        private DeckState deckState;
        private Dictionary<string, CardDefinition> catalog;

        [SetUp]
        public void Setup()
        {
            runState = new RunState(seed: 12345);
            deckState = new DeckState();
            catalog = new Dictionary<string, CardDefinition>();

            // Setup a few catalog cards
            var cardA = CardDefinition.CreateRuntimeInstance("card_a", "Proposta A", "Desc", null, null, npcId: "MinistroEco");
            var cardB = CardDefinition.CreateRuntimeInstance("card_b", "Proposta B", "Desc", null, null, npcId: "MinistroEco");
            var cardC = CardDefinition.CreateRuntimeInstance("card_c", "Proposta C", "Desc", null, null, npcId: "Deputado");

            catalog["card_a"] = cardA;
            catalog["card_b"] = cardB;
            catalog["card_c"] = cardC;

            deckState.Initialize(new[] { "card_a", "card_b", "card_c" });
        }

        [Test]
        public void StatImpact_AppliesDeltaToRunState()
        {
            var action = FlipPhoneActionDefinition.CreateRuntimeInstance(
                "action_pacote_economico",
                "Pacote de Estímulo",
                "Injeta recursos na economia ao custo de aprovação popular.",
                FlipPhoneCooldownType.None
            );
            action.effects.Add(FlipPhoneEffect.CreateStatImpact(new StatBlock(0, 0, -10, 20, 0)));

            var report = FlipPhoneResolver.ResolveUse(runState, deckState, action, catalog);

            Assert.IsTrue(report.success);
            Assert.AreEqual(50, report.statsBefore.economy);
            Assert.AreEqual(70, report.statsAfter.economy);
            Assert.AreEqual(40, report.statsAfter.popularApproval);
            Assert.AreEqual(70, runState.stats.economy);
            Assert.AreEqual(40, runState.stats.popularApproval);
        }

        [Test]
        public void Cooldown_Turns_BlocksUsageUntilAdvanced()
        {
            var action = FlipPhoneActionDefinition.CreateRuntimeInstance(
                "action_pronunciamento",
                "Pronunciamento Oficial",
                "Pronunciamento em rede nacional para acalmar a população.",
                FlipPhoneCooldownType.Turns,
                cooldownTurns: 2
            );
            action.effects.Add(FlipPhoneEffect.CreateStatImpact(new StatBlock(0, 0, 10, 0, 0)));

            // 1. Primeiro uso é bem-sucedido
            var report1 = FlipPhoneResolver.ResolveUse(runState, deckState, action, catalog);
            Assert.IsTrue(report1.success);
            Assert.AreEqual(60, runState.stats.popularApproval);
            Assert.IsTrue(runState.IsActionOnCooldown(action.id));
            Assert.AreEqual(2, runState.GetActionCooldown(action.id));

            // 2. Segundo uso consecutivo é bloqueado
            var report2 = FlipPhoneResolver.ResolveUse(runState, deckState, action, catalog);
            Assert.IsFalse(report2.success);
            Assert.IsTrue(report2.failReason.Contains("recarga"));

            // 3. Avança 1 mês: cooldown vai para 1 (ainda bloqueado)
            runState.AdvanceMonth();
            Assert.AreEqual(1, runState.GetActionCooldown(action.id));
            var report3 = FlipPhoneResolver.ResolveUse(runState, deckState, action, catalog);
            Assert.IsFalse(report3.success);

            // 4. Avança mais 1 mês: cooldown expira (0) e a ação volta a ficar disponível
            runState.AdvanceMonth();
            Assert.IsFalse(runState.IsActionOnCooldown(action.id));
            var report4 = FlipPhoneResolver.ResolveUse(runState, deckState, action, catalog);
            Assert.IsTrue(report4.success);
            Assert.AreEqual(70, runState.stats.popularApproval);
        }

        [Test]
        public void SingleUse_Action_CanOnlyBeUsedOnce()
        {
            var action = FlipPhoneActionDefinition.CreateRuntimeInstance(
                "action_decreto_secreto",
                "Decreto Presidencial",
                "Medida extrema de uso único.",
                FlipPhoneCooldownType.SingleUse
            );
            action.effects.Add(FlipPhoneEffect.CreateStatImpact(new StatBlock(0, 0, 0, 20, 10)));

            // 1. Primeiro uso funciona
            var report1 = FlipPhoneResolver.ResolveUse(runState, deckState, action, catalog);
            Assert.IsTrue(report1.success);
            Assert.IsTrue(runState.IsActionConsumed(action.id));

            // 2. Segundo uso é permanentemente bloqueado
            var report2 = FlipPhoneResolver.ResolveUse(runState, deckState, action, catalog);
            Assert.IsFalse(report2.success);
            Assert.IsTrue(report2.failReason.Contains("consumida"));
        }

        [Test]
        public void RemoveNpc_RemovesAllCardsFromNpcInDeck()
        {
            var action = FlipPhoneActionDefinition.CreateRuntimeInstance(
                "action_demitir_ministro",
                "Demitir Ministro",
                "Exonera o Ministro da Economia e remove suas propostas do baralho."
            );
            action.effects.Add(FlipPhoneEffect.CreateRemoveNpcFromGame("MinistroEco"));

            Assert.AreEqual(3, deckState.drawPile.Count);

            var report = FlipPhoneResolver.ResolveUse(runState, deckState, action, catalog);
            Assert.IsTrue(report.success);
            Assert.AreEqual(2, report.removedCardIds.Count);
            Assert.Contains("card_a", report.removedCardIds);
            Assert.Contains("card_b", report.removedCardIds);

            // Deck agora só tem a carta do Deputado
            Assert.AreEqual(1, deckState.drawPile.Count);
            Assert.AreEqual("card_c", deckState.drawPile[0]);
        }

        [Test]
        public void LockedAction_CannotBeUsed_UntilUnlocked()
        {
            var action = FlipPhoneActionDefinition.CreateRuntimeInstance(
                "action_gabinete_crise",
                "Gabinete de Crise",
                "Ação bloqueada inicialmente.",
                FlipPhoneCooldownType.None,
                unlockByDefault: false
            );

            // Bloqueada
            var report1 = FlipPhoneResolver.ResolveUse(runState, deckState, action, catalog);
            Assert.IsFalse(report1.success);
            Assert.IsTrue(report1.failReason.Contains("desbloqueada"));

            // Desbloqueia na run
            runState.UnlockAction(action.id);
            var report2 = FlipPhoneResolver.ResolveUse(runState, deckState, action, catalog);
            Assert.IsTrue(report2.success);
        }

        [Test]
        public void Conditions_MustBeMetToUseAction()
        {
            var action = FlipPhoneActionDefinition.CreateRuntimeInstance(
                "action_socorro_financeiro",
                "Socorro Financeiro",
                "Apenas utilizável se a economia estiver abaixo de 30%."
            );
            action.conditions.Add(new FlipPhoneCondition
            {
                checkStat = true,
                requiredStat = StatId.Economy,
                minStatValue = 0,
                maxStatValue = 30
            });

            // Economia padrão é 50: condição falha
            var report1 = FlipPhoneResolver.ResolveUse(runState, deckState, action, catalog);
            Assert.IsFalse(report1.success);

            // Reduz economia para 25
            runState.ApplyStatImpacts(new StatBlock(0, 0, 0, -25, 0));
            Assert.AreEqual(25, runState.stats.economy);

            // Agora a condição é atendida
            var report2 = FlipPhoneResolver.ResolveUse(runState, deckState, action, catalog);
            Assert.IsTrue(report2.success);
        }

        [Test]
        public void DismissProposal_FlagsProposalDismissal_WhenProposalExists()
        {
            var action = FlipPhoneActionDefinition.CreateRuntimeInstance(
                "action_descartar",
                "Engavetar Proposta",
                "Descarta a proposta atual sem aplicar suas consequências."
            );
            action.effects.Add(FlipPhoneEffect.CreateDismissProposal());

            var currentCard = catalog["card_a"];
            var report = FlipPhoneResolver.ResolveUse(runState, deckState, action, catalog, currentCard: currentCard);
            Assert.IsTrue(report.success);
            Assert.IsTrue(report.dismissedCurrentProposal);
        }

        [Test]
        public void DismissProposal_Fails_WhenNoProposalActive()
        {
            var action = FlipPhoneActionDefinition.CreateRuntimeInstance(
                "action_descartar",
                "Engavetar Proposta",
                "Descarta a proposta atual."
            );
            action.effects.Add(FlipPhoneEffect.CreateDismissProposal());

            var report = FlipPhoneResolver.ResolveUse(runState, deckState, action, catalog, currentCard: null);
            Assert.IsFalse(report.success);
            StringAssert.Contains("nenhuma proposta", report.failReason.ToLower());
        }

        [Test]
        public void ResolveUse_Fails_WhenRunTerminated()
        {
            var action = FlipPhoneActionDefinition.CreateRuntimeInstance("action_test", "Teste", "Desc");
            runState.ForceDefeat("Derrota de Teste");

            var report = FlipPhoneResolver.ResolveUse(runState, deckState, action, catalog);
            Assert.IsFalse(report.success);
            StringAssert.Contains("encerrada", report.failReason.ToLower());
        }

        [Test]
        public void StateMachine_DismissCurrentProposal_ResetsCurrentCardAndPreparesNextTurn()
        {
            var stateMachine = new RunStateMachine(seed: 42);
            stateMachine.StartRun(new[] { "card_a", "card_b" });

            bool drawn = stateMachine.DrawAndPresentProposal(catalog);
            Assert.IsTrue(drawn);
            Assert.IsNotNull(stateMachine.CurrentCard);
            Assert.AreEqual(RunPhase.AwaitingChoice, stateMachine.CurrentPhase);

            // Descarta a proposta
            stateMachine.DismissCurrentProposal(advanceMonth: false);

            Assert.IsNull(stateMachine.CurrentCard);
            Assert.AreEqual(RunPhase.PreparingRun, stateMachine.CurrentPhase);

            // Pode puxar a próxima proposta normalmente
            bool drawnNext = stateMachine.DrawAndPresentProposal(catalog);
            Assert.IsTrue(drawnNext);
            Assert.IsNotNull(stateMachine.CurrentCard);
        }

        [Test]
        public void PronunciamentoNacional_IncreasesAllStatsBy10_AndSets6TurnCooldown()
        {
            var action = FlipPhoneActionDefinition.CreateRuntimeInstance(
                "action_pronunciamento_nacional",
                "Pronunciamento Nacional",
                "Aumenta 10 de todos os atributos.",
                cooldownType: FlipPhoneCooldownType.Turns,
                cooldownTurns: 6
            );
            action.effects.Add(FlipPhoneEffect.CreateStatImpact(new StatBlock(10, 10, 10, 10, 0)));

            int initClimate = runState.stats.climaticChanges;
            int initEco = runState.stats.economy;
            int initRel = runState.stats.internationalRelations;
            int initApp = runState.stats.popularApproval;

            var report = FlipPhoneResolver.ResolveUse(runState, deckState, action, catalog);

            Assert.IsTrue(report.success);
            Assert.AreEqual(initClimate + 10, runState.stats.climaticChanges);
            Assert.AreEqual(initEco + 10, runState.stats.economy);
            Assert.AreEqual(initRel + 10, runState.stats.internationalRelations);
            Assert.AreEqual(initApp + 10, runState.stats.popularApproval);

            Assert.IsTrue(runState.IsActionOnCooldown("action_pronunciamento_nacional"));
            Assert.AreEqual(6, runState.GetActionCooldown("action_pronunciamento_nacional"));
        }

        [Test]
        public void EngavetarProposta_Requires50Corruption_AndDismissesProposal_SingleUse()
        {
            var action = FlipPhoneActionDefinition.CreateRuntimeInstance(
                "action_engavetar_proposta",
                "Engavetar Proposta",
                "Pula proposta com 50% ou mais de corrupção.",
                cooldownType: FlipPhoneCooldownType.SingleUse
            );
            action.conditions.Add(new FlipPhoneCondition
            {
                checkStat = true,
                requiredStat = StatId.Corruption,
                minStatValue = 50,
                maxStatValue = 100
            });
            action.effects.Add(FlipPhoneEffect.CreateDismissProposal());

            var currentCard = catalog["card_a"];

            // 1. Falha quando corrupção < 50
            runState.stats.corruption = 20;
            var reportFail = FlipPhoneResolver.ResolveUse(runState, deckState, action, catalog, currentCard: currentCard);
            Assert.IsFalse(reportFail.success);
            StringAssert.Contains("condições", reportFail.failReason.ToLower());

            // 2. Sucesso quando corrupção >= 50
            runState.stats.corruption = 55;
            var reportSuccess = FlipPhoneResolver.ResolveUse(runState, deckState, action, catalog, currentCard: currentCard);
            Assert.IsTrue(reportSuccess.success);
            Assert.IsTrue(reportSuccess.dismissedCurrentProposal);

            // 3. Como é SingleUse, não pode ser usada novamente
            Assert.IsTrue(runState.IsActionConsumed("action_engavetar_proposta"));
            var reportSecondUse = FlipPhoneResolver.ResolveUse(runState, deckState, action, catalog, currentCard: currentCard);
            Assert.IsFalse(reportSecondUse.success);
            StringAssert.Contains("consumida", reportSecondUse.failReason.ToLower());
        }

        [Test]
        public void EliteMundial_EnablesPreviewForCurrentMonth_AndApplies10Corruption_With6TurnsCooldown()
        {
            var action = FlipPhoneActionDefinition.CreateRuntimeInstance(
                "action_elite_mundial",
                "Elite Mundial",
                "Permite saber como os atributos serão afetados em cada resposta deste mês (+10 de corrupção).",
                cooldownType: FlipPhoneCooldownType.Turns,
                cooldownTurns: 6,
                unlockByDefault: false
            );
            action.effects.Add(FlipPhoneEffect.CreateStatImpact(new StatBlock(0, 0, 0, 0, 10)));
            action.effects.Add(FlipPhoneEffect.CreatePeekImpacts());

            // Bloqueada inicialmente
            var reportFail = FlipPhoneResolver.ResolveUse(runState, deckState, action, catalog);
            Assert.IsFalse(reportFail.success);

            // Desbloqueia na run
            runState.UnlockAction(action.id);
            int initCorruption = runState.stats.corruption;

            var reportSuccess = FlipPhoneResolver.ResolveUse(runState, deckState, action, catalog);
            Assert.IsTrue(reportSuccess.success);
            Assert.AreEqual(initCorruption + 10, runState.stats.corruption);
            Assert.IsTrue(reportSuccess.revealedMonthImpacts);
            Assert.IsTrue(runState.isPreviewAttributesActive);
            Assert.IsTrue(runState.IsActionOnCooldown(action.id));
            Assert.AreEqual(6, runState.GetActionCooldown(action.id));

            // Ao avançar o mês, a flag de preview temporária é resetada
            runState.AdvanceMonth();
            Assert.IsFalse(runState.isPreviewAttributesActive);
        }

        [Test]
        public void AssassinoDeAluguel_PermanentlyRemovesCurrentNpc_DismissesProposal_AndApplies30Corruption()
        {
            var action = FlipPhoneActionDefinition.CreateRuntimeInstance(
                "action_assassino_aluguel",
                "Assassino de Aluguel",
                "Retira o personagem da run permanentemente (+30 de corrupção).",
                cooldownType: FlipPhoneCooldownType.SingleUse,
                unlockByDefault: false
            );
            action.effects.Add(FlipPhoneEffect.CreateStatImpact(new StatBlock(0, 0, 0, 0, 30)));
            action.effects.Add(FlipPhoneEffect.CreateRemoveNpcFromGame(""));
            action.effects.Add(FlipPhoneEffect.CreateDismissProposal());

            runState.UnlockAction(action.id);
            var currentCard = catalog["card_a"]; // npcId = "MinistroEco"
            int initCorruption = runState.stats.corruption;

            var report = FlipPhoneResolver.ResolveUse(runState, deckState, action, catalog, currentCard: currentCard);
            Assert.IsTrue(report.success);
            Assert.AreEqual(initCorruption + 30, runState.stats.corruption);
            Assert.IsTrue(report.dismissedCurrentProposal);
            Assert.Contains("MinistroEco", report.removedNpcIds);

            var npcState = runState.GetOrCreateNpcState("MinistroEco");
            Assert.IsTrue(npcState.isDead);
            Assert.IsTrue(npcState.isRemoved);
            Assert.IsFalse(runState.IsNpcAvailable("MinistroEco"));

            // Cartas removidas do baralho
            Assert.IsFalse(deckState.drawPile.Contains("card_a"));
            Assert.IsFalse(deckState.drawPile.Contains("card_b"));
            Assert.IsTrue(runState.IsActionConsumed(action.id));
        }

        [Test]
        public void PoliciaFederal_SuspendsNpcFor24Months_DismissesProposal_AndApplies20Corruption()
        {
            var action = FlipPhoneActionDefinition.CreateRuntimeInstance(
                "action_policia_federal",
                "Polícia Federal",
                "Retira o NPC da run por 2 anos (+20 de corrupção).",
                cooldownType: FlipPhoneCooldownType.SingleUse,
                unlockByDefault: false
            );
            action.effects.Add(FlipPhoneEffect.CreateStatImpact(new StatBlock(0, 0, 0, 0, 20)));
            action.effects.Add(FlipPhoneEffect.CreateSuspendNpc(24, ""));
            action.effects.Add(FlipPhoneEffect.CreateDismissProposal());

            runState.UnlockAction(action.id);
            var currentCard = catalog["card_a"]; // npcId = "MinistroEco"
            int initCorruption = runState.stats.corruption;

            var report = FlipPhoneResolver.ResolveUse(runState, deckState, action, catalog, currentCard: currentCard);
            Assert.IsTrue(report.success);
            Assert.AreEqual(initCorruption + 20, runState.stats.corruption);
            Assert.IsTrue(report.dismissedCurrentProposal);
            Assert.Contains("MinistroEco", report.suspendedNpcIds);

            var npcState = runState.GetOrCreateNpcState("MinistroEco");
            Assert.AreEqual(24, npcState.suspendedMonths);
            Assert.IsTrue(npcState.isSuspended);
            Assert.IsFalse(runState.IsNpcAvailable("MinistroEco"));

            // Avança 1 mês: suspensão decrementa para 23
            runState.AdvanceMonth();
            Assert.AreEqual(23, npcState.suspendedMonths);
            Assert.IsFalse(runState.IsNpcAvailable("MinistroEco"));

            // Avança os 23 meses restantes
            for (int m = 0; m < 23; m++)
            {
                runState.AdvanceMonth();
            }

            Assert.AreEqual(0, npcState.suspendedMonths);
            Assert.IsFalse(npcState.isSuspended);
            Assert.IsTrue(runState.IsNpcAvailable("MinistroEco"));
        }

        [Test]
        public void PoliciaFederal_SuspendedNpcCardsNotDrawnUntil24MonthsElapsed()
        {
            var stateMachine = new RunStateMachine(seed: 42);
            // Deck inicial possui card_a e card_b (MinistroEco) e card_c (Deputado)
            stateMachine.StartRun(new[] { "card_a", "card_b", "card_c" });

            // Suspende MinistroEco por 24 meses
            var npcState = stateMachine.RunState.GetOrCreateNpcState("MinistroEco");
            npcState.suspendedMonths = 24;

            // Tentativas de puxar cartas: só pode vir a carta do Deputado (card_c) ou rotina neutra
            var drawn1 = stateMachine.DrawAndPresentProposal(catalog);
            Assert.IsTrue(drawn1);
            Assert.AreEqual("card_c", stateMachine.CurrentCard.id);

            // Próximo sorteio recicla o descarte e continua só podendo vir a única carta elegível (card_c do Deputado)
            var drawn2 = stateMachine.DrawAndPresentProposal(catalog);
            Assert.IsTrue(drawn2);
            Assert.AreEqual("card_c", stateMachine.CurrentCard.id);

            // Passam 24 meses
            for (int i = 0; i < 24; i++)
            {
                stateMachine.RunState.AdvanceMonth();
            }

            Assert.AreEqual(0, npcState.suspendedMonths);
            Assert.IsTrue(stateMachine.RunState.IsNpcAvailable("MinistroEco"));

            // Agora as cartas do Ministro voltam a ser elegíveis (injetando no topo para testar)
            stateMachine.DeckState.InjectCard("card_a", onTop: true);
            var drawn3 = stateMachine.DrawAndPresentProposal(catalog);
            Assert.IsTrue(drawn3);
            Assert.AreEqual("card_a", stateMachine.CurrentCard.id);
        }

        [Test]
        public void IndicesDePesquisa_PreventsStatLossDuringCurrentMonth_AndApplies25Corruption_With6TurnsCooldown()
        {
            var action = FlipPhoneActionDefinition.CreateRuntimeInstance(
                "action_indices_pesquisa",
                "Índices de Pesquisa",
                "Não perde atributos este mês (+25 de corrupção).",
                cooldownType: FlipPhoneCooldownType.Turns,
                cooldownTurns: 6,
                unlockByDefault: false
            );
            action.effects.Add(FlipPhoneEffect.CreateStatImpact(new StatBlock(0, 0, 0, 0, 25)));
            action.effects.Add(FlipPhoneEffect.CreatePreventStatLoss());

            runState.UnlockAction(action.id);
            int initCorruption = runState.stats.corruption;
            int initEco = runState.stats.economy;
            int initPop = runState.stats.popularApproval;

            var report = FlipPhoneResolver.ResolveUse(runState, deckState, action, catalog);
            Assert.IsTrue(report.success);
            Assert.AreEqual(initCorruption + 25, runState.stats.corruption);
            Assert.IsTrue(report.preventedStatLoss);
            Assert.IsTrue(runState.preventStatLossThisMonth);

            // Tenta aplicar um impacto fortemente negativo
            runState.ApplyStatImpacts(new StatBlock(-30, -30, -30, -30, 0));

            // Atributos não diminuíram
            Assert.AreEqual(initEco, runState.stats.economy);
            Assert.AreEqual(initPop, runState.stats.popularApproval);

            // Ganhos positivos continuam sendo aplicados
            runState.ApplyStatImpacts(new StatBlock(10, 10, 10, 10, 0));
            Assert.AreEqual(initEco + 10, runState.stats.economy);

            // Ao avançar o mês, a proteção expira
            runState.AdvanceMonth();
            Assert.IsFalse(runState.preventStatLossThisMonth);

            // Agora impactos negativos funcionam normalmente após o mês passar
            runState.ApplyStatImpacts(new StatBlock(-10, -10, -10, -10, 0));
            Assert.AreEqual(initEco, runState.stats.economy);
        }

        [Test]
        public void ComprarInfluencers_Grants20Popularity_AndApplies15Corruption_With6TurnsCooldown()
        {
            var action = FlipPhoneActionDefinition.CreateRuntimeInstance(
                "action_comprar_influencers",
                "Comprar Influencers",
                "Obtém +20 de Popularidade e +15 de corrupção.",
                cooldownType: FlipPhoneCooldownType.Turns,
                cooldownTurns: 6,
                unlockByDefault: false
            );
            action.effects.Add(FlipPhoneEffect.CreateStatImpact(new StatBlock(0, 0, 20, 0, 15)));

            runState.UnlockAction(action.id);
            int initPop = runState.stats.popularApproval;
            int initCorrupt = runState.stats.corruption;

            var report = FlipPhoneResolver.ResolveUse(runState, deckState, action, catalog);
            Assert.IsTrue(report.success);
            Assert.AreEqual(initPop + 20, runState.stats.popularApproval);
            Assert.AreEqual(initCorrupt + 15, runState.stats.corruption);
            Assert.IsTrue(runState.IsActionOnCooldown(action.id));
            Assert.AreEqual(6, runState.GetActionCooldown(action.id));
        }

        [Test]
        public void NpcRelationCondition_BlocksAction_WhenRelationOutOfRange()
        {
            var action = FlipPhoneActionDefinition.CreateRuntimeInstance(
                "action_alianca_politica",
                "Aliança Política",
                "Exige relacionamento >= 30 com o Ministro da Economia."
            );
            action.conditions.Add(new FlipPhoneCondition
            {
                checkNpcRelation = true,
                targetNpcIdForRelation = "MinistroEco",
                minNpcRelation = 30,
                maxNpcRelation = 100
            });
            action.effects.Add(FlipPhoneEffect.CreateStatImpact(new StatBlock(0, 0, 15, 0, 0)));

            var npcState = runState.GetOrCreateNpcState("MinistroEco");
            npcState.relationScore = 10; // Menor que 30

            // 1. Falha por não atender o relacionamento mínimo
            var reportFail = FlipPhoneResolver.ResolveUse(runState, deckState, action, catalog);
            Assert.IsFalse(reportFail.success);
            StringAssert.Contains("condições", reportFail.failReason.ToLower());

            // 2. Ajusta relacionamento para 45 (dentro do intervalo [30, 100])
            npcState.relationScore = 45;
            var reportSuccess = FlipPhoneResolver.ResolveUse(runState, deckState, action, catalog);
            Assert.IsTrue(reportSuccess.success);
            Assert.AreEqual(65, runState.stats.popularApproval);
        }

        [Test]
        public void LinkedNpc_ActionsBecomeUnavailable_WhenNpcIsSuspendedOrDead()
        {
            // Ação vinculada ao Ministro da Economia
            var action = FlipPhoneActionDefinition.CreateRuntimeInstance(
                "action_pacote_economico_vinculado",
                "Linha Direta Fazenda",
                "Ação fornecida pelo Ministro da Economia.",
                linkedNpcId: "MinistroEco"
            );
            action.effects.Add(FlipPhoneEffect.CreateStatImpact(new StatBlock(0, 0, 0, 15, 0)));

            // 1. Ministro disponível: ação pode ser usada
            Assert.IsTrue(runState.IsNpcAvailable("MinistroEco"));
            var report1 = FlipPhoneResolver.ResolveUse(runState, deckState, action, catalog);
            Assert.IsTrue(report1.success);
            Assert.AreEqual(65, runState.stats.economy);

            // 2. Prende / suspende o Ministro da Economia por 24 meses (ex: via Polícia Federal)
            var actionPF = FlipPhoneActionDefinition.CreateRuntimeInstance("action_pf_temp", "PF", "Prende visitante atual");
            actionPF.effects.Add(FlipPhoneEffect.CreateSuspendNpc(24, "")); // Prende visitante da proposta atual
            actionPF.effects.Add(FlipPhoneEffect.CreateDismissProposal());

            var currentCard = catalog["card_a"]; // card_a pertence a MinistroEco
            var reportPF = FlipPhoneResolver.ResolveUse(runState, deckState, actionPF, catalog, currentCard: currentCard);
            Assert.IsTrue(reportPF.success);
            Assert.IsFalse(runState.IsNpcAvailable("MinistroEco"));

            // 3. Tenta usar a ação vinculada ao Ministro suspenso: deve FALHAR
            var report2 = FlipPhoneResolver.ResolveUse(runState, deckState, action, catalog);
            Assert.IsFalse(report2.success);
            StringAssert.Contains("indisponível", report2.failReason.ToLower());

            // 4. Passam 24 meses: a suspensão expira
            for (int i = 0; i < 24; i++)
            {
                runState.AdvanceMonth();
            }
            Assert.IsTrue(runState.IsNpcAvailable("MinistroEco"));

            // 5. Ação volta a funcionar após o retorno do Ministro
            var report3 = FlipPhoneResolver.ResolveUse(runState, deckState, action, catalog);
            Assert.IsTrue(report3.success);

            // 6. Assassina o Ministro permanentemente
            var actionKill = FlipPhoneActionDefinition.CreateRuntimeInstance("action_kill_temp", "Kill", "Mata visitante");
            actionKill.effects.Add(FlipPhoneEffect.CreateRemoveNpcFromGame(""));
            FlipPhoneResolver.ResolveUse(runState, deckState, actionKill, catalog, currentCard: currentCard);

            Assert.IsFalse(runState.IsNpcAvailable("MinistroEco"));
            var report4 = FlipPhoneResolver.ResolveUse(runState, deckState, action, catalog);
            Assert.IsFalse(report4.success);
            StringAssert.Contains("indisponível", report4.failReason.ToLower());
        }

        [Test]
        public void ModifyNpcRelation_AppliesDeltaToTargetNpcOrCurrentProposal()
        {
            var actionExplicit = FlipPhoneActionDefinition.CreateRuntimeInstance(
                "action_afinar_relacao",
                "Jantar de Confraternização",
                "Melhora a relação com MinistroEco em +25."
            );
            actionExplicit.effects.Add(FlipPhoneEffect.CreateModifyNpcRelation(25, "MinistroEco"));

            var npcState = runState.GetOrCreateNpcState("MinistroEco");
            npcState.relationScore = 10;

            var report1 = FlipPhoneResolver.ResolveUse(runState, deckState, actionExplicit, catalog);
            Assert.IsTrue(report1.success);
            Assert.AreEqual(35, npcState.relationScore);

            // Agora testa ação que altera relação do visitante atual (sem targetId explícito)
            var actionCurrent = FlipPhoneActionDefinition.CreateRuntimeInstance(
                "action_elogio_publico",
                "Elogio Público",
                "Melhora a relação com a pessoa presente em +15."
            );
            actionCurrent.effects.Add(FlipPhoneEffect.CreateModifyNpcRelation(15, ""));

            var currentCard = catalog["card_c"]; // npcId = "Deputado"
            var depState = runState.GetOrCreateNpcState("Deputado");
            depState.relationScore = 0;

            var report2 = FlipPhoneResolver.ResolveUse(runState, deckState, actionCurrent, catalog, currentCard: currentCard);
            Assert.IsTrue(report2.success);
            Assert.AreEqual(15, depState.relationScore);
        }
    }
}
