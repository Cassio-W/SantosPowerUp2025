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
            var blockedPerks = character.GetBlockedPerkIds();
            foreach (var perkId in character.GetStartingPerkIds())
            {
                if (string.IsNullOrEmpty(perkId)) continue;
                if (blockedPerks != null && blockedPerks.Contains(perkId)) continue;

                runState.GrantPerk(perkId);
            }

            // 5. Ações extras do celular
            foreach (var actionId in character.GetExtraUnlockedActionIds())
            {
                if (!string.IsNullOrEmpty(actionId))
                {
                    runState.UnlockAction(actionId);
                }
            }

            // 6. Ações bloqueadas do celular (remove se já estiverem desbloqueadas por padrão)
            foreach (var lockedActionId in character.GetLockedActionIds())
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
