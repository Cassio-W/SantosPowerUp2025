using System;

namespace Mandato.Core
{
    /// <summary>
    /// Escala de dificuldade do Evento de Guerra, definida pelo alvo atacado.
    /// </summary>
    public enum WarLevel
    {
        None = 0,
        Neighbor = 1,         // Nível 1 — Médio: países vizinhos
        Dominant = 2,         // Nível 2 — Difícil: nações dominantes
        WorldDomination = 3   // Nível 3 — Impossível: dominação mundial
    }

    /// <summary>
    /// Desfecho do Evento de Guerra.
    /// <c>None</c> significa "sem desfecho" (guerra inexistente ou em andamento) no estado da run
    /// e "sem exigência" quando usado como requisito em um final.
    /// </summary>
    public enum WarOutcome
    {
        None = 0,
        Victory = 1,
        Surrendered = 2,
        Defeat = 3
    }

    /// <summary>Atributo militar que pode receber investimento (Tecnologia ← Economia, Tropas ← Popularidade).</summary>
    public enum WarAttribute
    {
        Technology = 0,
        Troops = 1
    }

    public static class WarLevelExtensions
    {
        public static string DisplayName(this WarLevel level)
        {
            return level switch
            {
                WarLevel.Neighbor => "Nível 1 — Médio (país vizinho)",
                WarLevel.Dominant => "Nível 2 — Difícil (nação dominante)",
                WarLevel.WorldDomination => "Nível 3 — Impossível (dominação mundial)",
                _ => string.Empty
            };
        }
    }

    /// <summary>
    /// Visão somente-leitura da guerra para a UI (exibida na aba Guerra do monitor retrô).
    /// </summary>
    [Serializable]
    public class WarSnapshot
    {
        public bool IsActive { get; }
        public WarLevel Level { get; }
        public string TargetId { get; }

        /// <summary>Progresso do "cabo de guerra" em 0..100 (50 = equilíbrio; 0 = derrota; 100 = vitória).</summary>
        public int Progress { get; }

        /// <summary>Tecnologia militar em pontos 0..100, sempre múltiplo de 25 (4 níveis: ver <see cref="TechnologyLevel"/>).</summary>
        public int Technology { get; }

        /// <summary>Tropas em pontos 0..100, sempre múltiplo de 25 (4 níveis: ver <see cref="TroopsLevel"/>).</summary>
        public int Troops { get; }

        /// <summary>Nível de Tecnologia de 0 (zerada) a 4 (máximo).</summary>
        public int TechnologyLevel => Technology / WarSnapshot.LevelStep;

        /// <summary>Nível de Tropas de 0 (zeradas) a 4 (máximo).</summary>
        public int TroopsLevel => Troops / WarSnapshot.LevelStep;

        public bool CanSurrender { get; }
        public bool HasInvestedThisMonth { get; }

        /// <summary>Quantidade de cliques de investimento ainda disponíveis neste mês.</summary>
        public int RemainingInvestmentClicks { get; }

        /// <summary>Total de cliques de investimento por mês.</summary>
        public int MaxInvestmentClicks { get; }

        /// <summary>Verdadeiro se um clique de investimento em Tecnologia seria aceito agora.</summary>
        public bool CanInvestTechnology { get; }

        /// <summary>Verdadeiro se um clique de investimento em Tropas seria aceito agora.</summary>
        public bool CanInvestTroops { get; }

        /// <summary>Pontos de Economia/Popularidade gastos por clique de investimento.</summary>
        public int InvestmentCost { get; }

        public WarOutcome Outcome { get; }

        /// <summary>Pontos de Tecnologia/Tropas que equivalem a 1 nível.</summary>
        public const int LevelStep = 25;

        public WarSnapshot(
            bool isActive,
            WarLevel level,
            string targetId,
            int progress,
            bool canSurrender,
            bool hasInvestedThisMonth,
            bool canInvestTechnology,
            bool canInvestTroops,
            WarOutcome outcome,
            int technology = 0,
            int troops = 0,
            int remainingInvestmentClicks = 0,
            int maxInvestmentClicks = 3,
            int investmentCost = 5)
        {
            IsActive = isActive;
            Level = level;
            TargetId = targetId ?? string.Empty;
            Progress = progress < 0 ? 0 : (progress > 100 ? 100 : progress);
            Technology = technology < 0 ? 0 : (technology > 100 ? 100 : technology);
            Troops = troops < 0 ? 0 : (troops > 100 ? 100 : troops);
            CanSurrender = canSurrender;
            HasInvestedThisMonth = hasInvestedThisMonth;
            CanInvestTechnology = canInvestTechnology;
            CanInvestTroops = canInvestTroops;
            Outcome = outcome;
            RemainingInvestmentClicks = remainingInvestmentClicks < 0 ? 0 : remainingInvestmentClicks;
            MaxInvestmentClicks = maxInvestmentClicks < 1 ? 1 : maxInvestmentClicks;
            InvestmentCost = investmentCost < 0 ? 0 : investmentCost;
        }

        public static WarSnapshot Inactive => new WarSnapshot(false, WarLevel.None, string.Empty, 50, false, false, false, false, WarOutcome.None, 0, 0, 0);
    }
}

