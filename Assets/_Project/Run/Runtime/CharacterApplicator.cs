using System;
using Mandato.Content;
using Mandato.Core;

namespace Mandato.Run
{
    public static class CharacterApplicator
    {
        public static void Apply(CharacterDefinition character, RunState runState)
        {
            if (character == null || runState == null) return;

            // 1. Atribui ID do personagem ativo
            runState.activeCharacterId = character.id ?? string.Empty;

            // 2. Atributos iniciais
            if (character.overrideInitialStats && character.initialStats != null)
            {
                runState.stats = character.initialStats.Clone();
            }

            // 3. Eixo Político inicial
            if (character.overridePoliticalAxis)
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

            // 4. Perks iniciais
            if (character.startingPerkIds != null)
            {
                foreach (var perkId in character.startingPerkIds)
                {
                    if (string.IsNullOrEmpty(perkId)) continue;

                    // Não concede se estiver explicitamente na lista de bloqueados
                    if (character.blockedPerkIds != null && character.blockedPerkIds.Contains(perkId))
                        continue;

                    runState.GrantPerk(perkId);
                }
            }

            // 5. Ações extras do celular
            if (character.extraUnlockedActionIds != null)
            {
                foreach (var actionId in character.extraUnlockedActionIds)
                {
                    if (!string.IsNullOrEmpty(actionId))
                    {
                        runState.UnlockAction(actionId);
                    }
                }
            }

            // 6. Ações bloqueadas do celular (remove se já estiverem desbloqueadas por padrão)
            if (character.lockedActionIds != null)
            {
                foreach (var lockedActionId in character.lockedActionIds)
                {
                    if (!string.IsNullOrEmpty(lockedActionId))
                    {
                        runState.LockAction(lockedActionId);
                    }
                }
            }

            // 7. Relacionamentos iniciais com NPCs
            if (character.initialNpcRelations != null)
            {
                foreach (var rel in character.initialNpcRelations)
                {
                    if (!string.IsNullOrEmpty(rel.npcId))
                    {
                        var npcState = runState.GetOrCreateNpcState(rel.npcId);
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
