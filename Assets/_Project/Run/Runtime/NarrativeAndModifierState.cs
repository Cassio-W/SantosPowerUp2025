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
        public bool isDead = false;
        public bool isRemoved = false;
        public int suspendedMonths = 0;
        public int interactionCount = 0;

        public bool isSuspended => suspendedMonths > 0;

        public int relationship
        {
            get => relationScore;
            set => relationScore = Math.Clamp(value, -100, 100);
        }

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

    /// <summary>
    /// Estado de runtime de um Investimento a Longo Prazo ativo.
    /// Rastreia quantos meses restam e o ID da definição do investimento.
    /// A definição completa é consultada via catálogo; este estado é puro dado da run.
    /// </summary>
    [Serializable]
    public class ActiveInvestmentState
    {
        /// <summary>ID da LongTermInvestmentDefinition associada.</summary>
        public string investmentId = string.Empty;

        /// <summary>Meses restantes até a conclusão.</summary>
        public int remainingMonths = 0;

        /// <summary>Duração original do investimento (para exibição de progresso).</summary>
        public int totalMonths = 0;

        /// <summary>Se verdadeiro, o investimento foi cancelado e não produzirá recompensas.</summary>
        public bool isCancelled = false;

        public bool IsCompleted => !isCancelled && remainingMonths <= 0;

        public ActiveInvestmentState() { }

        public ActiveInvestmentState(string id, int duration)
        {
            investmentId = id ?? string.Empty;
            remainingMonths = Math.Max(1, duration);
            totalMonths = remainingMonths;
        }

        /// <summary>
        /// Decrementa um mês e retorna true se o investimento foi concluído neste tick.
        /// </summary>
        public bool TickMonth()
        {
            if (isCancelled || remainingMonths <= 0) return false;
            remainingMonths--;
            return remainingMonths <= 0;
        }

        /// <summary>Progresso de 0.0 a 1.0 (0 = início, 1 = concluído).</summary>
        public float Progress => totalMonths > 0
            ? Math.Clamp(1f - (float)remainingMonths / totalMonths, 0f, 1f)
            : 1f;
    }
}
