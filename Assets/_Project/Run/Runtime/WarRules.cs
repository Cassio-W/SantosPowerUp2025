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
        public int investmentClicksThisMonth = 0;

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
                investmentClicksThisMonth = investmentClicksThisMonth,
                outcome = outcome
            };
        }
    }

    /// <summary>Resultado da tentativa de investimento militar do mês.</summary>
    [Serializable]
    public class WarInvestmentResult
    {
        public bool success;
        public WarAttribute attribute;
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

        /// <summary>Pontos de Tropas efetivamente perdidos no mês (múltiplo de 25).</summary>
        public int troopsDrained;

        /// <summary>Pontos de Tecnologia efetivamente perdidos no mês (múltiplo de 25).</summary>
        public int technologyDrained;

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

        /// <summary>Pontos de Tecnologia/Tropas que equivalem a 1 nível (alterações são sempre em múltiplos disso).</summary>
        public const int AttributeStep = WarSnapshot.LevelStep;

        private static int RoundToLevel(int points)
        {
            return StatBlock.Clamp((int)(Math.Round(points / (double)AttributeStep, MidpointRounding.AwayFromZero) * AttributeStep));
        }

        private static WarBalance BalanceOf(RunState run) => run.warBalance ?? WarBalance.CreateDefault();

        /// <summary>
        /// Declara a guerra. Falha (retorna false) se já houve guerra nesta run, se a run terminou
        /// ou se o nível for None. Tecnologia e Tropas iniciais = Soberania
        /// (Soberania = Relações Internacionais) no momento da declaração, arredondada para o nível
        /// mais próximo (múltiplos de 25).
        /// </summary>
        public static bool StartWar(RunState run, WarLevel level, string targetId = "")
        {
            if (run == null || level == WarLevel.None) return false;
            if (!run.termination.IsOngoing) return false;
            if (run.war != null && run.war.HasStarted) return false;

            var balance = BalanceOf(run);
            int initial = RoundToLevel(run.stats.internationalRelations);

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
                investmentClicksThisMonth = 0,
                outcome = WarOutcome.None
            };
            return true;
        }

        public static int GetRemainingInvestmentClicks(RunState run)
        {
            if (run == null || run.war == null || !run.war.isActive) return 0;
            var balance = BalanceOf(run);
            int maxClicks = balance.maxInvestmentClicksPerMonth > 0 ? balance.maxInvestmentClicksPerMonth : 3;
            return Math.Max(0, maxClicks - run.war.investmentClicksThisMonth);
        }

        /// <summary>
        /// Verdadeiro se um clique de investimento no atributo seria aceito agora: há guerra ativa,
        /// sobram cliques no mês, o atributo militar não está no nível máximo e o atributo de origem
        /// (Economia para Tecnologia, Popularidade para Tropas) continua acima do piso depois do gasto.
        /// </summary>
        public static bool CanInvest(RunState run, WarAttribute attribute)
        {
            if (run == null || run.war == null || !run.war.isActive || !run.termination.IsOngoing) return false;
            if (GetRemainingInvestmentClicks(run) <= 0) return false;

            var balance = BalanceOf(run);
            int current = attribute == WarAttribute.Technology ? run.war.technology : run.war.troops;
            if (current >= StatBlock.MaxValue) return false;

            StatId source = attribute == WarAttribute.Technology ? StatId.Economy : StatId.PopularApproval;
            return run.stats.Get(source) - balance.investmentPointsPerClick >= balance.investmentStatFloor;
        }

        /// <summary>
        /// Investimento mensal: 1 clique sobe 1 nível (+25) de Tecnologia ou Tropas e gasta
        /// <see cref="WarBalance.investmentPointsPerClick"/> (5) de Economia ou Popularidade, respectivamente.
        /// São permitidos até 3 cliques por mês, distribuídos livremente entre Tecnologia e Tropas.
        /// O gasto respeita o piso de segurança (10) e NÃO é bloqueado por "preventStatLossThisMonth"
        /// (é um gasto voluntário, não uma perda).
        /// </summary>
        public static WarInvestmentResult Invest(RunState run, WarAttribute attribute)
        {
            var result = new WarInvestmentResult { attribute = attribute };

            if (run == null || run.war == null || !run.war.isActive || !run.termination.IsOngoing)
            {
                result.failReason = "Nenhuma guerra em andamento.";
                return result;
            }

            var balance = BalanceOf(run);
            if (GetRemainingInvestmentClicks(run) <= 0)
            {
                result.failReason = $"Limite de {balance.maxInvestmentClicksPerMonth} investimentos mensais atingido.";
                return result;
            }

            bool tech = attribute == WarAttribute.Technology;
            if ((tech ? run.war.technology : run.war.troops) >= StatBlock.MaxValue)
            {
                result.failReason = tech ? "Tecnologia já está no nível máximo." : "Tropas já estão no nível máximo.";
                return result;
            }

            if (!CanInvest(run, attribute))
            {
                result.failReason = "Atributo de origem no piso de segurança.";
                return result;
            }

            int cost = balance.investmentPointsPerClick;
            if (tech)
            {
                run.stats.ApplyDelta(StatId.Economy, -cost);
                run.war.technology = StatBlock.Clamp(run.war.technology + AttributeStep);
                run.war.economyInvestedThisMonth += cost;
                result.economySpent = cost;
            }
            else
            {
                run.stats.ApplyDelta(StatId.PopularApproval, -cost);
                run.war.troops = StatBlock.Clamp(run.war.troops + AttributeStep);
                run.war.popularityInvestedThisMonth += cost;
                result.popularitySpent = cost;
            }

            run.war.investmentClicksThisMonth++;
            run.war.investedThisMonth = GetRemainingInvestmentClicks(run) <= 0 ||
                                        (!CanInvest(run, WarAttribute.Technology) && !CanInvest(run, WarAttribute.Troops));

            run.UpdateTermination();

            result.success = true;
            return result;
        }
        /// <summary>
        /// Rodada de fim de mês. Ordem estrita:
        /// 1. drenagem de N níveis (N x 25 pontos, N = nível da guerra), sorteados entre Tecnologia e Tropas;
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
                enemyPressure = 0
            };

            // 1. Drenagem aleatória por nível:
            //    - Nível 1: 0% a 50% (0 a 2 passos de 25%)
            //    - Nível 2: 25% a 75% (1 a 3 passos de 25%)
            //    - Nível 3: 50% a 100% (2 a 4 passos de 25%)
            int minSteps = Math.Max(0, (int)war.level - 1);
            int maxSteps = Math.Min(4, (int)war.level + 1);
            int drainSteps = rng.Next(minSteps, maxSteps + 1);

            for (int i = 0; i < drainSteps; i++)
            {
                if (war.technology > 0 && war.troops > 0)
                {
                    bool hitTechnology = rng.Next(0, 2) == 0;
                    if (hitTechnology)
                    {
                        war.technology = Math.Max(0, war.technology - AttributeStep);
                        report.technologyDrained += AttributeStep;
                    }
                    else
                    {
                        war.troops = Math.Max(0, war.troops - AttributeStep);
                        report.troopsDrained += AttributeStep;
                    }
                }
                else if (war.technology > 0)
                {
                    war.technology = Math.Max(0, war.technology - AttributeStep);
                    report.technologyDrained += AttributeStep;
                }
                else if (war.troops > 0)
                {
                    war.troops = Math.Max(0, war.troops - AttributeStep);
                    report.troopsDrained += AttributeStep;
                }
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

            // 4. Sem dano passivo fixo (removido por balanceamento)
            war.progress = Math.Max(0, Math.Min(100, progress));
            war.investedThisMonth = false;
            war.economyInvestedThisMonth = 0;
            war.popularityInvestedThisMonth = 0;
            war.investmentClicksThisMonth = 0;

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
            var balance = BalanceOf(run);
            int maxClicks = balance.maxInvestmentClicksPerMonth > 0 ? balance.maxInvestmentClicksPerMonth : 3;

            if (!war.isActive)
            {
                return new WarSnapshot(false, war.level, war.targetId, war.progress, false, false, false, false, war.outcome, war.technology, war.troops, 0, maxClicks, balance.investmentPointsPerClick);
            }

            return new WarSnapshot(
                true,
                war.level,
                war.targetId,
                war.progress,
                CanSurrender(run),
                war.investedThisMonth,
                CanInvest(run, WarAttribute.Technology),
                CanInvest(run, WarAttribute.Troops),
                war.outcome,
                war.technology,
                war.troops,
                GetRemainingInvestmentClicks(run),
                maxClicks,
                balance.investmentPointsPerClick);
        }
    }
}