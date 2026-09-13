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
        public List<string> grantedPerkIds = new List<string>();
        public List<string> triggeredEventIds = new List<string>();

        public bool dismissedCurrentProposal = false;
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

            // 2. Verifica se está desbloqueada
            if (!runState.IsActionUnlocked(action.id) && !action.unlockByDefault)
            {
                return new FlipPhoneUseReport { success = false, failReason = "Ação não desbloqueada." };
            }

            // 3. Verifica se já foi consumida (SingleUse)
            if (action.cooldownType == FlipPhoneCooldownType.SingleUse && runState.IsActionConsumed(action.id))
            {
                return new FlipPhoneUseReport { success = false, failReason = "Ação de uso único já consumida." };
            }

            // 4. Verifica cooldown ativo
            if (runState.IsActionOnCooldown(action.id))
            {
                int remaining = runState.GetActionCooldown(action.id);
                return new FlipPhoneUseReport { success = false, failReason = $"Ação em recarga ({remaining} turno(s) restante(s))." };
            }

            // 5. Valida efeitos que requerem proposta ativa
            if (action.effects != null)
            {
                bool requiresProposal = action.effects.Exists(e => e != null && e.effectType == FlipPhoneEffectType.DismissCurrentProposal);
                if (requiresProposal && currentCard == null)
                {
                    return new FlipPhoneUseReport { success = false, failReason = "Nenhuma proposta ativa para dispensar." };
                }
            }

            // 6. Valida condições
            string currentNpcId = currentCard != null ? currentCard.npcId : string.Empty;
            if (!action.AreConditionsMet(runState.stats, runState.calendar.currentMonthIndex, runState.activePerkIds, runState.decisionHistory, currentNpcId))
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

            // 7. Aplica cada efeito
            if (action.effects != null)
            {
                foreach (var effect in action.effects)
                {
                    if (effect == null) continue;

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
                            if (!string.IsNullOrEmpty(effect.targetId) && deckState != null)
                            {
                                deckState.InjectCard(effect.targetId, effect.injectOnTop);
                                report.injectedCardIds.Add(effect.targetId);
                            }
                            break;

                        case FlipPhoneEffectType.RemoveCard:
                            if (!string.IsNullOrEmpty(effect.targetId) && deckState != null)
                            {
                                deckState.RemoveCard(effect.targetId);
                                report.removedCardIds.Add(effect.targetId);
                            }
                            break;

                        case FlipPhoneEffectType.RemoveNpcFromGame:
                            if (!string.IsNullOrEmpty(effect.targetId))
                            {
                                var npcState = runState.GetOrCreateNpcState(effect.targetId);
                                if (npcState != null)
                                {
                                    npcState.relationScore = -100;
                                    npcState.isDead = true;
                                    npcState.isRemoved = true;
                                }

                                if (deckState != null && catalog != null)
                                {
                                    var removed = deckState.RemoveCardsByNpc(effect.targetId, catalog);
                                    if (removed != null)
                                    {
                                        report.removedCardIds.AddRange(removed);
                                    }
                                }

                                report.removedNpcIds.Add(effect.targetId);
                            }
                            break;

                        case FlipPhoneEffectType.GrantPerk:
                            if (!string.IsNullOrEmpty(effect.targetId))
                            {
                                int duration = effect.duration;
                                if (duration <= 0 && perkCatalog != null && perkCatalog.TryGetValue(effect.targetId, out var pDef) && pDef != null)
                                {
                                    duration = pDef.durationMonths;
                                }
                                runState.GrantPerk(effect.targetId, duration);
                                report.grantedPerkIds.Add(effect.targetId);
                            }
                            break;

                        case FlipPhoneEffectType.TriggerEvent:
                            if (!string.IsNullOrEmpty(effect.targetId))
                            {
                                runState.TriggerEvent(effect.targetId, effect.duration > 0 ? effect.duration : 3);
                                report.triggeredEventIds.Add(effect.targetId);
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
