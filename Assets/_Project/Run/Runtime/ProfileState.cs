using System;
using System.Collections.Generic;

namespace Mandato.Run
{
    [Serializable]
    public class ProfileState
    {
        public int totalRunsPlayed = 0;
        public int totalVictories = 0;
        public List<string> unlockedCharacterIds = new List<string>();
        public List<string> discoveredEndingIds = new List<string>();
        public List<string> unlockedCardIds = new List<string>();

        public void RecordRunCompleted(bool isVictory, string endingId = "")
        {
            totalRunsPlayed++;
            if (isVictory)
            {
                totalVictories++;
            }

            if (!string.IsNullOrEmpty(endingId) && !discoveredEndingIds.Contains(endingId))
            {
                discoveredEndingIds.Add(endingId);
            }
        }

        public bool UnlockCharacter(string characterId)
        {
            if (string.IsNullOrEmpty(characterId) || unlockedCharacterIds.Contains(characterId))
                return false;

            unlockedCharacterIds.Add(characterId);
            return true;
        }

        public bool UnlockCard(string cardId)
        {
            if (string.IsNullOrEmpty(cardId) || unlockedCardIds.Contains(cardId))
                return false;

            unlockedCardIds.Add(cardId);
            return true;
        }
    }
}
