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
    }
}
