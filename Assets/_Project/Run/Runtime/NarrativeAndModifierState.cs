using System;
using System.Collections.Generic;
using Mandato.Content;
using Mandato.Core;

namespace Mandato.Run
{
    [Serializable]
    public class NpcRunState
    {
        public string npcId = string.Empty;
        public int relationScore = 0; // -100 (Hostil) a +100 (Aliado fiel)
        public bool isMet = false;
        public int interactionCount = 0;

        public NpcRunState() { }

        public NpcRunState(string id, int initialRelation = 0)
        {
            npcId = id ?? string.Empty;
            relationScore = Math.Clamp(initialRelation, -100, 100);
        }

        public void ModifyRelation(int delta)
        {
            relationScore = Math.Clamp(relationScore + delta, -100, 100);
        }

        public void RecordInteraction()
        {
            isMet = true;
            interactionCount++;
        }
    }

    [Serializable]
    public class QuestRunState
    {
        public string questId = string.Empty;
        public int currentStepIndex = 0;
        public bool isCompleted = false;
        public bool isFailed = false;

        public QuestRunState() { }

        public QuestRunState(string id)
        {
            questId = id ?? string.Empty;
            currentStepIndex = 0;
            isCompleted = false;
            isFailed = false;
        }

        public bool AdvanceStep(QuestDefinition questDef)
        {
            if (isCompleted || isFailed || questDef == null) return false;

            currentStepIndex++;
            if (currentStepIndex >= questDef.steps.Count)
            {
                isCompleted = true;
            }
            return true;
        }

        public void FailQuest()
        {
            isFailed = true;
        }
    }

    [Serializable]
    public class ActivePerkState
    {
        public string perkId = string.Empty;
        public int remainingMonths = 0; // 0 = permanente
        public int timesUsed = 0;

        public bool IsExpired => remainingMonths < 0;

        public ActivePerkState() { }

        public ActivePerkState(string id, int duration = 0)
        {
            perkId = id ?? string.Empty;
            remainingMonths = duration;
            timesUsed = 0;
        }

        public void TickMonth()
        {
            if (remainingMonths > 0)
            {
                remainingMonths--;
            }
        }
    }

    [Serializable]
    public class ActiveEventState
    {
        public string eventId = string.Empty;
        public int remainingMonths = 3;

        public bool IsExpired => remainingMonths <= 0;

        public ActiveEventState() { }

        public ActiveEventState(string id, int duration = 3)
        {
            eventId = id ?? string.Empty;
            remainingMonths = duration;
        }

        public void TickMonth()
        {
            remainingMonths--;
        }
    }
}
