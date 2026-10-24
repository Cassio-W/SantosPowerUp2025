using System;
using System.Collections.Generic;
using Mandato.Core;
using UnityEngine;

namespace Mandato.Content
{
    /// <summary>
    /// Parâmetros de um nível de dificuldade da guerra. A drenagem mensal de Tecnologia/Tropas não é
    /// configurável: perde-se 1/2/3 níveis (25/50/75 pontos) nos níveis de guerra 1/2/3.
    /// </summary>
    [Serializable]
    public class WarLevelConfig
    {
        public WarLevel level = WarLevel.Neighbor;
        public string displayName = string.Empty;

        [Header("Pressão do inimigo")]
        [Tooltip("Pontos de progresso perdidos todo mês de guerra, independentemente do upkeep.")]
        public int enemyPressurePerMonth = 0;

        public WarLevelConfig() { }

        public WarLevelConfig(WarLevel level, string displayName, int pressure)
        {
            this.level = level;
            this.displayName = displayName;
            enemyPressurePerMonth = pressure;
        }
    }

    /// <summary>
    /// Balanceamento completo do Evento de Guerra. Classe de dados pura: usada tanto pelo
    /// <see cref="WarEventDefinition"/> (asset do editor) quanto pelos testes (via <see cref="CreateDefault"/>).
    /// </summary>
    [Serializable]
    public class WarBalance
    {
        [Header("Cabo de guerra")]
        public int startingProgress = 50;

        [Header("Recompensa (ofensiva)")]
        public int offensiveBonusPercent = 10;
        [Tooltip("Tecnologia E Tropas precisam estar ACIMA deste valor (em pontos, 25 = 1 nível) após a drenagem do mês.")]
        public int offensiveThreshold = 25;

        [Header("Falha de manutenção")]
        [Tooltip("Progresso perdido por atributo oculto zerado (Tecnologia e/ou Tropas).")]
        public int maintenanceFailurePenalty = 8;
        [Range(0f, 1f)] public float climateHitChance = 0.5f;
        public int climateHitAmount = 10;

        [Header("Investimento mensal (Economia→Tecnologia, Popularidade→Tropas)")]
        [Tooltip("Máximo de cliques de investimento permitidos por mês (distribuídos livremente entre Tecnologia e Tropas).")]
        public int maxInvestmentClicksPerMonth = 3;
        [Tooltip("Pontos de Economia/Popularidade gastos por clique (cada clique sobe 1 nível = 25 pontos de Tecnologia/Tropas).")]
        public int investmentPointsPerClick = 5;
        [Tooltip("O investimento nunca deixa Economia/Popularidade abaixo deste piso (evita auto-derrota acidental).")]
        public int investmentStatFloor = 10;

        [Header("Rendição")]
        [Tooltip("Só é possível se o progresso estiver ABAIXO deste valor.")]
        public int surrenderProgressBelow = 30;
        [Tooltip("Popularidade e Soberania precisam estar ACIMA deste valor.")]
        public int surrenderMinStat = 50;

        [Header("Vitória total")]
        [Tooltip("Aumento percentual aplicado à Popularidade (30 = ×1,3).")]
        public int victoryPopularityBonusPercent = 30;

        public List<WarLevelConfig> levels = new List<WarLevelConfig>();

        public WarLevelConfig GetLevel(WarLevel level)
        {
            if (levels != null)
            {
                for (int i = 0; i < levels.Count; i++)
                {
                    if (levels[i] != null && levels[i].level == level) return levels[i];
                }
            }
            return null;
        }

        public static WarBalance CreateDefault()
        {
            var b = new WarBalance();
            b.maxInvestmentClicksPerMonth = 3;
            b.investmentPointsPerClick = 5;
            b.investmentStatFloor = 10;
            b.levels.Add(new WarLevelConfig(WarLevel.Neighbor, "Médio", 0));
            b.levels.Add(new WarLevelConfig(WarLevel.Dominant, "Difícil", 0));
            b.levels.Add(new WarLevelConfig(WarLevel.WorldDomination, "Impossível", 0));
            return b;
        }

        /// <summary>Valida o balanceamento. Retorna true se não houver erros.</summary>
        public bool Validate(out List<string> errors)
        {
            errors = new List<string>();

            foreach (var level in new[] { WarLevel.Neighbor, WarLevel.Dominant, WarLevel.WorldDomination })
            {
                if (GetLevel(level) == null)
                    errors.Add($"Nível {level} ausente em WarBalance.levels.");
            }

            if (startingProgress <= 0 || startingProgress >= 100)
                errors.Add("startingProgress deve estar entre 1 e 99.");
            if (investmentPointsPerClick < 0)
                errors.Add("investmentPointsPerClick não pode ser negativo.");

            return errors.Count == 0;
        }
    }

    /// <summary>
    /// Configuração estática do Evento de Guerra (conteúdo imutável em runtime).
    /// Opcional: sem este asset, o jogo usa <see cref="WarBalance.CreateDefault"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "WarEventDefinition", menuName = "Mandato/War Event Definition")]
    public class WarEventDefinition : ScriptableObject
    {
        public WarBalance balance = WarBalance.CreateDefault();
    }
}

