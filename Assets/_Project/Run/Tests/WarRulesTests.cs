using System;
using System.Collections.Generic;
using Mandato.Content;
using Mandato.Core;
using NUnit.Framework;
using UnityEngine;

namespace Mandato.Run.Tests
{
    public class WarRulesTests
    {
        /// <summary>RNG controlável: devolve sempre o mínimo (ou o máximo) do intervalo e um NextDouble fixo.</summary>
        private class ScriptedRandom : System.Random
        {
            public bool useMax;
            public double doubleValue;

            public override int Next(int minValue, int maxValue) => useMax ? maxValue - 1 : minValue;
            public override double NextDouble() => doubleValue;
        }

        private static ScriptedRandom MinRng(double d = 0.99) => new ScriptedRandom { useMax = false, doubleValue = d };
        private static ScriptedRandom MaxRng(double d = 0.99) => new ScriptedRandom { useMax = true, doubleValue = d };

        /// <summary>RNG que devolve uma sequência fixa de sorteios (0 = Tecnologia, 1 = Tropas na drenagem mensal).</summary>
        private class SequencedRandom : System.Random
        {
            private readonly int[] picks;
            private int index;

            public SequencedRandom(params int[] picks) { this.picks = picks; }

            public override int Next(int minValue, int maxValue) => picks[index++ % picks.Length];
            public override double NextDouble() => 0.99;
        }

        private static void MaxOutMilitary(RunState run)
        {
            run.war.technology = 100;
            run.war.troops = 100;
        }

        private static RunState NewRun(int seed = 1) => new RunState(seed);

        private static RunState NewWar(WarLevel level = WarLevel.Neighbor, int relations = 50)
        {
            var run = NewRun();
            run.stats.internationalRelations = relations;
            Assert.IsTrue(WarRules.StartWar(run, level, "Vizinholândia"));
            return run;
        }

        // ---------------------------------------------------------------- Início

        [Test]
        public void StartWar_InitializesBarAtFiftyAndHiddenStatsAsSovereignty()
        {
            var run = NewRun();
            run.stats.internationalRelations = 51;

            bool started = WarRules.StartWar(run, WarLevel.Dominant, "Nação");

            Assert.IsTrue(started);
            Assert.IsTrue(run.war.isActive);
            Assert.AreEqual(WarLevel.Dominant, run.war.level);
            Assert.AreEqual("Nação", run.war.targetId);
            Assert.AreEqual(50, run.war.progress);
            Assert.AreEqual(50, run.war.technology); // 51 arredonda para 50 (múltiplo de 25 mais próximo)
            Assert.AreEqual(50, run.war.troops);
        }

        [Test]
        public void StartWar_OnlyOncePerRun_EvenAfterItEnded()
        {
            var run = NewWar();
            Assert.IsFalse(WarRules.StartWar(run, WarLevel.Neighbor, "Outro"));

            run.war.isActive = false;
            run.war.outcome = WarOutcome.Victory;
            Assert.IsFalse(WarRules.StartWar(run, WarLevel.Neighbor, "Outro"));
        }

        [Test]
        public void StartWar_NoneLevelOrFinishedRun_IsRejected()
        {
            var run = NewRun();
            Assert.IsFalse(WarRules.StartWar(run, WarLevel.None));

            run.ForceDefeat("fim");
            Assert.IsFalse(WarRules.StartWar(run, WarLevel.Neighbor));
        }

        [Test]
        public void Reset_ClearsWar()
        {
            var run = NewWar();
            run.Reset();
            Assert.IsFalse(run.war.HasStarted);
        }

        // ---------------------------------------------------------------- Drenagem

        [Test]
        public void StartWar_RoundsHiddenStatsToTheNearestLevel()
        {
            var low = NewWar(WarLevel.Neighbor, 10);   // 10 -> 0
            Assert.AreEqual(0, low.war.technology);

            var mid = NewWar(WarLevel.Neighbor, 30);   // 30 -> 25
            Assert.AreEqual(25, mid.war.technology);

            var high = NewWar(WarLevel.Neighbor, 100); // 100 -> 100
            Assert.AreEqual(100, high.war.troops);
        }

        [Test]
        public void ResolveMonthEnd_DrainsRandomRanges_ByWarLevel()
        {
            // Nível 1: min 0 passos (0%), max 2 passos (50%)
            // Nível 2: min 1 passo (25%), max 3 passos (75%)
            // Nível 3: min 2 passos (50%), max 4 passos (100%)
            foreach (var (level, minPoints, maxPoints) in new[]
                     {
                         (WarLevel.Neighbor, 0, 50),
                         (WarLevel.Dominant, 25, 75),
                         (WarLevel.WorldDomination, 50, 100)
                     })
            {
                var runMin = NewWar(level, 100);
                var runMax = NewWar(level, 100);
                MaxOutMilitary(runMin);
                MaxOutMilitary(runMax);

                var rMin = WarRules.ResolveMonthEnd(runMin, MinRng()); // min steps
                var rMax = WarRules.ResolveMonthEnd(runMax, MaxRng()); // max steps

                Assert.AreEqual(minPoints, rMin.technologyDrained + rMin.troopsDrained, $"{level} Min");
                Assert.AreEqual(maxPoints, rMax.technologyDrained + rMax.troopsDrained, $"{level} Max");
            }
        }

        [Test]
        public void ResolveMonthEnd_DrainCanBeSplitBetweenBothAttributes()
        {
            var run = NewWar(WarLevel.Dominant, 100);
            MaxOutMilitary(run);

            // SequencedRandom: primeiro valor (2) escolhe 2 drain steps, depois 0 (tech), depois 1 (troops)
            var report = WarRules.ResolveMonthEnd(run, new SequencedRandom(2, 0, 1));

            Assert.AreEqual(25, report.technologyDrained);
            Assert.AreEqual(25, report.troopsDrained);
            Assert.AreEqual(75, run.war.technology);
            Assert.AreEqual(75, run.war.troops);
        }

        [Test]
        public void ResolveMonthEnd_DrainNeverGoesBelowZero()
        {
            var run = NewWar(WarLevel.WorldDomination, 100);
            run.war.technology = 25;
            run.war.troops = 0;

            // Nível 3 com MaxRng escolheria 4 passos, mas como só há 25 pontos, drena no máximo 25
            var report = WarRules.ResolveMonthEnd(run, MaxRng());

            Assert.AreEqual(0, run.war.technology);
            Assert.AreEqual(0, run.war.troops);
            Assert.AreEqual(25, report.technologyDrained);
        }

        // ---------------------------------------------------------------- Recompensa x falha

        [Test]
        public void ResolveMonthEnd_BothAboveThreshold_AdvancesTenPercent()
        {
            var run = NewWar(WarLevel.Neighbor, 100);
            MaxOutMilitary(run);

            var report = WarRules.ResolveMonthEnd(run, MinRng());

            Assert.IsTrue(report.offensiveBonusApplied);
            Assert.IsFalse(report.maintenanceFailed);
            Assert.AreEqual(60, run.war.progress); // 50 + 10 (sem pressão passiva)
        }

        [Test]
        public void ResolveMonthEnd_ExactlyAtThreshold_DoesNotAdvance()
        {
            var run = NewWar(WarLevel.Neighbor, 100);
            MaxOutMilitary(run);
            // Com MaxRng no Nível 1 drena 2 passos de tropas (50 pontos)
            // Se tropas começam com 75, 75 - 50 = 25, que NÃO é "acima de 25"
            run.war.troops = 75;

            var report = WarRules.ResolveMonthEnd(run, MaxRng());

            Assert.AreEqual(25, run.war.troops);
            Assert.IsFalse(report.offensiveBonusApplied);
            Assert.IsFalse(report.maintenanceFailed);
            Assert.AreEqual(50, run.war.progress);
        }

        [Test]
        public void ResolveMonthEnd_NoEnemyPressureApplied()
        {
            var run = NewWar(WarLevel.Dominant, 100);
            MaxOutMilitary(run);

            WarRules.ResolveMonthEnd(run, MinRng());

            // 50 (inicial) + 10 (ofensiva) - 0 (sem dano passivo) = 60
            Assert.AreEqual(60, run.war.progress);
        }

        [Test]
        public void ResolveMonthEnd_ZeroTroops_IsMaintenanceFailure_AndLosesProgress()
        {
            var run = NewWar(WarLevel.Neighbor, 100);
            run.war.troops = 25;

            var report = WarRules.ResolveMonthEnd(run, MaxRng(0.99));

            Assert.IsTrue(report.maintenanceFailed);
            Assert.IsFalse(report.offensiveBonusApplied);
            Assert.AreEqual(0, run.war.troops);
            Assert.AreEqual(42, run.war.progress); // 50 - 8
        }

        [Test]
        public void ResolveMonthEnd_BothZero_PenalizesEachAttribute()
        {
            var run = NewWar(WarLevel.Dominant, 100);
            run.war.troops = 25;
            run.war.technology = 25;

            // SequencedRandom: 2 drain steps, primeiro 0 (tech -> 0), segundo 1 (troops -> 0)
            WarRules.ResolveMonthEnd(run, new SequencedRandom(2, 0, 1));

            Assert.AreEqual(0, run.war.technology);
            Assert.AreEqual(0, run.war.troops);
            Assert.AreEqual(50 - 16, run.war.progress); // 2 falhas de manutenção (-16) sem dano passivo
        }

        [Test]
        public void ResolveMonthEnd_MaintenanceFailure_ClimateDropsByTen_OnlyWhenRollSucceeds()
        {
            var hit = NewWar(WarLevel.Neighbor, 100);
            hit.war.troops = 25;
            var hitReport = WarRules.ResolveMonthEnd(hit, MaxRng(0.0));

            Assert.IsTrue(hitReport.climateHit);
            Assert.AreEqual(40, hit.stats.climaticChanges);

            var miss = NewWar(WarLevel.Neighbor, 100);
            miss.war.troops = 25;
            var missReport = WarRules.ResolveMonthEnd(miss, MaxRng(0.99));

            Assert.IsFalse(missReport.climateHit);
            Assert.AreEqual(50, miss.stats.climaticChanges);
        }

        [Test]
        public void ResolveMonthEnd_WithoutMaintenanceFailure_NeverTouchesClimate()
        {
            var run = NewWar(WarLevel.Neighbor, 100);
            WarRules.ResolveMonthEnd(run, MinRng(0.0));
            Assert.AreEqual(50, run.stats.climaticChanges);
        }
        // ---------------------------------------------------------------- Desfechos

        [Test]
        public void Defeat_WhenBarReachesZero_EndsRun_AndSurvivesUpdateTermination()
        {
            var run = NewWar(WarLevel.Neighbor, 100);
            run.war.progress = 5;
            run.war.troops = 25;

            var report = WarRules.ResolveMonthEnd(run, MaxRng(0.99));

            Assert.AreEqual(WarOutcome.Defeat, report.outcome);
            Assert.IsTrue(run.termination.IsDefeat);
            Assert.IsFalse(run.war.isActive);

            run.UpdateTermination(); // não pode apagar a derrota por guerra
            Assert.IsTrue(run.termination.IsDefeat);
            Assert.AreEqual(WarRules.DefeatReason, run.termination.reason);
        }

        [Test]
        public void Victory_WhenBarReachesHundred_MaxesEconomy_AndBoostsPopularityBy30Percent()
        {
            var run = NewWar(WarLevel.Neighbor, 100);
            run.stats.economy = 40;
            run.stats.popularApproval = 60;
            run.stats.climaticChanges = 33;
            run.stats.corruption = 12;
            run.war.progress = 95;
            MaxOutMilitary(run);

            var report = WarRules.ResolveMonthEnd(run, MinRng());

            Assert.AreEqual(WarOutcome.Victory, report.outcome);
            Assert.IsFalse(run.war.isActive);
            Assert.IsTrue(run.termination.IsOngoing, "A run continua após a vitória na guerra.");
            Assert.AreEqual(100, run.stats.economy);
            Assert.AreEqual(78, run.stats.popularApproval); // 60 x 1,3
            Assert.AreEqual(33, run.stats.climaticChanges);
            Assert.AreEqual(100, run.stats.internationalRelations);
            Assert.AreEqual(12, run.stats.corruption);
        }

        [Test]
        public void Victory_PopularityIsClampedAtMax()
        {
            var run = NewWar(WarLevel.Neighbor, 100);
            run.stats.popularApproval = 90;
            run.war.progress = 95;
            MaxOutMilitary(run);

            WarRules.ResolveMonthEnd(run, MinRng());

            Assert.AreEqual(100, run.stats.popularApproval);
        }

        [Test]
        public void NeighborWar_WithHealthyMilitary_ReachesVictoryInFiveMonths()
        {
            var run = NewWar(WarLevel.Neighbor, 100);

            int months = 0;
            while (run.war.isActive && months < 20)
            {
                MaxOutMilitary(run); // simula o jogador mantendo o exército em dia
                WarRules.ResolveMonthEnd(run, MinRng());
                months++;
            }

            Assert.AreEqual(WarOutcome.Victory, run.war.outcome);
            Assert.AreEqual(5, months);
        }

        [Test]
        public void WorldDominationWar_WithoutInvestment_EndsTheRunInDefeat()
        {
            var run = NewWar(WarLevel.WorldDomination, 50);

            int months = 0;
            while (run.war.isActive && run.termination.IsOngoing && months < 40)
            {
                WarRules.ResolveMonthEnd(run, MaxRng(0.99));
                months++;
            }

            Assert.AreEqual(WarOutcome.Defeat, run.war.outcome);
            Assert.IsTrue(run.termination.IsDefeat);
        }

        // ---------------------------------------------------------------- Rendição

        [Test]
        public void CanSurrender_RequiresBarBelow30_AndPopularityAndSovereigntyAbove50()
        {
            var run = NewWar();
            run.stats.popularApproval = 60;
            run.stats.internationalRelations = 60;
            run.war.progress = 29;
            Assert.IsTrue(WarRules.CanSurrender(run));

            run.war.progress = 30;
            Assert.IsFalse(WarRules.CanSurrender(run), "30% não é 'abaixo de 30%'.");

            run.war.progress = 20;
            run.stats.popularApproval = 50;
            Assert.IsFalse(WarRules.CanSurrender(run), "Popularidade precisa estar ACIMA de 50.");

            run.stats.popularApproval = 60;
            run.stats.internationalRelations = 50;
            Assert.IsFalse(WarRules.CanSurrender(run), "Soberania precisa estar ACIMA de 50.");
        }

        [Test]
        public void CanSurrender_FalseWithoutActiveWar()
        {
            var run = NewRun();
            run.stats.popularApproval = 90;
            run.stats.internationalRelations = 90;
            Assert.IsFalse(WarRules.CanSurrender(run));
        }

        [Test]
        public void Surrender_RunContinuesUntilTermEnd_ThenFinishesAsBadEnding()
        {
            var run = NewWar();
            run.stats.popularApproval = 60;
            run.stats.internationalRelations = 60;
            run.war.progress = 10;

            Assert.IsTrue(WarRules.Surrender(run));

            Assert.AreEqual(WarOutcome.Surrendered, run.war.outcome);
            Assert.IsFalse(run.war.isActive);
            Assert.IsTrue(run.termination.IsOngoing, "A run continua até o fim do mandato.");
            Assert.IsFalse(WarRules.StartWar(run, WarLevel.Neighbor), "Não pode haver nova guerra.");

            run.calendar.currentMonthIndex = RunCalendar.DefaultTotalMonths;
            MonthlyEffectsResolver.ResolveMonth(run);

            Assert.IsTrue(run.termination.IsDefeat);
            Assert.AreEqual(WarRules.ProtectorateReason, run.termination.reason);
        }

        [Test]
        public void Surrender_RejectedWhenConditionsAreNotMet()
        {
            var run = NewWar();
            run.war.progress = 80;
            Assert.IsFalse(WarRules.Surrender(run));
            Assert.IsTrue(run.war.isActive);
        }

        // ---------------------------------------------------------------- Investimento

        [Test]
        public void Invest_RaisesOneLevel_AndSpendsFivePointsOfTheSourceStat()
        {
            var run = NewWar(WarLevel.Neighbor, 50); // tech/troops iniciam em 50, econ/pop 50

            var tech = WarRules.Invest(run, WarAttribute.Technology);
            var troops = WarRules.Invest(run, WarAttribute.Troops);

            Assert.IsTrue(tech.success);
            Assert.AreEqual(5, tech.economySpent);
            Assert.AreEqual(0, tech.popularitySpent);
            Assert.IsTrue(troops.success);
            Assert.AreEqual(5, troops.popularitySpent);
            Assert.AreEqual(45, run.stats.economy);
            Assert.AreEqual(45, run.stats.popularApproval);
            Assert.AreEqual(75, run.war.technology);
            Assert.AreEqual(75, run.war.troops);
            Assert.AreEqual(1, WarRules.GetRemainingInvestmentClicks(run), "2 cliques consumidos, resta 1.");
        }

        [Test]
        public void Invest_StopsAtMaxLevel()
        {
            var run = NewWar(WarLevel.Neighbor, 50); // tech/troops iniciam em 50
            run.war.technology = 75;

            Assert.IsTrue(WarRules.Invest(run, WarAttribute.Technology).success); // 75 -> 100
            Assert.AreEqual(100, run.war.technology);

            Assert.IsFalse(WarRules.CanInvest(run, WarAttribute.Technology));
            var capped = WarRules.Invest(run, WarAttribute.Technology);
            Assert.IsFalse(capped.success);
            Assert.AreEqual(100, run.war.technology);
            Assert.AreEqual(45, run.stats.economy, "Nível máximo não gasta Economia.");
        }

        [Test]
        public void Invest_RespectsSafetyFloor_AndThreeClicksMonthlyLimit()
        {
            var run = NewWar();
            run.stats.economy = 15;
            run.war.troops = 25; // espaço para investir em tropas

            var first = WarRules.Invest(run, WarAttribute.Technology);
            Assert.IsTrue(first.success);
            Assert.AreEqual(10, run.stats.economy);
            Assert.AreEqual(2, WarRules.GetRemainingInvestmentClicks(run));

            Assert.IsFalse(WarRules.CanInvest(run, WarAttribute.Technology), "Economia no piso.");
            Assert.IsFalse(WarRules.Invest(run, WarAttribute.Technology).success, "Economia já atingiu o piso.");
            Assert.AreEqual(10, run.stats.economy);

            Assert.IsTrue(WarRules.Invest(run, WarAttribute.Troops).success, "Primeiro clique em Tropas consome o 2º clique do mês.");
            Assert.AreEqual(1, WarRules.GetRemainingInvestmentClicks(run));

            Assert.IsTrue(WarRules.Invest(run, WarAttribute.Troops).success, "Segundo clique em Tropas consome o 3º clique do mês.");
            Assert.AreEqual(0, WarRules.GetRemainingInvestmentClicks(run));

            Assert.IsFalse(WarRules.Invest(run, WarAttribute.Troops).success, "4º clique no mês é rejeitado pois o limite mensal é 3 cliques.");

            run.war.troops = 25; // espaço para novo mês
            WarRules.ResolveMonthEnd(run, MinRng());
            Assert.AreEqual(3, WarRules.GetRemainingInvestmentClicks(run), "Novo mês restaura 3 cliques.");
            Assert.IsFalse(WarRules.CanInvest(run, WarAttribute.Technology), "Economia continua no piso.");
            Assert.IsTrue(WarRules.Invest(run, WarAttribute.Troops).success, "Novo mês, novo investimento em Tropas.");
        }

        [Test]
        public void Invest_AllowsThreeClicksPerMonthInAnyCombination_AndResetsNextMonth()
        {
            var run = NewWar(WarLevel.Neighbor, 50);
            run.war.technology = 0;
            run.war.troops = 0;
            Assert.AreEqual(3, WarRules.GetRemainingInvestmentClicks(run));

            // Mês 1: todos os 3 cliques em Tecnologia
            Assert.IsTrue(WarRules.Invest(run, WarAttribute.Technology).success);
            Assert.IsTrue(WarRules.Invest(run, WarAttribute.Technology).success);
            Assert.IsTrue(WarRules.Invest(run, WarAttribute.Technology).success);
            Assert.AreEqual(0, WarRules.GetRemainingInvestmentClicks(run));
            Assert.AreEqual(75, run.war.technology); // 3 x 25
            Assert.AreEqual(0, run.war.troops);
            Assert.AreEqual(35, run.stats.economy); // 50 - 15
            Assert.IsFalse(WarRules.Invest(run, WarAttribute.Technology).success, "4º clique em tecnologia rejeitado.");
            Assert.IsFalse(WarRules.Invest(run, WarAttribute.Troops).success, "4º clique em tropas rejeitado.");

            // Avança para Mês 2: reseta para 3 cliques (reseta tech para ter espaço)
            run.war.technology = 0;
            WarRules.ResolveMonthEnd(run, MinRng());
            Assert.AreEqual(3, WarRules.GetRemainingInvestmentClicks(run));

            // Mês 2: 2 cliques em Tecnologia e 1 clique em Tropas
            Assert.IsTrue(WarRules.Invest(run, WarAttribute.Technology).success);
            Assert.IsTrue(WarRules.Invest(run, WarAttribute.Technology).success);
            Assert.IsTrue(WarRules.Invest(run, WarAttribute.Troops).success);
            Assert.AreEqual(0, WarRules.GetRemainingInvestmentClicks(run));
            Assert.IsFalse(WarRules.Invest(run, WarAttribute.Troops).success);

            // Avança para Mês 3: reseta para 3 cliques (reseta tropas para ter espaço)
            run.war.troops = 0;
            WarRules.ResolveMonthEnd(run, MinRng());
            Assert.AreEqual(3, WarRules.GetRemainingInvestmentClicks(run));

            // Mês 3: todos os 3 cliques em Tropas
            Assert.IsTrue(WarRules.Invest(run, WarAttribute.Troops).success);
            Assert.IsTrue(WarRules.Invest(run, WarAttribute.Troops).success);
            Assert.IsTrue(WarRules.Invest(run, WarAttribute.Troops).success);
            Assert.AreEqual(0, WarRules.GetRemainingInvestmentClicks(run));
            Assert.IsFalse(WarRules.Invest(run, WarAttribute.Troops).success);
        }

        [Test]
        public void Invest_WithoutActiveWar_Fails()
        {
            var noWar = NewRun();
            Assert.IsFalse(WarRules.Invest(noWar, WarAttribute.Technology).success);
            Assert.IsFalse(WarRules.CanInvest(noWar, WarAttribute.Troops));
        }

        [Test]
        public void Invest_IsNotBlockedByPreventStatLoss()
        {
            var run = NewWar();
            run.preventStatLossThisMonth = true;

            WarRules.Invest(run, WarAttribute.Technology);
            WarRules.Invest(run, WarAttribute.Troops);

            Assert.AreEqual(45, run.stats.economy);
            Assert.AreEqual(45, run.stats.popularApproval);
        }

        [Test]
        public void Snapshot_ExposesBarAndInvestmentState_AndWarStats()
        {
            var run = NewWar();
            var snap = run.GetSnapshot().War;

            Assert.IsTrue(snap.IsActive);
            Assert.AreEqual(50, snap.Progress);
            Assert.AreEqual(50, snap.Technology);
            Assert.AreEqual(50, snap.Troops);
            Assert.AreEqual(2, snap.TechnologyLevel);
            Assert.AreEqual(2, snap.TroopsLevel);
            Assert.AreEqual(WarLevel.Neighbor, snap.Level);
            Assert.IsTrue(snap.CanInvestTechnology);
            Assert.IsTrue(snap.CanInvestTroops);
            Assert.AreEqual(5, snap.InvestmentCost);
            Assert.AreEqual(3, snap.RemainingInvestmentClicks);
            Assert.AreEqual(3, snap.MaxInvestmentClicks);
            Assert.IsFalse(snap.CanSurrender);
        }
        [Test]
        public void Snapshot_WithoutWar_IsInactive()
        {
            Assert.IsFalse(NewRun().GetSnapshot().War.IsActive);
        }

        // ---------------------------------------------------------------- Integração

        [Test]
        public void MonthlyResolver_TicksTheWarAndAdvancesCalendar()
        {
            var run = NewWar(WarLevel.Neighbor, 100);

            var report = MonthlyEffectsResolver.ResolveMonth(run);

            Assert.IsNotNull(report.war);
            Assert.AreEqual(1, run.war.monthsInWar);
            Assert.AreEqual(2, run.calendar.currentMonthIndex);
            Assert.AreEqual(report.war.progressAfter, run.war.progress);
        }

        [Test]
        public void MonthlyResolver_WithoutWar_HasNoWarReport()
        {
            var report = MonthlyEffectsResolver.ResolveMonth(NewRun());
            Assert.IsNull(report.war);
        }

        [Test]
        public void ResolveMonthEnd_IsDeterministicForTheSameSeed()
        {
            var a = NewWar(WarLevel.WorldDomination, 80);
            var b = NewWar(WarLevel.WorldDomination, 80);

            for (int i = 0; i < 6; i++)
            {
                WarRules.ResolveMonthEnd(a);
                WarRules.ResolveMonthEnd(b);
            }

            Assert.AreEqual(a.war.progress, b.war.progress);
            Assert.AreEqual(a.war.troops, b.war.troops);
            Assert.AreEqual(a.war.technology, b.war.technology);
            Assert.AreEqual(a.stats.climaticChanges, b.stats.climaticChanges);
        }

        // ---------------------------------------------------------------- Gatilhos

        [Test]
        public void HostileCardChoice_SchedulesWar_AndStartsWhenMonthAdvances()
        {
            var run = NewRun();
            var hostile = new ChoiceDefinition("Atacar", new StatBlock(0, -20, 0, 0, 0))
            {
                triggersWar = WarLevel.Dominant,
                warTargetId = "Nação Dominante"
            };
            var card = CardDefinition.CreateRuntimeInstance("war_card", "Ultimato", "desc", hostile, new ChoiceDefinition("Recuar"));

            var report = DecisionResolver.Resolve(run, null, card, 0);

            Assert.AreEqual(WarLevel.Dominant, report.startedWarLevel);
            Assert.AreEqual(WarLevel.Dominant, run.pendingWarLevel);
            Assert.IsFalse(run.war.isActive, "Guerra não deve iniciar imediatamente com o NPC na sala.");

            // Quando o mês avança (NPC deixa a sala):
            run.AdvanceMonth();

            Assert.IsTrue(run.war.isActive);
            Assert.AreEqual("Nação Dominante", run.war.targetId);
            Assert.AreEqual(25, run.war.technology); // 50 - 20 = 30 -> 30 arredonda para 25
            Assert.AreEqual(25, run.war.troops);
        }

        [Test]
        public void PeacefulChoice_DoesNotDeclareWar()
        {
            var run = NewRun();
            var card = CardDefinition.CreateRuntimeInstance("war_card", "Ultimato", "desc",
                new ChoiceDefinition("Atacar") { triggersWar = WarLevel.Neighbor, warTargetId = "X" },
                new ChoiceDefinition("Recuar"));

            var report = DecisionResolver.Resolve(run, null, card, 1);

            Assert.AreEqual(WarLevel.None, report.startedWarLevel);
            Assert.IsFalse(run.war.HasStarted);
        }

        [Test]
        public void FlipPhoneTriggerWarEffect_DeclaresWar()
        {
            var run = NewRun();
            var action = FlipPhoneActionDefinition.CreateRuntimeInstance("act_war", "Declarar Guerra", "desc");
            action.effects.Add(FlipPhoneEffect.CreateTriggerWar(WarLevel.WorldDomination, "Mundo"));

            var report = FlipPhoneResolver.ResolveUse(run, null, action);

            Assert.IsTrue(report.success, report.failReason);
            Assert.AreEqual(WarLevel.WorldDomination, report.startedWarLevel);
            Assert.AreEqual(WarLevel.WorldDomination, run.war.level);
        }

        [Test]
        public void AssassinatingForeignLeader_DeclaresWarAtTheNpcConfiguredLevel()
        {
            var run = NewRun();
            var leader = NpcDefinition.CreateRuntimeInstance("lider_vizinho", "Líder Vizinho");
            leader.isForeignLeader = true;
            leader.warLevelIfAssassinated = WarLevel.Dominant;
            var catalog = new Dictionary<string, NpcDefinition> { ["lider_vizinho"] = leader };

            var action = FlipPhoneActionDefinition.CreateRuntimeInstance("act_kill", "Eliminar", "desc");
            action.effects.Add(FlipPhoneEffect.CreateRemoveNpcFromGame("lider_vizinho"));

            var report = FlipPhoneResolver.ResolveUse(run, null, action, npcCatalog: catalog);

            Assert.IsTrue(report.success, report.failReason);
            Assert.AreEqual(WarLevel.Dominant, report.startedWarLevel);
            Assert.IsTrue(run.war.isActive);
            Assert.AreEqual("lider_vizinho", run.war.targetId);
        }

        [Test]
        public void AssassinatingDomesticNpc_DoesNotDeclareWar()
        {
            var run = NewRun();
            var minister = NpcDefinition.CreateRuntimeInstance("ministro", "Ministro");
            var catalog = new Dictionary<string, NpcDefinition> { ["ministro"] = minister };

            var action = FlipPhoneActionDefinition.CreateRuntimeInstance("act_kill", "Eliminar", "desc");
            action.effects.Add(FlipPhoneEffect.CreateRemoveNpcFromGame("ministro"));

            var report = FlipPhoneResolver.ResolveUse(run, null, action, npcCatalog: catalog);

            Assert.IsTrue(report.success, report.failReason);
            Assert.AreEqual(WarLevel.None, report.startedWarLevel);
            Assert.IsFalse(run.war.HasStarted);
        }

        // ---------------------------------------------------------------- Finais

        [Test]
        public void EndingEvaluator_PicksWarEnding_OnlyWhenOutcomeMatches()
        {
            var generic = EndingDefinition.CreateRuntimeInstance("generic", "Genérico", "x", victory: false, priority: 0);
            var surrender = EndingDefinition.CreateRuntimeInstance("surrender", "Protetorado", "x", victory: false, priority: 10);
            surrender.requiredWarOutcome = WarOutcome.Surrendered;
            var endings = new[] { generic, surrender };

            var surrendered = NewRun();
            surrendered.war.outcome = WarOutcome.Surrendered;
            surrendered.termination = RunTermination.CreateDefeat("x");
            Assert.AreEqual("surrender", EndingEvaluator.EvaluateEnding(surrendered, endings).id);

            var plain = NewRun();
            plain.termination = RunTermination.CreateDefeat("x");
            Assert.AreEqual("generic", EndingEvaluator.EvaluateEnding(plain, endings).id);
        }

        // ---------------------------------------------------------------- Persistência

        [Test]
        public void SaveData_RoundTripsWarState_ThroughJson()
        {
            var run = NewWar(WarLevel.Dominant, 70);
            run.war.progress = 63;
            run.war.technology = 17;
            run.war.troops = 9;
            run.war.monthsInWar = 4;
            run.war.investedThisMonth = true;

            var json = JsonUtility.ToJson(RunSaveData.FromRuntime(run, new DeckState()));
            var loaded = JsonUtility.FromJson<RunSaveData>(json);

            var restored = NewRun();
            loaded.ApplyToRuntime(restored, new DeckState());

            Assert.IsTrue(restored.war.isActive);
            Assert.AreEqual(WarLevel.Dominant, restored.war.level);
            Assert.AreEqual("Vizinholândia", restored.war.targetId);
            Assert.AreEqual(63, restored.war.progress);
            Assert.AreEqual(17, restored.war.technology);
            Assert.AreEqual(9, restored.war.troops);
            Assert.AreEqual(4, restored.war.monthsInWar);
            Assert.IsTrue(restored.war.investedThisMonth);
        }

        [Test]
        public void SaveData_FromOlderSaveWithoutWarField_LoadsAsInactiveWar()
        {
            var loaded = JsonUtility.FromJson<RunSaveData>("{\"seed\":7}");

            var restored = NewRun();
            loaded.ApplyToRuntime(restored, new DeckState());

            Assert.IsNotNull(restored.war);
            Assert.IsFalse(restored.war.HasStarted);
        }

        // ---------------------------------------------------------------- Balanceamento

        [Test]
        public void DefaultBalance_MatchesTheDesignedRanges_AndIsValid()
        {
            var b = WarBalance.CreateDefault();

            Assert.IsTrue(b.Validate(out var errors), string.Join("; ", errors));

            Assert.AreEqual(0, b.GetLevel(WarLevel.Neighbor).enemyPressurePerMonth);
            Assert.AreEqual(0, b.GetLevel(WarLevel.Dominant).enemyPressurePerMonth);
            Assert.AreEqual(0, b.GetLevel(WarLevel.WorldDomination).enemyPressurePerMonth);
            Assert.AreEqual(25, b.offensiveThreshold);
            Assert.AreEqual(5, b.investmentPointsPerClick);
            Assert.AreEqual(3, b.maxInvestmentClicksPerMonth);
        }

        [Test]
        public void Balance_Validate_FlagsMissingLevelsAndInvalidStartingProgress()
        {
            var b = WarBalance.CreateDefault();
            b.startingProgress = 100;
            b.levels.RemoveAll(l => l.level == WarLevel.WorldDomination);

            Assert.IsFalse(b.Validate(out var errors));
            Assert.AreEqual(2, errors.Count);
        }

        [Test]
        public void CustomBalanceOnRunState_IsUsedByTheRules()
        {
            var run = NewRun();
            run.warBalance = WarBalance.CreateDefault();
            run.warBalance.startingProgress = 40;
            run.warBalance.offensiveBonusPercent = 25;
            run.stats.internationalRelations = 100;

            WarRules.StartWar(run, WarLevel.Neighbor);
            MaxOutMilitary(run);
            WarRules.ResolveMonthEnd(run, MinRng());

            Assert.AreEqual(65, run.war.progress); // 40 + 25
        }
    }
}

