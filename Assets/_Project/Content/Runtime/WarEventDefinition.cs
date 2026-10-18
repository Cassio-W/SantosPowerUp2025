using System;
using System.Collections.Generic;
using Mandato.Core;
using UnityEngine;

namespace Mandato.Content
{
    /// <summary>Parâmetros de um nível de dificuldade da guerra.</summary>
    [Serializable]
    public class WarLevelConfig
    {
        public WarLevel level = WarLevel.Neighbor;
        public string displayName = string.Empty;

        [Header("Drenagem de Tropas (todo mês)")]
        public int troopsDrainMin = 1;
        public int troopsDrainMax = 3;

        [Header("Drenagem de Tecnologia (a cada N meses)")]
        public int technologyDrainMin = 3;
        public int technologyDrainMax = 9;

        [Header("Pressão do inimigo")]
        [Tooltip("Pontos de progresso perdidos todo mês de guerra, independentemente do upkeep.")]
        public int enemyPressurePerMonth = 0;

        public WarLevelConfig() { }

        public WarLevelConfig(WarLevel level, string displayName, int troopsMin, int troopsMax, int techMin, int techMax, int pressure)
        {
            this.level = level;
            this.displayName = displayName;
            troopsDrainMin = troopsMin;
            troopsDrainMax = troopsMax;
            technologyDrainMin = techMin;
            technologyDrainMax = techMax;
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

        [Header("Upkeep mensal")]
        [Tooltip("Tecnologia só sofre drenagem a cada N meses de guerra.")]
        public int technologyDrainIntervalMonths = 3;

        [Header("Recompensa (ofensiva)")]
        public int offensiveBonusPercent = 10;
        [Tooltip("Tecnologia E Tropas precisam estar ACIMA deste valor após a drenagem do mês.")]
        public int offensiveThreshold = 10;

        [Header("Falha de manutenção")]
        [Tooltip("Progresso perdido por atributo oculto zerado (Tecnologia e/ou Tropas).")]
        public int maintenanceFailurePenalty = 8;
        [Range(0f, 1f)] public float climateHitChance = 0.5f;
        public int climateHitAmount = 10;

        [Header("Investimento mensal (Economia→Tecnologia, Popularidade→Tropas)")]
        [Tooltip("Máximo de pontos de cada atributo convertido por mês (conversão 1:1).")]
        public int investmentCapPerStatPerMonth = 5;
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
            b.levels.Add(new WarLevelConfig(WarLevel.Neighbor, "Médio", 1, 3, 3, 9, 0));
            b.levels.Add(new WarLevelConfig(WarLevel.Dominant, "Difícil", 3, 5, 10, 15, 2));
            b.levels.Add(new WarLevelConfig(WarLevel.WorldDomination, "Impossível", 5, 10, 15, 20, 4));
            return b;
        }

        /// <summary>Valida o balanceamento. Retorna true se não houver erros.</summary>
        public bool Validate(out List<string> errors)
        {
            errors = new List<string>();

            foreach (var level in new[] { WarLevel.Neighbor, WarLevel.Dominant, WarLevel.WorldDomination })
            {
                var cfg = GetLevel(level);
                if (cfg == null)
                {
                    errors.Add($"Nível {level} ausente em WarBalance.levels.");
                    continue;
                }
                if (cfg.troopsDrainMin < 0 || cfg.troopsDrainMin > cfg.troopsDrainMax)
                    errors.Add($"Nível {level}: drenagem de Tropas inválida (min {cfg.troopsDrainMin}, max {cfg.troopsDrainMax}).");
                if (cfg.technologyDrainMin < 0 || cfg.technologyDrainMin > cfg.technologyDrainMax)
                    errors.Add($"Nível {level}: drenagem de Tecnologia inválida (min {cfg.technologyDrainMin}, max {cfg.technologyDrainMax}).");
            }

            if (technologyDrainIntervalMonths < 1)
                errors.Add("technologyDrainIntervalMonths deve ser >= 1.");
            if (startingProgress <= 0 || startingProgress >= 100)
                errors.Add("startingProgress deve estar entre 1 e 99.");

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

