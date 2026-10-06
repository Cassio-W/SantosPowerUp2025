using System;
using System.Collections.Generic;
using Mandato.Content;
using Mandato.Core;

namespace Mandato.Run
{
    [Serializable]
    public struct DecisionRecord
    {
        public string cardId;
        public int choiceIndex;
        public string choiceLabel;
        public int monthIndex;
        public string displayDate;

        public DecisionRecord(string cardId, int choiceIndex, string choiceLabel, int monthIndex, string displayDate)
        {
            this.cardId = cardId ?? string.Empty;
            this.choiceIndex = choiceIndex;
            this.choiceLabel = choiceLabel ?? string.Empty;
            this.monthIndex = monthIndex;
            this.displayDate = displayDate ?? string.Empty;
        }
    }

    [Serializable]
    public class ResolutionReport
    {
        public string cardId = string.Empty;
        public string cardTitle = string.Empty;
        public int choiceIndex = 0;
        public string choiceLabel = string.Empty;

        public StatBlock statsBefore;
        public StatBlock statsAfter;
        public StatBlock impactsApplied;

        public int deltaPoliticalX = 0;
        public int deltaPoliticalY = 0;
        public int npcPoliticalDeltaX = 0;
        public int npcPoliticalDeltaY = 0;

        public List<string> injectedCardIds = new List<string>();
        public List<string> removedCardIds = new List<string>();
        public string grantedPerkId = string.Empty;
        public string presentationCue = string.Empty;

        // Narrativa & NPCs
        public string npcId = string.Empty;
        public int npcRelationBefore = 0;
        public int npcRelationAfter = 0;
        public int npcRelationDelta = 0;

        // Quests
        public List<string> advancedQuestIds = new List<string>();
        public List<string> completedQuestIds = new List<string>();
        public List<string> grantedRewardPerkIds = new List<string>();
        public List<string> rescuedByPerkIds = new List<string>();

        public RunTermination resultingTermination;

        public bool IsRunTerminated => resultingTermination.IsDefeat || resultingTermination.IsVictory;
    }

    public static class DecisionResolver
    {
        public const int DefaultAcceptNpcRelationDelta = 5;
        public const int DefaultRejectNpcRelationDelta = -5;
        public const int DefaultProposalPoliticalStep = PoliticalAxis.DefaultProposalStep;
        public const int DefaultRelationPoliticalStep = PoliticalAxis.DefaultRelationStep;

        public static ResolutionReport Resolve(
            RunState runState,
            DeckState deckState,
            CardDefinition card,
            int choiceIndex,
            IReadOnlyDictionary<string, QuestDefinition> questCatalog = null,
            IReadOnlyDictionary<string, PerkDefinition> perkCatalog = null,
            IReadOnlyDictionary<string, NpcDefinition> npcCatalog = null)
        {
            if (runState == null || card == null) return null;
            if (choiceIndex < 0 || choiceIndex > 1) return null;

            ChoiceDefinition choice = card.GetChoice(choiceIndex);
            if (choice == null) return null;

            string resolvedNpcId = card.GetNpcId();

            var report = new ResolutionReport
            {
                cardId = card.id,
                cardTitle = card.title,
                choiceIndex = choiceIndex,
                choiceLabel = choice.label,
                statsBefore = runState.stats.Clone(),
                deltaPoliticalX = choice.deltaPoliticalX,
                deltaPoliticalY = choice.deltaPoliticalY,
                grantedPerkId = choice.GetGrantPerkId(),
                presentationCue = choice.presentationCue,
                npcId = resolvedNpcId
            };

            // 1. Aplica impactos em atributos
            runState.ApplyStatImpacts(choice.statImpacts, choice.hasCorruptionMods);
            report.statsAfter = runState.stats.Clone();
            report.impactsApplied = choice.statImpacts?.Clone() ?? new StatBlock(0, 0, 0, 0, 0);

            // 2. Aplica deslocamento do eixo político da escolha
            if (choice.deltaPoliticalX != 0 || choice.deltaPoliticalY != 0)
            {
                runState.ApplyPoliticalDelta(choice.deltaPoliticalX, choice.deltaPoliticalY);
            }

            // Deslocamento na direção do eixo político do NPC (ao aceitar proposta)
            int npcPolDeltaX = 0;
            int npcPolDeltaY = 0;
            if (choiceIndex == 0) // Apenas aceitar proposta desloca em direção ao viés do NPC
            {
                NpcDefinition resolvedNpc = card.npc;
                if (resolvedNpc == null && !string.IsNullOrEmpty(resolvedNpcId) && npcCatalog != null)
                {
                    npcCatalog.TryGetValue(resolvedNpcId, out resolvedNpc);
                }

                if (resolvedNpc != null)
                {
                    var (pDx, pDy) = runState.MovePoliticalAxisTowards(
                        resolvedNpc.politicalBiasX,
                        resolvedNpc.politicalBiasY,
                        DefaultProposalPoliticalStep
                    );
                    npcPolDeltaX = pDx;
                    npcPolDeltaY = pDy;
                }
            }

            report.deltaPoliticalX = choice.deltaPoliticalX + npcPolDeltaX;
            report.deltaPoliticalY = choice.deltaPoliticalY + npcPolDeltaY;
            report.npcPoliticalDeltaX = npcPolDeltaX;
            report.npcPoliticalDeltaY = npcPolDeltaY;

            // 3. Atualiza o baralho (injeção e remoção)
            if (deckState != null)
            {
                foreach (string id in choice.GetInjectCardIds())
                {
                    if (!string.IsNullOrEmpty(id))
                    {
                        deckState.InjectCard(id, onTop: true);
                        report.injectedCardIds.Add(id);
                    }
                }

                foreach (string id in choice.GetRemoveCardIds())
                {
                    if (!string.IsNullOrEmpty(id))
                    {
                        deckState.RemoveCard(id);
                        report.removedCardIds.Add(id);
                    }
                }
            }

            // 4. Concede perk se houver
            string perkToGrant = choice.GetGrantPerkId();
            if (!string.IsNullOrEmpty(perkToGrant))
            {
                int duration = 0;
                if (perkCatalog != null && perkCatalog.TryGetValue(perkToGrant, out var perkDef) && perkDef != null)
                {
                    duration = perkDef.durationMonths;
                }
                runState.GrantPerk(perkToGrant, duration);
            }

            // 5. Agenda evento interativo se a escolha definir um
            if (!string.IsNullOrEmpty(choice.scheduleEventId))
            {
                runState.ScheduleEvent(choice.scheduleEventId);
            }

            // 6. Atualização de Relação com NPC
            if (!string.IsNullOrEmpty(resolvedNpcId))
            {
                var npcState = runState.GetOrCreateNpcState(resolvedNpcId);
                report.npcRelationBefore = npcState.relationScore;
                npcState.RecordInteraction();

                int relationDelta = choice.deltaNpcRelation != 0
                    ? choice.deltaNpcRelation
                    : (choiceIndex == 0 ? DefaultAcceptNpcRelationDelta : DefaultRejectNpcRelationDelta);

                npcState.ModifyRelation(relationDelta);
                report.npcRelationAfter = npcState.relationScore;
                report.npcRelationDelta = relationDelta;
            }

            // 6. Resolução de Quests
            if (questCatalog != null)
            {
                foreach (var kvp in questCatalog)
                {
                    string qId = kvp.Key;
                    QuestDefinition qDef = kvp.Value;
                    if (qDef == null || qDef.steps == null || qDef.steps.Count == 0) continue;

                    var qState = runState.GetOrCreateQuestState(qId);
                    if (qState.isCompleted || qState.isFailed) continue;

                    if (qState.currentStepIndex < qDef.steps.Count)
                    {
                        var step = qDef.steps[qState.currentStepIndex];
                        if (step != null && string.Equals(step.GetTriggerCardId(), card.id, StringComparison.OrdinalIgnoreCase))
                        {
                            if (step.requiredChoiceIndex == -1 || step.requiredChoiceIndex == choiceIndex)
                            {
                                bool advanced = qState.AdvanceStep(qDef);
                                if (advanced)
                                {
                                    report.advancedQuestIds.Add(qId);
                                    if (qState.isCompleted)
                                    {
                                        report.completedQuestIds.Add(qId);
                                        string rewardPerk = qDef.GetRewardPerkId();
                                        if (!string.IsNullOrEmpty(rewardPerk))
                                        {
                                            int rewardDuration = 0;
                                            if (perkCatalog != null && perkCatalog.TryGetValue(rewardPerk, out var rewardDef) && rewardDef != null)
                                            {
                                                rewardDuration = rewardDef.durationMonths;
                                            }
                                            runState.GrantPerk(rewardPerk, rewardDuration);
                                            report.grantedRewardPerkIds.Add(rewardPerk);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // 7. Registra no histórico da run
            var record = new DecisionRecord(
                card.id,
                choiceIndex,
                choice.label,
                runState.calendar.currentMonthIndex,
                runState.calendar.DisplayDate
            );
            runState.decisionHistory.Add(record.cardId);

            // Registra os detalhes completos da decisão para a UI diegética do calendário
            string resolvedNpcName = resolvedNpcId;
            if (card.npc != null && !string.IsNullOrEmpty(card.npc.displayName))
            {
                resolvedNpcName = card.npc.displayName;
            }
            else if (!string.IsNullOrEmpty(resolvedNpcId) && npcCatalog != null && npcCatalog.TryGetValue(resolvedNpcId, out var nDef) && nDef != null && !string.IsNullOrEmpty(nDef.displayName))
            {
                resolvedNpcName = nDef.displayName;
            }
            else if (string.IsNullOrEmpty(resolvedNpcName))
            {
                resolvedNpcName = "Ministério";
            }

            string stat1Txt = string.Empty;
            bool stat1Pos = true;
            string stat2Txt = string.Empty;
            bool stat2Pos = true;

            if (choice.statImpacts != null)
            {
                var impacts = new List<(string text, bool positive)>();
                if (choice.statImpacts.popularApproval != 0)
                    impacts.Add(($"{choice.statImpacts.popularApproval:+0;-0} Popularidade", choice.statImpacts.popularApproval > 0));
                if (choice.statImpacts.economy != 0)
                    impacts.Add(($"{choice.statImpacts.economy:+0;-0} Economia", choice.statImpacts.economy > 0));
                if (choice.statImpacts.internationalRelations != 0)
                    impacts.Add(($"{choice.statImpacts.internationalRelations:+0;-0} Relações", choice.statImpacts.internationalRelations > 0));
                if (choice.statImpacts.climaticChanges != 0)
                    impacts.Add(($"{choice.statImpacts.climaticChanges:+0;-0} Clima", choice.statImpacts.climaticChanges > 0));
                if (choice.statImpacts.corruption != 0)
                    impacts.Add(($"{choice.statImpacts.corruption:+0;-0} Corrupção", choice.statImpacts.corruption < 0));

                if (impacts.Count > 0)
                {
                    stat1Txt = impacts[0].text;
                    stat1Pos = impacts[0].positive;
                }
                if (impacts.Count > 1)
                {
                    stat2Txt = impacts[1].text;
                    stat2Pos = impacts[1].positive;
                }
            }

            string perkSum = string.Empty;
            if (!string.IsNullOrEmpty(perkToGrant))
            {
                perkSum = $"★ Perk: {perkToGrant}";
            }

            var monthRecord = new MonthDecisionRecord
            {
                monthIndex = runState.calendar.currentMonthIndex,
                cardId = card.id,
                title = card.title,
                npcName = resolvedNpcName,
                isApproved = (choiceIndex == 0),
                choiceLabel = choice.label,
                stat1Text = stat1Txt,
                stat1Positive = stat1Pos,
                stat2Text = stat2Txt,
                stat2Positive = stat2Pos,
                perkText = perkSum,
                isEvent = false
            };
            runState.RecordMonthDecision(monthRecord);

            // 8. Avalia e aplica perks de resgate emergencial caso algum atributo tenha zerado
            var rescued = runState.CheckAndApplyEmergencyRescue(perkCatalog);
            if (rescued != null && rescued.Count > 0)
            {
                report.rescuedByPerkIds.AddRange(rescued);
                report.statsAfter = runState.stats.Clone();
            }

            // 9. Atualiza o status terminal
            runState.UpdateTermination();
            report.resultingTermination = runState.termination;

            return report;
        }
    }
}
