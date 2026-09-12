using System.Collections.Generic;
using Mandato.Content;
using Mandato.Core;
using Mandato.Run;
using NUnit.Framework;

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
        public void DecisionResolver_UpdatesNpcRelation_AndAdvancesQuest()
        {
            var runState = new RunState();
            var card1 = CardDefinition.CreateRuntimeInstance("card_step_1", "Proposta 1", "", new ChoiceDefinition("Aprovar"), new ChoiceDefinition("Rejeitar"), npcId: "npc_senador");
            var card2 = CardDefinition.CreateRuntimeInstance("card_step_2", "Proposta 2", "", new ChoiceDefinition("Aprovar"), new ChoiceDefinition("Rejeitar"), npcId: "npc_senador");

            var quest = QuestDefinition.CreateRuntimeInstance("quest_senado", "npc_senador", "Acordo do Senado");
            quest.rewardPerkId = "perk_apoio_senado";
            quest.steps.Add(new QuestStepDefinition { stepIndex = 0, triggerCardId = "card_step_1", requiredChoiceIndex = 0 });
            quest.steps.Add(new QuestStepDefinition { stepIndex = 1, triggerCardId = "card_step_2", requiredChoiceIndex = 0 });

            var questCatalog = new Dictionary<string, QuestDefinition> { { quest.id, quest } };

            // Decisão 1: Aprova (choiceIndex = 0)
            var report1 = DecisionResolver.Resolve(runState, null, card1, 0, questCatalog);

            Assert.AreEqual("npc_senador", report1.npcId);
            Assert.AreEqual(5, report1.npcRelationAfter);
            Assert.AreEqual(1, runState.GetOrCreateNpcState("npc_senador").interactionCount);
            Assert.IsTrue(report1.advancedQuestIds.Contains("quest_senado"));
            Assert.IsFalse(runState.IsQuestCompleted("quest_senado"));

            // Decisão 2: Aprova etapa 2 (conclusão da quest)
            var report2 = DecisionResolver.Resolve(runState, null, card2, 0, questCatalog);

            Assert.AreEqual(10, report2.npcRelationAfter);
            Assert.IsTrue(report2.completedQuestIds.Contains("quest_senado"));
            Assert.IsTrue(report2.grantedRewardPerkIds.Contains("perk_apoio_senado"));
            Assert.IsTrue(runState.IsQuestCompleted("quest_senado"));
            Assert.IsTrue(runState.activePerkIds.Contains("perk_apoio_senado"));
        }

        [Test]
        public void CardCondition_FiltersByNpcRelation_AndQuestState()
        {
            var runState = new RunState();
            var conditionNpc = new CardCondition
            {
                checkNpcRelation = true,
                targetNpcId = "npc_governador",
                minNpcRelation = 20
            };

            var conditionQuest = new CardCondition
            {
                requiredQuestId = "quest_metro",
                requireQuestCompleted = true
            };

            // Sem atingir requisitos
            Assert.IsFalse(conditionNpc.IsMet(runState.stats, 1, null, null, id => runState.GetNpcRelation(id), id => runState.GetQuestState(id)));
            Assert.IsFalse(conditionQuest.IsMet(runState.stats, 1, null, null, id => runState.GetNpcRelation(id), id => runState.GetQuestState(id)));

            // Atende relação
            runState.GetOrCreateNpcState("npc_governador").ModifyRelation(30);
            Assert.IsTrue(conditionNpc.IsMet(runState.stats, 1, null, null, id => runState.GetNpcRelation(id), id => runState.GetQuestState(id)));

            // Conclui quest
            var quest = runState.GetOrCreateQuestState("quest_metro");
            quest.isCompleted = true;
            Assert.IsTrue(conditionQuest.IsMet(runState.stats, 1, null, null, id => runState.GetNpcRelation(id), id => runState.GetQuestState(id)));
        }

        [Test]
        public void CardCondition_FiltersByPoliticalAxis()
        {
            var axis = new PoliticalAxis(-6, 4); // Esquerda Autoritária
            var condition = new CardCondition
            {
                requiredPoliticalQuadrant = "Esquerda Autoritária"
            };

            Assert.IsTrue(condition.IsMet(null, 1, null, politicalAxis: axis));

            axis.Unlock();
            axis.ApplyDelta(12, 0); // Vira Direita Autoritária
            Assert.IsFalse(condition.IsMet(null, 1, null, politicalAxis: axis));
        }
    }
}
