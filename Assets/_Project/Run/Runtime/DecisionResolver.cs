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

        public List<string> injectedCardIds = new List<string>();
        public List<string> removedCardIds = new List<string>();
        public string grantedPerkId = string.Empty;
        public string presentationCue = string.Empty;

        public RunTermination resultingTermination;

        public bool IsRunTerminated => resultingTermination.IsDefeat || resultingTermination.IsVictory;
    }

    public static class DecisionResolver
    {
        public static ResolutionReport Resolve(
            RunState runState,
            DeckState deckState,
            CardDefinition card,
            int choiceIndex)
        {
            if (runState == null || card == null) return null;

            ChoiceDefinition choice = card.GetChoice(choiceIndex);
            if (choice == null) return null;

            var report = new ResolutionReport
            {
                cardId = card.id,
                cardTitle = card.title,
                choiceIndex = choiceIndex,
                choiceLabel = choice.label,
                statsBefore = runState.stats.Clone(),
                deltaPoliticalX = choice.deltaPoliticalX,
                deltaPoliticalY = choice.deltaPoliticalY,
                grantedPerkId = choice.grantPerkId,
                presentationCue = choice.presentationCue
            };

            // 1. Aplica impactos em atributos
            runState.ApplyStatImpacts(choice.statImpacts, choice.hasCorruptionMods);
            report.statsAfter = runState.stats.Clone();
            report.impactsApplied = choice.statImpacts?.Clone() ?? new StatBlock(0, 0, 0, 0, 0);

            // 2. Aplica deslocamento do eixo político
            if (choice.deltaPoliticalX != 0 || choice.deltaPoliticalY != 0)
            {
                runState.ApplyPoliticalDelta(choice.deltaPoliticalX, choice.deltaPoliticalY);
            }

            // 3. Atualiza o baralho (injeção e remoção)
            if (deckState != null)
            {
                if (choice.injectCardIds != null)
                {
                    foreach (string id in choice.injectCardIds)
                    {
                        if (!string.IsNullOrEmpty(id))
                        {
                            deckState.InjectCard(id, onTop: true);
                            report.injectedCardIds.Add(id);
                        }
                    }
                }

                if (choice.removeCardIds != null)
                {
                    foreach (string id in choice.removeCardIds)
                    {
                        if (!string.IsNullOrEmpty(id))
                        {
                            deckState.RemoveCard(id);
                            report.removedCardIds.Add(id);
                        }
                    }
                }
            }

            // 4. Concede perk se houver
            if (!string.IsNullOrEmpty(choice.grantPerkId) && !runState.activePerkIds.Contains(choice.grantPerkId))
            {
                runState.activePerkIds.Add(choice.grantPerkId);
            }

            // 5. Registra no histórico da run
            var record = new DecisionRecord(
                card.id,
                choiceIndex,
                choice.label,
                runState.calendar.currentMonthIndex,
                runState.calendar.DisplayDate
            );
            runState.decisionHistory.Add(record.cardId);

            // 6. Atualiza o status terminal
            report.resultingTermination = runState.termination;

            return report;
        }
    }
}
