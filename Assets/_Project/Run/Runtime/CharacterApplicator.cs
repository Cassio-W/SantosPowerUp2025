using System;
using System.Collections.Generic;
using Mandato.Content;
using Mandato.Core;

namespace Mandato.Run
{
    public static class CharacterApplicator
    {
        private static readonly string[] DefaultPerkPool =
        {
            "AliancaEUA",
            "InvestimentoUsina",
            "AliancaJudiciario",
            "TratadoInternacional",
            "ReservaFlorestal",
            "Imorrivel",
            "Cripto",
            "ComunicadorNato",
            "GrandeIrmao",
            "AliancaLegislativo",
            "MovimentoAmbientalista",
            "SempreAposteEmNada",
            "ThisIsBrasil"
        };

        private static readonly string[] DefaultActionPool =
        {
            "action_assassino_aluguel",
            "action_comprar_influencers",
            "action_engavetar_proposta",
            "action_elite_mundial",
            "action_indices_pesquisa",
            "action_policia_federal",
            "action_pronunciamento_nacional"
        };

        public static void Apply(CharacterDefinition character, RunState runState)
        {
            Apply(character, runState, null, null);
        }

        public static void Apply(
            CharacterDefinition character,
            RunState runState,
            IReadOnlyDictionary<string, PerkDefinition> perkCatalog = null,
            IReadOnlyDictionary<string, FlipPhoneActionDefinition> actionCatalog = null)
        {
            if (character == null || runState == null) return;

            var rng = new Random(runState.seed != 0 ? runState.seed : Environment.TickCount);

            // 1. Atribui ID do personagem ativo
            runState.activeCharacterId = character.id ?? string.Empty;

            // 2. Atributos iniciais (fixos ou aleatórios, ex: Caneta Vermelha)
            if (character.randomizeInitialStats)
            {
                int minVal = Math.Max(1, character.minRandomStatValue);
                int maxVal = Math.Max(minVal, character.maxRandomStatValue);
                int initCorruption;

                if (character.randomizeCorruption)
                {
                    int minCorrupt = Math.Max(0, character.minRandomCorruptionValue);
                    int maxCorrupt = Math.Max(minCorrupt, character.maxRandomCorruptionValue);
                    initCorruption = rng.Next(minCorrupt, maxCorrupt + 1);
                }
                else
                {
                    initCorruption = character.initialStats != null ? character.initialStats.corruption : 0;
                }

                runState.stats = new StatBlock(
                    climate: rng.Next(minVal, maxVal + 1),
                    relations: rng.Next(minVal, maxVal + 1),
                    approval: rng.Next(minVal, maxVal + 1),
                    eco: rng.Next(minVal, maxVal + 1),
                    corrupt: initCorruption,
                    clamp: true
                );
            }
            else if (character.overrideInitialStats && character.initialStats != null)
            {
                runState.stats = character.initialStats.Clone();
            }

            // 3. Eixo Político inicial
            if (character.overridePoliticalAxis || character.initialPoliticalX != 0 || character.initialPoliticalY != 0)
            {
                runState.politicalAxis = new PoliticalAxis(
                    character.initialPoliticalX,
                    character.initialPoliticalY,
                    character.lockPoliticalAxis
                );
            }
            else if (character.lockPoliticalAxis)
            {
                runState.LockPoliticalAxis();
            }

            // 4. Perks iniciais fixos
            var blockedPerks = character.GetBlockedPerkIds();
            foreach (var perkId in character.GetStartingPerkIds())
            {
                if (string.IsNullOrEmpty(perkId)) continue;
                if (blockedPerks != null && blockedPerks.Contains(perkId)) continue;

                int duration = 0;
                if (perkCatalog != null && perkCatalog.TryGetValue(perkId, out var pDef) && pDef != null)
                {
                    duration = pDef.durationMonths;
                }

                runState.GrantPerk(perkId, duration, perkCatalog);
            }

            // 4b. Perks iniciais aleatórios (ex: Caneta Vermelha -> 1 Perk Random)
            if (character.randomStartingPerksCount > 0)
            {
                var candidatePerks = new List<string>();
                if (perkCatalog != null && perkCatalog.Count > 0)
                {
                    foreach (var pId in perkCatalog.Keys)
                    {
                        if (!string.IsNullOrEmpty(pId) &&
                            !runState.HasPerk(pId) &&
                            (blockedPerks == null || !blockedPerks.Contains(pId)))
                        {
                            candidatePerks.Add(pId);
                        }
                    }
                }
                else
                {
                    foreach (var pId in DefaultPerkPool)
                    {
                        if (!runState.HasPerk(pId) &&
                            (blockedPerks == null || !blockedPerks.Contains(pId)))
                        {
                            candidatePerks.Add(pId);
                        }
                    }
                }

                int toPick = Math.Min(character.randomStartingPerksCount, candidatePerks.Count);
                for (int i = 0; i < toPick; i++)
                {
                    int idx = rng.Next(0, candidatePerks.Count);
                    string chosenPerkId = candidatePerks[idx];
                    candidatePerks.RemoveAt(idx);

                    int duration = 0;
                    if (perkCatalog != null && perkCatalog.TryGetValue(chosenPerkId, out var pDef) && pDef != null)
                    {
                        duration = pDef.durationMonths;
                    }

                    runState.GrantPerk(chosenPerkId, duration, perkCatalog);
                }
            }

            runState.RefreshMaxStatBonuses(perkCatalog);

            // 5. Ações extras do celular e cargas iniciais (ex: 3x Polícia Federal)
            var startingCharges = character.GetStartingActionCharges();
            foreach (var kvp in startingCharges)
            {
                if (!string.IsNullOrEmpty(kvp.Key))
                {
                    runState.UnlockAction(kvp.Key, kvp.Value);
                }
            }

            // 5b. Contatos/ações iniciais aleatórias (ex: Caneta Vermelha -> 1 Contato Random)
            var lockedActionSet = new HashSet<string>(character.GetLockedActionIds(), StringComparer.OrdinalIgnoreCase);
            if (character.randomStartingActionsCount > 0)
            {
                var candidateActions = new List<string>();
                if (actionCatalog != null && actionCatalog.Count > 0)
                {
                    foreach (var aId in actionCatalog.Keys)
                    {
                        if (!string.IsNullOrEmpty(aId) &&
                            !runState.IsActionUnlocked(aId) &&
                            !lockedActionSet.Contains(aId))
                        {
                            candidateActions.Add(aId);
                        }
                    }
                }
                else
                {
                    foreach (var aId in DefaultActionPool)
                    {
                        if (!runState.IsActionUnlocked(aId) &&
                            !lockedActionSet.Contains(aId))
                        {
                            candidateActions.Add(aId);
                        }
                    }
                }

                int toPick = Math.Min(character.randomStartingActionsCount, candidateActions.Count);
                for (int i = 0; i < toPick; i++)
                {
                    int idx = rng.Next(0, candidateActions.Count);
                    string chosenActionId = candidateActions[idx];
                    candidateActions.RemoveAt(idx);

                    runState.UnlockAction(chosenActionId, charges: 1);
                }
            }

            // 6. Ações bloqueadas do celular (remove se já estiverem desbloqueadas por padrão)
            foreach (var lockedActionId in lockedActionSet)
            {
                if (!string.IsNullOrEmpty(lockedActionId))
                {
                    runState.LockAction(lockedActionId);
                }
            }

            // 7. Relacionamentos iniciais com NPCs
            if (character.initialNpcRelations != null)
            {
                foreach (var rel in character.initialNpcRelations)
                {
                    string targetNpcId = rel.GetNpcId();
                    if (!string.IsNullOrEmpty(targetNpcId))
                    {
                        var npcState = runState.GetOrCreateNpcState(targetNpcId);
                        if (npcState != null)
                        {
                            npcState.relationScore = rel.initialRelation;
                        }
                    }
                }
            }

            // 8. Atualiza o status de término da run após novos atributos
            runState.UpdateTermination();
        }
    }
}
