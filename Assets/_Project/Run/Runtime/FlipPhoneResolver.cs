using System;
using System.Collections.Generic;
using Mandato.Content;
using Mandato.Core;

namespace Mandato.Run
{
    [Serializable]
    public class FlipPhoneUseReport
    {
        public bool success;
        public string failReason = string.Empty;
        public string actionId = string.Empty;
        public string actionDisplayName = string.Empty;

        public StatBlock statsBefore;
        public StatBlock statsAfter;
        public StatBlock impactsApplied = new StatBlock(0, 0, 0, 0, 0);

        public int deltaPoliticalX = 0;
        public int deltaPoliticalY = 0;

        public List<string> injectedCardIds = new List<string>();
        public List<string> removedCardIds = new List<string>();
        public List<string> removedNpcIds = new List<string>();
        public List<string> suspendedNpcIds = new List<string>();
        public List<string> grantedPerkIds = new List<string>();
        public List<string> triggeredEventIds = new List<string>();

        public bool dismissedCurrentProposal = false;
        public bool revealedMonthImpacts = false;
        public bool preventedStatLoss = false;
        public RunTermination resultingTermination;
    }

    public static class FlipPhoneResolver
    {
        public static FlipPhoneUseReport ResolveUse(
            RunState runState,
            DeckState deckState,
            FlipPhoneActionDefinition action,
            IReadOnlyDictionary<string, CardDefinition> catalog = null,
            CardDefinition currentCard = null,
            IReadOnlyDictionary<string, PerkDefinition> perkCatalog = null)
        {
            if (runState == null || action == null)
            {
                return new FlipPhoneUseReport { success = false, failReason = "RunState ou Ação nula." };
            }

            // 1. Verifica se a partida está em andamento
            if (!runState.termination.IsOngoing)
            {
                return new FlipPhoneUseReport { success = false, failReason = "A partida já está encerrada." };
            }

            // 2. Verifica se o NPC vinculado à ação está disponível (não foi preso, afastado ou morto)
            string linkedNpc = action.GetLinkedNpcId();
            if (!string.IsNullOrEmpty(linkedNpc) && !runState.IsNpcAvailable(linkedNpc))
            {
                return new FlipPhoneUseReport { success = false, failReason = "O contato vinculado está indisponível (preso, afastado ou falecido)." };
            }

            // 3. Verifica se está desbloqueada
            if (!runState.IsActionUnlocked(action.id) && !action.unlockByDefault)
            {
                return new FlipPhoneUseReport { success = false, failReason = "Ação não desbloqueada." };
            }

            // 4. Verifica se já foi consumida (SingleUse)
            if (action.cooldownType == FlipPhoneCooldownType.SingleUse && runState.IsActionConsumed(action.id))
            {
                return new FlipPhoneUseReport { success = false, failReason = "Ação de uso único já consumida." };
            }

            // 5. Verifica cooldown ativo
            if (runState.IsActionOnCooldown(action.id))
            {
                int remaining = runState.GetActionCooldown(action.id);
                return new FlipPhoneUseReport { success = false, failReason = $"Ação em recarga ({remaining} turno(s) restante(s))." };
            }

            // 6. Valida efeitos que requerem proposta ou visitante ativo
            if (action.effects != null)
            {
                bool requiresProposal = action.effects.Exists(e => e != null && (
                    e.effectType == FlipPhoneEffectType.DismissCurrentProposal ||
                    (e.effectType == FlipPhoneEffectType.RemoveNpcFromGame && string.IsNullOrEmpty(e.GetTargetId())) ||
                    (e.effectType == FlipPhoneEffectType.SuspendNpc && string.IsNullOrEmpty(e.GetTargetId())) ||
                    (e.effectType == FlipPhoneEffectType.ModifyNpcRelation && string.IsNullOrEmpty(e.GetTargetId()))
                ));
                if (requiresProposal && currentCard == null)
                {
                    return new FlipPhoneUseReport { success = false, failReason = "Nenhuma proposta ativa para esta ação." };
                }
            }

            // 7. Valida condições
            string currentNpcId = currentCard != null ? currentCard.GetNpcId() : string.Empty;
            if (!action.AreConditionsMet(runState.stats, runState.calendar.currentMonthIndex, runState.activePerkIds, runState.decisionHistory, currentNpcId, runState.GetNpcRelation))
            {
                return new FlipPhoneUseReport { success = false, failReason = "Condições da ação não atendidas." };
            }

            var report = new FlipPhoneUseReport
            {
                success = true,
                actionId = action.id,
                actionDisplayName = action.displayName,
                statsBefore = runState.stats.Clone()
            };

            // 8. Aplica cada efeito
            if (action.effects != null)
            {
                foreach (var effect in action.effects)
                {
                    if (effect == null) continue;

                    string effTargetId = effect.GetTargetId();

                    switch (effect.effectType)
                    {
                        case FlipPhoneEffectType.StatImpact:
                            if (effect.statImpacts != null)
                            {
                                runState.ApplyStatImpacts(effect.statImpacts);
                                report.impactsApplied.climaticChanges += effect.statImpacts.climaticChanges;
                                report.impactsApplied.economy += effect.statImpacts.economy;
                                report.impactsApplied.internationalRelations += effect.statImpacts.internationalRelations;
                                report.impactsApplied.popularApproval += effect.statImpacts.popularApproval;
                                report.impactsApplied.corruption += effect.statImpacts.corruption;
                            }
                            break;

                        case FlipPhoneEffectType.PoliticalImpact:
                            runState.ApplyPoliticalDelta(effect.deltaPoliticalX, effect.deltaPoliticalY);
                            report.deltaPoliticalX += effect.deltaPoliticalX;
                            report.deltaPoliticalY += effect.deltaPoliticalY;
                            break;

                        case FlipPhoneEffectType.InjectCard:
                            if (!string.IsNullOrEmpty(effTargetId) && deckState != null)
                            {
                                deckState.InjectCard(effTargetId, effect.injectOnTop);
                                report.injectedCardIds.Add(effTargetId);
                            }
                            break;

                        case FlipPhoneEffectType.RemoveCard:
                            if (!string.IsNullOrEmpty(effTargetId) && deckState != null)
                            {
                                deckState.RemoveCard(effTargetId);
                                report.removedCardIds.Add(effTargetId);
                            }
                            break;

                        case FlipPhoneEffectType.RemoveNpcFromGame:
                            string targetRemoveNpc = !string.IsNullOrEmpty(effTargetId)
                                ? effTargetId
                                : (currentCard != null ? currentCard.GetNpcId() : string.Empty);
                            if (!string.IsNullOrEmpty(targetRemoveNpc))
                            {
                                var npcState = runState.GetOrCreateNpcState(targetRemoveNpc);
                                if (npcState != null)
                                {
                                    npcState.relationScore = -100;
                                    npcState.isDead = true;
                                    npcState.isRemoved = true;
                                }

                                if (deckState != null && catalog != null)
                                {
                                    var removed = deckState.RemoveCardsByNpc(targetRemoveNpc, catalog);
                                    if (removed != null)
                                    {
                                        report.removedCardIds.AddRange(removed);
                                    }
                                }

                                report.removedNpcIds.Add(targetRemoveNpc);
                            }
                            break;

                        case FlipPhoneEffectType.SuspendNpc:
                            string targetSuspendNpc = !string.IsNullOrEmpty(effTargetId)
                                ? effTargetId
                                : (currentCard != null ? currentCard.GetNpcId() : string.Empty);
                            if (!string.IsNullOrEmpty(targetSuspendNpc))
                            {
                                var npcState = runState.GetOrCreateNpcState(targetSuspendNpc);
                                if (npcState != null)
                                {
                                    npcState.suspendedMonths = effect.duration > 0 ? effect.duration : 24;
                                }
                                report.suspendedNpcIds.Add(targetSuspendNpc);
                            }
                            break;

                        case FlipPhoneEffectType.ModifyNpcRelation:
                            string targetRelationNpc = !string.IsNullOrEmpty(effTargetId)
                                ? effTargetId
                                : (currentCard != null ? currentCard.GetNpcId() : string.Empty);
                            if (!string.IsNullOrEmpty(targetRelationNpc))
                            {
                                var npcState = runState.GetOrCreateNpcState(targetRelationNpc);
                                if (npcState != null)
                                {
                                    npcState.ModifyRelation(effect.deltaNpcRelation);
                                }
                            }
                            break;

                        case FlipPhoneEffectType.PeekStatImpacts:
                            runState.isPreviewAttributesActive = true;
                            report.revealedMonthImpacts = true;
                            break;

                        case FlipPhoneEffectType.PreventStatLoss:
                            runState.preventStatLossThisMonth = true;
                            report.preventedStatLoss = true;
                            break;

                        case FlipPhoneEffectType.GrantPerk:
                            if (!string.IsNullOrEmpty(effTargetId))
                            {
                                int duration = effect.duration;
                                if (duration <= 0 && perkCatalog != null && perkCatalog.TryGetValue(effTargetId, out var pDef) && pDef != null)
                                {
                                    duration = pDef.durationMonths;
                                }
                                runState.GrantPerk(effTargetId, duration);
                                report.grantedPerkIds.Add(effTargetId);
                            }
                            break;

                        case FlipPhoneEffectType.TriggerEvent:
                            if (!string.IsNullOrEmpty(effTargetId))
                            {
                                runState.TriggerEvent(effTargetId, effect.duration > 0 ? effect.duration : 3);
                                report.triggeredEventIds.Add(effTargetId);
                            }
                            break;

                        case FlipPhoneEffectType.DismissCurrentProposal:
                            report.dismissedCurrentProposal = true;
                            break;
                    }
                }
            }

            // 8. Registra consumo / cooldown
            runState.RecordActionUsed(action.id, action.cooldownType, action.cooldownTurns);

            // 9. Resgate Emergencial e Término
            runState.CheckAndApplyEmergencyRescue(perkCatalog);
            runState.UpdateTermination();

            report.statsAfter = runState.stats.Clone();
            report.resultingTermination = runState.termination;

            return report;
        }
    }
}
