using System;
using System.Collections.Generic;
using System.Linq;
using Mandato.Content;
using Mandato.Core;

namespace Mandato.Run
{
    public static class EndingEvaluator
    {
        public static EndingDefinition EvaluateEnding(
            RunState runState,
            IEnumerable<EndingDefinition> endingsCatalog)
        {
            if (runState == null || endingsCatalog == null) return null;

            bool isVictory = runState.termination.IsVictory;
            string currentQuadrant = runState.politicalAxis.Quadrant;
            var candidates = new List<EndingDefinition>();

            foreach (var ending in endingsCatalog)
            {
                if (ending == null) continue;

                // 1. Checa condição de vitória / derrota
                if (ending.requiredVictory != isVictory) continue;

                // 2. Checa quadrante político
                if (!string.IsNullOrEmpty(ending.requiredQuadrant) &&
                    !string.Equals(ending.requiredQuadrant, currentQuadrant, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // 3. Checa limiares de atributos
                if (ending.minEconomy >= 0 && runState.stats.economy < ending.minEconomy) continue;
                if (ending.minClimate >= 0 && runState.stats.climaticChanges < ending.minClimate) continue;
                if (ending.minPopularApproval >= 0 && runState.stats.popularApproval < ending.minPopularApproval) continue;
                if (ending.minCorruption >= 0 && runState.stats.corruption < ending.minCorruption) continue;

                // 4. Checa quests concluídas
                if (!string.IsNullOrEmpty(ending.requiredCompletedQuestId))
                {
                    if (!runState.questStates.TryGetValue(ending.requiredCompletedQuestId, out var qState) || !qState.isCompleted)
                    {
                        continue;
                    }
                }

                candidates.Add(ending);
            }

            // Retorna o candidato com maior prioridade
            if (candidates.Count > 0)
            {
                return candidates.OrderByDescending(c => c.priority).First();
            }

            // Fallback genérico em runtime caso nenhum final customizado combine
            return EndingDefinition.CreateRuntimeInstance(
                id: isVictory ? "ending_generic_victory" : "ending_generic_defeat",
                title: isVictory ? "Mandato Concluído" : "Fim de Governo",
                epilogue: isVictory
                    ? $"Você completou os 4 anos de mandato com posicionamento {currentQuadrant}."
                    : $"O mandato encerrou-se prematuramente: {runState.termination.reason}",
                victory: isVictory,
                quadrant: currentQuadrant,
                priority: 0
            );
        }
    }
}
