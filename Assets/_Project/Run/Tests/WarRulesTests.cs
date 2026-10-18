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
        public void StartWar_InitializesBarAtFiftyAndHiddenStatsAsHalfOfSovereignty()
        {
            var run = NewRun();
            run.stats.internationalRelations = 51;

            bool started = WarRules.StartWar(run, WarLevel.Dominant, "Nação");

            Assert.IsTrue(started);
            Assert.IsTrue(run.war.isActive);
            Assert.AreEqual(WarLevel.Dominant, run.war.level);
            Assert.AreEqual("Nação", run.war.targetId);
            Assert.AreEqual(50, run.war.progress);
            Assert.AreEqual(25, run.war.technology); // 51 / 2 (divisão inteira)
            Assert.AreEqual(25, run.war.troops);
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
        public void ResolveMonthEnd_DrainsTroopsEveryMonth_WithinLevelRange()
        {
            foreach (var (level, min, max) in new[]
                     {
                         (WarLevel.Neighbor, 1, 3),
                         (WarLevel.Dominant, 3, 5),
                         (WarLevel.WorldDomination, 5, 10)
                     })
            {
                var runMin = NewWar(level, 100);
                var runMax = NewWar(level, 100);

                var rMin = WarRules.ResolveMonthEnd(runMin, MinRng());
                var rMax = WarRules.ResolveMonthEnd(runMax, MaxRng());

                Assert.AreEqual(min, rMin.troopsDrained, $"{level} min");
                Assert.AreEqual(max, rMax.troopsDrained, $"{level} max");
                Assert.AreEqual(50 - min, runMin.war.troops);
                Assert.AreEqual(50 - max, runMax.war.troops);
            }
        }

        [Test]
        public void ResolveMonthEnd_DrainsTechnologyOnlyEveryThirdMonth_WithinLevelRange()
        {
            foreach (var (level, min, max) in new[]
                     {
                         (WarLevel.Neighbor, 3, 9),
                         (WarLevel.Dominant, 10, 15),
                         (WarLevel.WorldDomination, 15, 20)
                     })
            {
                var runMin = NewWar(level, 100);
                var runMax = NewWar(level, 100);

                for (int month = 1; month <= 3; month++)
                {
                    var rMin = WarRules.ResolveMonthEnd(runMin, MinRng());
                    var rMax = WarRules.ResolveMonthEnd(runMax, MaxRng());

                    if (month < 3)
                    {
                        Assert.IsFalse(rMin.technologyDrainApplied, $"{level} mês {month}");
                        Assert.AreEqual(50, runMin.war.technology);
                    }
                    else
                    {
                        Assert.IsTrue(rMin.technologyDrainApplied);
                        Assert.AreEqual(min, rMin.technologyDrained, $"{level} min");
                        Assert.AreEqual(max, rMax.technologyDrained, $"{level} max");
                        Assert.AreEqual(50 - min, runMin.war.technology);
                        Assert.AreEqual(50 - max, runMax.war.technology);
                    }
                }
            }
        }

        // ---------------------------------------------------------------- Recompensa x falha

        [Test]
        public void ResolveMonthEnd_BothAboveThreshold_AdvancesTenPercent()
        {
            var run = NewWar(WarLevel.Neighbor, 100); // 50/50

            var report = WarRules.ResolveMonthEnd(run, MinRng());

            Assert.IsTrue(report.offensiveBonusApplied);
            Assert.IsFalse(report.maintenanceFailed);
            Assert.AreEqual(60, run.war.progress);
        }

        [Test]
        public void ResolveMonthEnd_ExactlyAtThreshold_DoesNotAdvance()
        {
            var run = NewWar(WarLevel.Neighbor, 100);
            run.war.troops = 11; // após drenar 1 => 10, que NÃO é "acima de 10"

            var report = WarRules.ResolveMonthEnd(run, MinRng());

            Assert.AreEqual(10, run.war.troops);
            Assert.IsFalse(report.offensiveBonusApplied);
            Assert.IsFalse(report.maintenanceFailed);
            Assert.AreEqual(50, run.war.progress);
        }

        [Test]
        public void ResolveMonthEnd_EnemyPressureAppliesByLevel()
        {
            var run = NewWar(WarLevel.Dominant, 100);

            WarRules.ResolveMonthEnd(run, MinRng());

            Assert.AreEqual(50 + 10 - 2, run.war.progress);
        }

        [Test]
        public void ResolveMonthEnd_ZeroTroops_IsMaintenanceFailure_AndLosesProgress()
        {
            var run = NewWar(WarLevel.Neighbor, 100);
            run.war.troops = 1;

            var report = WarRules.ResolveMonthEnd(run, MinRng(0.99));

            Assert.IsTrue(report.maintenanceFailed);
            Assert.IsFalse(report.offensiveBonusApplied);
            Assert.AreEqual(0, run.war.troops);
            Assert.AreEqual(42, run.war.progress); // 50 - 8
        }

        [Test]
        public void ResolveMonthEnd_BothZero_PenalizesEachAttribute()
        {
            var run = NewWar(WarLevel.Neighbor, 100);
            run.war.monthsInWar = 2; // próximo tick é o 3º mês (drena tecnologia)
            run.war.troops = 1;
            run.war.technology = 1;

            WarRules.ResolveMonthEnd(run, MinRng(0.99));

            Assert.AreEqual(50 - 16, run.war.progress);
        }

        [Test]
        public void ResolveMonthEnd_MaintenanceFailure_ClimateDropsByTen_OnlyWhenRollSucceeds()
        {
            var hit = NewWar(WarLevel.Neighbor, 100);
            hit.war.troops = 1;
            var hitReport = WarRules.ResolveMonthEnd(hit, MinRng(0.0));

            Assert.IsTrue(hitReport.climateHit);
            Assert.AreEqual(40, hit.stats.climaticChanges);

            var miss = NewWar(WarLevel.Neighbor, 100);
            miss.war.troops = 1;
            var missReport = WarRules.ResolveMonthEnd(miss, MinRng(0.99));

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
            run.war.troops = 1;

            var report = WarRules.ResolveMonthEnd(run, MinRng(0.99));

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

            WarRules.ResolveMonthEnd(run, MinRng());

            Assert.AreEqual(100, run.stats.popularApproval);
        }

        [Test]
        public void NeighborWar_WithoutInvestment_ReachesVictoryInFiveMonths()
        {
            var run = NewWar(WarLevel.Neighbor, 50); // 25/25

            int months = 0;
            while (run.war.isActive && months < 20)
            {
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
        public void Invest_ConvertsEconomyToTechnologyAndPopularityToTroops_OneToOne_WithMonthlyCap()
        {
            var run = NewWar(WarLevel.Neighbor, 50); // tech/troops 25

            var result = WarRules.Invest(run, 100, 100);

            Assert.IsTrue(result.success);
            Assert.AreEqual(5, result.economySpent);
            Assert.AreEqual(5, result.popularitySpent);
            Assert.AreEqual(45, run.stats.economy);
            Assert.AreEqual(45, run.stats.popularApproval);
            Assert.AreEqual(30, run.war.technology);
            Assert.AreEqual(30, run.war.troops);
        }

        [Test]
        public void Invest_RespectsSafetyFloor_AndIndependentPerStatMonthlyCap()
        {
            var run = NewWar();
            run.stats.economy = 13;

            var first = WarRules.Invest(run, 5, 0);
            Assert.IsTrue(first.success);
            Assert.AreEqual(3, first.economySpent, "Piso de segurança: Economia nunca cai abaixo de 10.");
            Assert.AreEqual(10, run.stats.economy);

            var secondEcon = WarRules.Invest(run, 5, 0);
            Assert.IsFalse(secondEcon.success, "Economia já atingiu o piso.");

            var firstPop = WarRules.Invest(run, 0, 5);
            Assert.IsTrue(firstPop.success, "Popularidade pode ser investida independentemente no mesmo mês.");
            Assert.AreEqual(5, firstPop.popularitySpent);

            var secondPop = WarRules.Invest(run, 0, 5);
            Assert.IsFalse(secondPop.success, "Popularidade já atingiu o limite mensal de 5.");

            WarRules.ResolveMonthEnd(run, MinRng());
            Assert.AreEqual(0, WarRules.GetMaxInvestment(run, StatId.Economy), "Economia já está no piso.");
            Assert.IsTrue(WarRules.Invest(run, 0, 5).success, "Novo mês, novo investimento em Tropas.");
        }

        [Test]
        public void Invest_ZeroOrNoWar_Fails()
        {
            var noWar = NewRun();
            Assert.IsFalse(WarRules.Invest(noWar, 5, 5).success);

            var run = NewWar();
            Assert.IsFalse(WarRules.Invest(run, 0, 0).success);
            Assert.IsFalse(WarRules.Invest(run, -5, -5).success);
        }

        [Test]
        public void Invest_IsNotBlockedByPreventStatLoss()
        {
            var run = NewWar();
            run.preventStatLossThisMonth = true;

            WarRules.Invest(run, 5, 5);

            Assert.AreEqual(45, run.stats.economy);
            Assert.AreEqual(45, run.stats.popularApproval);
        }

        [Test]
        public void Snapshot_ExposesBarAndInvestmentLimits_AndWarStats()
        {
            var run = NewWar();
            var snap = run.GetSnapshot().War;

            Assert.IsTrue(snap.IsActive);
            Assert.AreEqual(50, snap.Progress);
            Assert.AreEqual(25, snap.Technology);
            Assert.AreEqual(25, snap.Troops);
            Assert.AreEqual(WarLevel.Neighbor, snap.Level);
            Assert.AreEqual(5, snap.MaxEconomyInvestment);
            Assert.AreEqual(5, snap.MaxPopularityInvestment);
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
        public void HostileCardChoice_DeclaresWar_UsingSovereigntyAfterTheChoiceImpacts()
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
            Assert.IsTrue(run.war.isActive);
            Assert.AreEqual("Nação Dominante", run.war.targetId);
            Assert.AreEqual(15, run.war.technology); // (50 - 20) / 2
            Assert.AreEqual(15, run.war.troops);
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

            var n = b.GetLevel(WarLevel.Neighbor);
            Assert.AreEqual((1, 3, 3, 9), (n.troopsDrainMin, n.troopsDrainMax, n.technologyDrainMin, n.technologyDrainMax));
            var d = b.GetLevel(WarLevel.Dominant);
            Assert.AreEqual((3, 5, 10, 15), (d.troopsDrainMin, d.troopsDrainMax, d.technologyDrainMin, d.technologyDrainMax));
            var w = b.GetLevel(WarLevel.WorldDomination);
            Assert.AreEqual((5, 10, 15, 20), (w.troopsDrainMin, w.troopsDrainMax, w.technologyDrainMin, w.technologyDrainMax));
        }

        [Test]
        public void Balance_Validate_FlagsInvertedRangesAndMissingLevels()
        {
            var b = WarBalance.CreateDefault();
            b.GetLevel(WarLevel.Dominant).troopsDrainMin = 9;
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
            WarRules.ResolveMonthEnd(run, MinRng());

            Assert.AreEqual(65, run.war.progress); // 40 + 25
        }
    }
}

