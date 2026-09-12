using System;
using System.Collections.Generic;

namespace Mandato.Run
{
    [Serializable]
    public class ProfileState
    {
        public int schemaVersion = 2;
        public int totalRunsPlayed = 0;
        public int totalVictories = 0;
        public int totalDecisionsMade = 0;
        public int highestPopularityScore = 0;

        public List<string> unlockedCharacterIds = new List<string>();
        public List<string> discoveredEndingIds = new List<string>();
        public List<string> unlockedCardIds = new List<string>();
        public List<string> unlockedActionIds = new List<string>();
        public List<string> completedQuestIds = new List<string>();
        public List<string> unlockedAchievementIds = new List<string>();

        public void RecordRunCompleted(bool isVictory, string endingId = "", int decisionsCount = 0, int finalPopularity = 0)
        {
            totalRunsPlayed++;
            if (isVictory)
            {
                totalVictories++;
            }

            totalDecisionsMade += decisionsCount;
            if (finalPopularity > highestPopularityScore)
            {
                highestPopularityScore = finalPopularity;
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

        public bool UnlockAction(string actionId)
        {
            if (string.IsNullOrEmpty(actionId) || unlockedActionIds.Contains(actionId))
                return false;

            unlockedActionIds.Add(actionId);
            return true;
        }

        public bool RecordQuestCompletion(string questId)
        {
            if (string.IsNullOrEmpty(questId) || completedQuestIds.Contains(questId))
                return false;

            completedQuestIds.Add(questId);
            return true;
        }

        public bool UnlockAchievement(string achievementId)
        {
            if (string.IsNullOrEmpty(achievementId) || unlockedAchievementIds.Contains(achievementId))
                return false;

            unlockedAchievementIds.Add(achievementId);
            return true;
        }

        public void EnsureCollectionsInitialized()
        {
            if (unlockedCharacterIds == null) unlockedCharacterIds = new List<string>();
            if (discoveredEndingIds == null) discoveredEndingIds = new List<string>();
            if (unlockedCardIds == null) unlockedCardIds = new List<string>();
            if (unlockedActionIds == null) unlockedActionIds = new List<string>();
            if (completedQuestIds == null) completedQuestIds = new List<string>();
            if (unlockedAchievementIds == null) unlockedAchievementIds = new List<string>();
        }
    }
}

