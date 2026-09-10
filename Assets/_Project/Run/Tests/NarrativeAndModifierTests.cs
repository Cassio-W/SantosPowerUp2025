using NUnit.Framework;
using Mandato.Content;
using Mandato.Core;
using Mandato.Run;

namespace Mandato.Run.Tests
{
    public class NarrativeAndModifierTests
    {
        [Test]
        public void NpcRunState_ModifiesRelationAndTracksInteractions()
        {
            var run = new RunState();
            var npcState = run.GetOrCreateNpcState("npc_claudio");

            Assert.AreEqual(0, npcState.relationScore);
            Assert.IsFalse(npcState.isMet);

            npcState.RecordInteraction();
            npcState.ModifyRelation(30);

            Assert.IsTrue(npcState.isMet);
            Assert.AreEqual(1, npcState.interactionCount);
            Assert.AreEqual(30, npcState.relationScore);

            // Testa limites [-100..100]
            npcState.ModifyRelation(150);
            Assert.AreEqual(100, npcState.relationScore);
        }

        [Test]
        public void QuestRunState_AdvancesStepsUntilCompletion()
        {
            var quest = QuestDefinition.CreateRuntimeInstance("quest_previdencia", "npc_claudio", "Reforma da Previdência");
            quest.steps.Add(new QuestStepDefinition { stepIndex = 0, triggerCardId = "card_1" });
            quest.steps.Add(new QuestStepDefinition { stepIndex = 1, triggerCardId = "card_2" });

            var run = new RunState();
            var questState = run.GetOrCreateQuestState(quest.id);

            Assert.AreEqual(0, questState.currentStepIndex);
            Assert.IsFalse(questState.isCompleted);

            // Avança etapa 1
            questState.AdvanceStep(quest);
            Assert.AreEqual(1, questState.currentStepIndex);
            Assert.IsFalse(questState.isCompleted);

            // Avança etapa 2 (conclusão)
            questState.AdvanceStep(quest);
            Assert.AreEqual(2, questState.currentStepIndex);
            Assert.IsTrue(questState.isCompleted);
        }

        [Test]
        public void EventsAndPerks_ExpireCorrectlyOnMonthAdvance()
        {
            var run = new RunState();

            // Adiciona evento temporário de 2 meses e perk temporário de 1 mês
            run.TriggerEvent("event_campanha_eleitoral", duration: 2);
            run.GrantPerk("perk_populista", duration: 1);

            Assert.AreEqual(1, run.activeEvents.Count);
            Assert.IsTrue(run.activePerkIds.Contains("perk_populista"));

            // Avança 1 mês
            run.AdvanceMonth();
            Assert.AreEqual(1, run.activeEvents.Count);
            Assert.AreEqual(1, run.activeEvents[0].remainingMonths);
            Assert.IsFalse(run.activePerkIds.Contains("perk_populista")); // Perk expirou após 1 mês

            // Avança 2º mês
            run.AdvanceMonth();
            Assert.AreEqual(0, run.activeEvents.Count); // Evento expirou
        }

        [Test]
        public void PhoneAction_AppliesCostsAndImpacts()
        {
            var action = PhoneActionDefinition.CreateRuntimeInstance(
                id: "action_pacote_emergencial",
                name: "Pacote Emergencial",
                description: "Injeta dinheiro rápido na economia.",
                popCost: -5,
                corruptCost: 10,
                impacts: new StatBlock(0, 0, 0, 20, 0)
            );

            var run = new RunState();

            // Aplica os custos e efeitos da ação
            run.ApplyStatDelta(StatId.PopularApproval, action.politicalCost);
            run.ApplyStatDelta(StatId.Corruption, action.corruptionCost);
            run.ApplyStatImpacts(action.instantStatImpacts);

            Assert.AreEqual(45, run.stats.popularApproval);
            Assert.AreEqual(10, run.stats.corruption);
            Assert.AreEqual(70, run.stats.economy);
        }
    }
}
