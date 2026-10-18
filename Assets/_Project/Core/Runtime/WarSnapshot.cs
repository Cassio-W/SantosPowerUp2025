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

        /// <summary>Nível atual de Tecnologia militar (0..100).</summary>
        public int Technology { get; }

        /// <summary>Nível atual de Tropas (0..100).</summary>
        public int Troops { get; }

        public bool CanSurrender { get; }
        public bool HasInvestedThisMonth { get; }

        /// <summary>Máximo de Economia que o jogador ainda pode converter em Tecnologia neste mês.</summary>
        public int MaxEconomyInvestment { get; }

        /// <summary>Máximo de Popularidade que o jogador ainda pode converter em Tropas neste mês.</summary>
        public int MaxPopularityInvestment { get; }

        public WarOutcome Outcome { get; }

        public WarSnapshot(
            bool isActive,
            WarLevel level,
            string targetId,
            int progress,
            bool canSurrender,
            bool hasInvestedThisMonth,
            int maxEconomyInvestment,
            int maxPopularityInvestment,
            WarOutcome outcome,
            int technology = 0,
            int troops = 0)
        {
            IsActive = isActive;
            Level = level;
            TargetId = targetId ?? string.Empty;
            Progress = progress < 0 ? 0 : (progress > 100 ? 100 : progress);
            Technology = technology < 0 ? 0 : (technology > 100 ? 100 : technology);
            Troops = troops < 0 ? 0 : (troops > 100 ? 100 : troops);
            CanSurrender = canSurrender;
            HasInvestedThisMonth = hasInvestedThisMonth;
            MaxEconomyInvestment = maxEconomyInvestment < 0 ? 0 : maxEconomyInvestment;
            MaxPopularityInvestment = maxPopularityInvestment < 0 ? 0 : maxPopularityInvestment;
            Outcome = outcome;
        }

        public static WarSnapshot Inactive => new WarSnapshot(false, WarLevel.None, string.Empty, 50, false, false, 0, 0, WarOutcome.None, 0, 0);
    }
}

