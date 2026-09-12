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
            action.effects.Add(FlipPhoneEffect.CreateStatImpact(new StatBlock(0, 20, 0, -10, 0)));

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
                "Aumenta aprovação popular com cooldown de 2 turnos.",
                FlipPhoneCooldownType.Turns,
                cooldownTurns: 2
            );
            action.effects.Add(FlipPhoneEffect.CreateStatImpact(new StatBlock(0, 0, 0, 15, 0)));

            // Primeiro uso: sucesso
            var report1 = FlipPhoneResolver.ResolveUse(runState, deckState, action, catalog);
            Assert.IsTrue(report1.success);
            Assert.IsTrue(runState.IsActionOnCooldown(action.id));
            Assert.AreEqual(2, runState.GetActionCooldown(action.id));

            // Segundo uso imediato: bloqueado por cooldown
            var report2 = FlipPhoneResolver.ResolveUse(runState, deckState, action, catalog);
            Assert.IsFalse(report2.success);
            StringAssert.Contains("recarga", report2.failReason.ToLower());

            // Avança 1 mês: cooldown vai para 1
            runState.AdvanceMonth();
            Assert.IsTrue(runState.IsActionOnCooldown(action.id));
            Assert.AreEqual(1, runState.GetActionCooldown(action.id));

            var report3 = FlipPhoneResolver.ResolveUse(runState, deckState, action, catalog);
            Assert.IsFalse(report3.success);

            // Avança mais 1 mês: cooldown expira
            runState.AdvanceMonth();
            Assert.IsFalse(runState.IsActionOnCooldown(action.id));
            Assert.AreEqual(0, runState.GetActionCooldown(action.id));

            // Agora pode ser usada novamente
            var report4 = FlipPhoneResolver.ResolveUse(runState, deckState, action, catalog);
            Assert.IsTrue(report4.success);
        }

        [Test]
        public void SingleUse_Action_CanOnlyBeUsedOnce()
        {
            var action = FlipPhoneActionDefinition.CreateRuntimeInstance(
                "action_intervencao_extrema",
                "Intervenção Federal",
                "Ação única drástica.",
                FlipPhoneCooldownType.SingleUse
            );
            action.effects.Add(FlipPhoneEffect.CreateStatImpact(new StatBlock(0, 30, 0, 0, 0)));

            var report1 = FlipPhoneResolver.ResolveUse(runState, deckState, action, catalog);
            Assert.IsTrue(report1.success);
            Assert.IsTrue(runState.IsActionConsumed(action.id));

            // Mesmo após avançar meses, continua consumida
            runState.AdvanceMonth();
            runState.AdvanceMonth();

            var report2 = FlipPhoneResolver.ResolveUse(runState, deckState, action, catalog);
            Assert.IsFalse(report2.success);
            StringAssert.Contains("consumida", report2.failReason.ToLower());
        }

        [Test]
        public void RemoveNpc_RemovesAllCardsFromNpcInDeck()
        {
            var action = FlipPhoneActionDefinition.CreateRuntimeInstance(
                "action_exonerar_ministro",
                "Demitir Ministro da Economia",
                "Remove o ministro e todas as suas propostas da partida.",
                FlipPhoneCooldownType.SingleUse
            );
            action.effects.Add(FlipPhoneEffect.CreateRemoveNpc("MinistroEco"));

            Assert.AreEqual(3, deckState.TotalActiveCards);

            var report = FlipPhoneResolver.ResolveUse(runState, deckState, action, catalog);
            Assert.IsTrue(report.success);
            Assert.Contains("MinistroEco", report.removedNpcIds);

            // Resta apenas a carta do Deputado no baralho
            Assert.AreEqual(1, deckState.TotalActiveCards);
            Assert.AreEqual("card_c", deckState.drawPile[0]);
        }

        [Test]
        public void LockedAction_CannotBeUsed_UntilUnlocked()
        {
            var action = FlipPhoneActionDefinition.CreateRuntimeInstance(
                "action_secreta",
                "Operação Secreta",
                "Ação bloqueada inicialmente.",
                unlockByDefault: false
            );

            // Não está desbloqueada
            var report1 = FlipPhoneResolver.ResolveUse(runState, deckState, action, catalog);
            Assert.IsFalse(report1.success);

            // Desbloqueia na run
            runState.UnlockAction("action_secreta");
            Assert.IsTrue(runState.IsActionUnlocked("action_secreta"));

            var report2 = FlipPhoneResolver.ResolveUse(runState, deckState, action, catalog);
            Assert.IsTrue(report2.success);
        }

        [Test]
        public void Conditions_MustBeMetToUseAction()
        {
            var action = FlipPhoneActionDefinition.CreateRuntimeInstance(
                "action_condicional",
                "Ação de Emergência Econômica",
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
            runState.ApplyStatImpacts(new StatBlock(0, -25, 0, 0, 0));
            Assert.AreEqual(25, runState.stats.economy);

            // Agora a condição é atendida
            var report2 = FlipPhoneResolver.ResolveUse(runState, deckState, action, catalog);
            Assert.IsTrue(report2.success);
        }

        [Test]
        public void DismissProposal_FlagsProposalDismissal()
        {
            var action = FlipPhoneActionDefinition.CreateRuntimeInstance(
                "action_descartar",
                "Engavetar Proposta",
                "Descarta a proposta atual sem aplicar suas consequências."
            );
            action.effects.Add(FlipPhoneEffect.CreateDismissProposal());

            var report = FlipPhoneResolver.ResolveUse(runState, deckState, action, catalog);
            Assert.IsTrue(report.success);
            Assert.IsTrue(report.dismissedCurrentProposal);
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
