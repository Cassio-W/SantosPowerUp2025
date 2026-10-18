using System;
using Mandato.Content;
using Mandato.Core;

namespace Mandato.Run
{
    /// <summary>
    /// Estado mutável do Evento de Guerra na run. Tecnologia e Tropas são atributos OCULTOS:
    /// existem aqui, mas nunca são copiados para o <see cref="WarSnapshot"/> exibido na UI.
    /// </summary>
    [Serializable]
    public class WarState
    {
        public bool isActive = false;
        public WarLevel level = WarLevel.None;
        public string targetId = string.Empty;

        /// <summary>Cabo de guerra 0..100. 0 = derrota, 100 = vitória total.</summary>
        public int progress = 50;

        public int technology = 0;
        public int troops = 0;
        public int monthsInWar = 0;
        public bool investedThisMonth = false;
        public int economyInvestedThisMonth = 0;
        public int popularityInvestedThisMonth = 0;

        /// <summary>None enquanto a guerra não existe ou está em andamento.</summary>
        public WarOutcome outcome = WarOutcome.None;

        /// <summary>Verdadeiro se a guerra está em andamento OU já foi concluída (só ocorre uma por run).</summary>
        public bool HasStarted => isActive || outcome != WarOutcome.None;

        public WarState Clone()
        {
            return new WarState
            {
                isActive = isActive,
                level = level,
                targetId = targetId ?? string.Empty,
                progress = progress,
                technology = technology,
                troops = troops,
                monthsInWar = monthsInWar,
                investedThisMonth = investedThisMonth,
                economyInvestedThisMonth = economyInvestedThisMonth,
                popularityInvestedThisMonth = popularityInvestedThisMonth,
                outcome = outcome
            };
        }
    }

    /// <summary>Resultado da tentativa de investimento militar do mês.</summary>
    [Serializable]
    public class WarInvestmentResult
    {
        public bool success;
        public string failReason = string.Empty;
        public int economySpent;
        public int popularitySpent;
    }

    /// <summary>Relatório da rodada de fim de mês da guerra (consumido por UI/Presentation).</summary>
    [Serializable]
    public class WarMonthReport
    {
        public WarLevel level;
        public int progressBefore;
        public int progressAfter;

        public int troopsDrained;
        public int technologyDrained;     // 0 se não houve drenagem de tecnologia neste mês
        public bool technologyDrainApplied;

        public bool maintenanceFailed;
        public int maintenancePenalty;
        public bool climateHit;

        public bool offensiveBonusApplied;
        public int offensiveBonus;

        public int enemyPressure;

        public WarOutcome outcome;        // != None quando a guerra terminou neste mês
    }

    /// <summary>
    /// Regras puras e determinísticas do Evento de Guerra (sem Unity, sem cenas).
    /// </summary>
    public static class WarRules
    {
        public const string DefeatReason = "Guerra Perdida: o país foi devastado pelo conflito.";
        public const string ProtectorateReason = "Mandato Encerrado sob Protetorado: o país se rendeu na guerra e virou uma colônia.";

        private static WarBalance BalanceOf(RunState run) => run.warBalance ?? WarBalance.CreateDefault();

        /// <summary>
        /// Declara a guerra. Falha (retorna false) se já houve guerra nesta run, se a run terminou
        /// ou se o nível for None. Tecnologia e Tropas iniciais = metade da Soberania
        /// (Soberania = Relações Internacionais) no momento da declaração.
        /// </summary>
        public static bool StartWar(RunState run, WarLevel level, string targetId = "")
        {
            if (run == null || level == WarLevel.None) return false;
            if (!run.termination.IsOngoing) return false;
            if (run.war != null && run.war.HasStarted) return false;

            var balance = BalanceOf(run);
            int initial = run.stats.internationalRelations / 2;

            run.war = new WarState
            {
                isActive = true,
                level = level,
                targetId = targetId ?? string.Empty,
                progress = balance.startingProgress,
                technology = StatBlock.Clamp(initial),
                troops = StatBlock.Clamp(initial),
                monthsInWar = 0,
                investedThisMonth = false,
                economyInvestedThisMonth = 0,
                popularityInvestedThisMonth = 0,
                outcome = WarOutcome.None
            };
            return true;
        }

        public static int GetMaxInvestment(RunState run, StatId stat)
        {
            if (run == null || run.war == null || !run.war.isActive) return 0;
            if (stat != StatId.Economy && stat != StatId.PopularApproval) return 0;

            var balance = BalanceOf(run);
            int alreadyInvested = stat == StatId.Economy
                ? run.war.economyInvestedThisMonth
                : run.war.popularityInvestedThisMonth;

            if (run.war.investedThisMonth && run.war.economyInvestedThisMonth == 0 && run.war.popularityInvestedThisMonth == 0)
            {
                return 0;
            }

            int remainingCap = Math.Max(0, balance.investmentCapPerStatPerMonth - alreadyInvested);
            int available = Math.Max(0, run.stats.Get(stat) - balance.investmentStatFloor);
            return Math.Min(remainingCap, available);
        }

        /// <summary>
        /// Investimento mensal: converte Economia → Tecnologia e Popularidade → Tropas (1:1).
        /// Os valores pedidos são limitados pelo teto mensal de cada atributo e pelo piso de segurança.
        /// A despesa NÃO é bloqueada por "preventStatLossThisMonth" (é um gasto voluntário, não uma perda).
        /// </summary>
        public static WarInvestmentResult Invest(RunState run, int economyAmount, int popularityAmount)
        {
            var result = new WarInvestmentResult();

            if (run == null || run.war == null || !run.war.isActive || !run.termination.IsOngoing)
            {
                result.failReason = "Nenhuma guerra em andamento.";
                return result;
            }

            int econ = Math.Min(Math.Max(0, economyAmount), GetMaxInvestment(run, StatId.Economy));
            int pop = Math.Min(Math.Max(0, popularityAmount), GetMaxInvestment(run, StatId.PopularApproval));

            if (econ <= 0 && pop <= 0)
            {
                result.failReason = "Nada a investir (limite mensal atingido ou atributos no piso de segurança).";
                return result;
            }

            run.stats.ApplyDelta(StatId.Economy, -econ);
            run.stats.ApplyDelta(StatId.PopularApproval, -pop);
            run.war.technology = StatBlock.Clamp(run.war.technology + econ);
            run.war.troops = StatBlock.Clamp(run.war.troops + pop);
            run.war.economyInvestedThisMonth += econ;
            run.war.popularityInvestedThisMonth += pop;
            run.war.investedThisMonth = GetMaxInvestment(run, StatId.Economy) <= 0 &&
                                        GetMaxInvestment(run, StatId.PopularApproval) <= 0;

            run.UpdateTermination();

            result.success = true;
            result.economySpent = econ;
            result.popularitySpent = pop;
            return result;
        }

        /// <summary>
        /// Rodada de fim de mês. Ordem estrita:
        /// 1. drenagem de Tropas (todo mês) e de Tecnologia (a cada N meses);
        /// 2. falha de manutenção (algum atributo oculto zerado): perde progresso + chance de -10 no Meio Ambiente;
        /// 3. senão, se ambos > limiar: ofensiva (+10%);
        /// 4. pressão base do inimigo (por nível);
        /// 5. desfecho (0 = derrota; 100 = vitória total).
        /// O RNG padrão é derivado da seed da run, do mês e do tempo de guerra (determinístico e salvável).
        /// Retorna null se não houver guerra ativa.
        /// </summary>
        public static WarMonthReport ResolveMonthEnd(RunState run, Random rng = null)
        {
            if (run == null || run.war == null || !run.war.isActive || !run.termination.IsOngoing) return null;

            var balance = BalanceOf(run);
            var war = run.war;
            var cfg = balance.GetLevel(war.level);
            if (cfg == null)
            {
                throw new InvalidOperationException($"WarBalance sem configuração para o nível {war.level}.");
            }

            war.monthsInWar++;
            rng ??= new Random(unchecked(run.seed * 31 + war.monthsInWar * 7919 + 17));

            var report = new WarMonthReport
            {
                level = war.level,
                progressBefore = war.progress,
                enemyPressure = cfg.enemyPressurePerMonth
            };

            // 1. Drenagem
            report.troopsDrained = NextInclusive(rng, cfg.troopsDrainMin, cfg.troopsDrainMax);
            war.troops = StatBlock.Clamp(war.troops - report.troopsDrained);

            if (war.monthsInWar % Math.Max(1, balance.technologyDrainIntervalMonths) == 0)
            {
                report.technologyDrainApplied = true;
                report.technologyDrained = NextInclusive(rng, cfg.technologyDrainMin, cfg.technologyDrainMax);
                war.technology = StatBlock.Clamp(war.technology - report.technologyDrained);
            }

            // 2/3. Falha de manutenção x ofensiva (mutuamente exclusivas)
            int progress = war.progress;
            bool technologyZero = war.technology <= 0;
            bool troopsZero = war.troops <= 0;

            if (technologyZero || troopsZero)
            {
                int failures = (technologyZero ? 1 : 0) + (troopsZero ? 1 : 0);
                report.maintenanceFailed = true;
                report.maintenancePenalty = balance.maintenanceFailurePenalty * failures;
                progress -= report.maintenancePenalty;

                if (rng.NextDouble() < balance.climateHitChance)
                {
                    report.climateHit = true;
                    run.ApplyStatDelta(StatId.ClimaticChanges, -balance.climateHitAmount);
                }
            }
            else if (war.technology > balance.offensiveThreshold && war.troops > balance.offensiveThreshold)
            {
                report.offensiveBonusApplied = true;
                report.offensiveBonus = balance.offensiveBonusPercent;
                progress += report.offensiveBonus;
            }

            // 4. Pressão do inimigo
            progress -= cfg.enemyPressurePerMonth;
            war.progress = Math.Max(0, Math.Min(100, progress));
            war.investedThisMonth = false;
            war.economyInvestedThisMonth = 0;
            war.popularityInvestedThisMonth = 0;

            // 5. Desfecho
            if (run.termination.IsOngoing)
            {
                if (war.progress <= 0)
                {
                    war.outcome = WarOutcome.Defeat;
                    war.isActive = false;
                    run.ForceDefeat(DefeatReason);
                }
                else if (war.progress >= 100)
                {
                    ConcludeWithVictory(run);
                }
            }

            report.progressAfter = war.progress;
            report.outcome = war.outcome;
            return report;
        }

        /// <summary>
        /// Vitória total: Economia vai ao máximo, Popularidade sobe 30% (×1,3) e os demais atributos ficam iguais.
        /// A run continua normalmente até o fim do mandato.
        /// </summary>
        private static void ConcludeWithVictory(RunState run)
        {
            var balance = BalanceOf(run);
            run.war.outcome = WarOutcome.Victory;
            run.war.isActive = false;

            run.stats.economy = StatBlock.MaxValue;
            double boosted = run.stats.popularApproval * (100 + balance.victoryPopularityBonusPercent) / 100.0;
            run.stats.popularApproval = StatBlock.Clamp((int)Math.Round(boosted, MidpointRounding.AwayFromZero));

            run.UpdateTermination();
        }

        /// <summary>
        /// Rendição permitida apenas com progresso abaixo de 30% e Popularidade e Soberania acima de 50.
        /// </summary>
        public static bool CanSurrender(RunState run)
        {
            if (run == null || run.war == null || !run.war.isActive || !run.termination.IsOngoing) return false;

            var balance = BalanceOf(run);
            return run.war.progress < balance.surrenderProgressBelow
                   && run.stats.popularApproval > balance.surrenderMinStat
                   && run.stats.internationalRelations > balance.surrenderMinStat;
        }

        /// <summary>
        /// O país vira colônia/protetorado. A run continua até o fim do mandato, que fecha com final ruim
        /// (ver <see cref="RunRules.Evaluate"/>), permitindo concluir questlines pendentes.
        /// </summary>
        public static bool Surrender(RunState run)
        {
            if (!CanSurrender(run)) return false;

            run.war.outcome = WarOutcome.Surrendered;
            run.war.isActive = false;
            run.UpdateTermination();
            return true;
        }

        public static WarSnapshot CreateSnapshot(RunState run)
        {
            if (run == null || run.war == null) return WarSnapshot.Inactive;

            var war = run.war;
            if (!war.isActive)
            {
                return new WarSnapshot(false, war.level, war.targetId, war.progress, false, false, 0, 0, war.outcome, war.technology, war.troops);
            }

            return new WarSnapshot(
                true,
                war.level,
                war.targetId,
                war.progress,
                CanSurrender(run),
                war.investedThisMonth,
                GetMaxInvestment(run, StatId.Economy),
                GetMaxInvestment(run, StatId.PopularApproval),
                war.outcome,
                war.technology,
                war.troops);
        }

        private static int NextInclusive(Random rng, int min, int max)
        {
            if (max < min) max = min;
            return rng.Next(min, max + 1);
        }
    }
}

