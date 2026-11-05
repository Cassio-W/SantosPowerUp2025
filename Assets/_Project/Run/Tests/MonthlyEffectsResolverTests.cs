using System.Collections.Generic;
using Mandato.Content;
using Mandato.Core;
using Mandato.Run;
using NUnit.Framework;

namespace Mandato.Run.Tests
{
    public class MonthlyEffectsResolverTests
    {
        [Test]
        public void ResolveMonth_AppliesStatDeltas_FromActivePerksAndEvents()
        {
            var runState = new RunState();
            runState.stats.economy = 50;
            runState.stats.corruption = 10;
            runState.stats.climaticChanges = 50;

            var perk = PerkDefinition.CreateRuntimeInstance(
                "perk_eco_boost",
                "Crescimento Verde",
                "Aumenta economia e clima todo mês.",
                monthlyDeltas: new StatBlock(5, 0, 0, 10, 0),
                duration: 3
            );

            var ev = RunEventDefinition.CreateRuntimeInstance(
                "event_seca",
                "Crise Hídrica",
                "Reduz clima e aumenta custos.",
                duration: 2,
                monthlyModifiers: new StatBlock(-10, 0, 0, -5, 0)
            );

            var perkCatalog = new Dictionary<string, PerkDefinition> { { perk.id, perk } };
            var eventCatalog = new Dictionary<string, RunEventDefinition> { { ev.id, ev } };

            runState.GrantPerk(perk.id, duration: 3);
            runState.TriggerEvent(ev.id, duration: 2);

            var report = MonthlyEffectsResolver.ResolveMonth(runState, perkCatalog, eventCatalog);

            Assert.IsTrue(report.isSuccess);
            Assert.AreEqual(45, runState.stats.climaticChanges); // 50 + 5 - 10 = 45
            Assert.AreEqual(55, runState.stats.economy);         // 50 + 10 - 5 = 55
            Assert.AreEqual(2, report.advancedToMonthIndex);     // Avançou para mês 2
            Assert.IsTrue(report.activePerkIdsApplied.Contains(perk.id));
            Assert.IsTrue(report.activeEventIdsApplied.Contains(ev.id));
        }

        [Test]
        public void ResolveMonth_ExpiresTemporaryPerksAndEvents_AtZeroMonths()
        {
            var runState = new RunState();
            var perk = PerkDefinition.CreateRuntimeInstance("perk_temp", "Perk Temporário", "", new StatBlock(0, 0, 0, 0, 0), duration: 1);
            var ev = RunEventDefinition.CreateRuntimeInstance("ev_temp", "Evento Temporário", "", duration: 1);

            var perkCatalog = new Dictionary<string, PerkDefinition> { { perk.id, perk } };
            var eventCatalog = new Dictionary<string, RunEventDefinition> { { ev.id, ev } };

            runState.GrantPerk(perk.id, duration: 1);
            runState.TriggerEvent(ev.id, duration: 1);

            var report1 = MonthlyEffectsResolver.ResolveMonth(runState, perkCatalog, eventCatalog);

            Assert.IsTrue(report1.expiredPerkIds.Contains("perk_temp"));
            Assert.IsTrue(report1.expiredEventIds.Contains("ev_temp"));
            Assert.IsFalse(runState.activePerkIds.Contains("perk_temp"));
            Assert.AreEqual(0, runState.activeEvents.Count);
        }

        [Test]
        public void ResolveMonth_TicksActionCooldowns_AndAdvancesCalendar()
        {
            var runState = new RunState();
            runState.actionCooldowns["action_intervencao"] = 2;

            var report = MonthlyEffectsResolver.ResolveMonth(runState);

            Assert.AreEqual(1, runState.GetActionCooldown("action_intervencao"));
            Assert.AreEqual(2, report.advancedToMonthIndex);

            MonthlyEffectsResolver.ResolveMonth(runState);
            Assert.AreEqual(0, runState.GetActionCooldown("action_intervencao"));
            Assert.IsFalse(runState.IsActionOnCooldown("action_intervencao"));
        }

        [Test]
        public void ResolveMonth_TriggersDefeat_WhenStatDropsToZeroDuringMonthlyTick()
        {
            var runState = new RunState();
            runState.stats.economy = 5; // Quase quebrando

            var ev = RunEventDefinition.CreateRuntimeInstance(
                "event_hiperinflacao",
                "Hiperinflação",
                "",
                duration: 3,
                monthlyModifiers: new StatBlock(0, 0, 0, -10, 0)
            );

            var eventCatalog = new Dictionary<string, RunEventDefinition> { { ev.id, ev } };
            runState.TriggerEvent(ev.id, duration: 3);

            var report = MonthlyEffectsResolver.ResolveMonth(runState, null, eventCatalog);

            Assert.AreEqual(0, runState.stats.economy);
            Assert.IsTrue(report.resultingTermination.IsDefeat);
            Assert.IsTrue(runState.termination.IsDefeat);
        }

        [Test]
        public void ResolveMonth_TriggersCampaignEvent_AtMonth37_AndLocksPoliticalAxis()
        {
            var runState = new RunState();
            runState.calendar.currentMonthIndex = 37;
            Assert.IsFalse(runState.politicalAxis.isLocked);

            var report = MonthlyEffectsResolver.ResolveMonth(runState);

            Assert.IsTrue(report.triggeredEventIds.Contains(MonthlyEffectsResolver.CampaignEventId));
            Assert.IsTrue(runState.activeEvents.Exists(e => e.eventId == MonthlyEffectsResolver.CampaignEventId));
            Assert.IsTrue(runState.politicalAxis.isLocked, "Eixo político deve ser travado durante a Campanha Eleitoral no 4º ano.");

            // Tenta aplicar alteração no eixo político após travamento
            int prevX = runState.politicalAxis.x;
            int prevY = runState.politicalAxis.y;
            runState.ApplyPoliticalDelta(5, -5);
            Assert.AreEqual(prevX, runState.politicalAxis.x, "Eixo travado não deve sofrer alteração em X.");
            Assert.AreEqual(prevY, runState.politicalAxis.y, "Eixo travado não deve sofrer alteração em Y.");
        }

        [Test]
        public void Perk_AliancaJudiciario_RescuesCorruptionAt100_To65_SingleUse()
        {
            var runState = new RunState();
            runState.stats.corruption = 90;
            runState.GrantPerk("AliancaJudiciario");

            runState.ApplyStatDelta(StatId.Corruption, 15); // Atingiria 100
            bool rescued = runState.CheckAndApplyEmergencyRescue(out string perkId, out StatId statId);

            Assert.IsTrue(rescued);
            Assert.AreEqual("AliancaJudiciario", perkId);
            Assert.AreEqual(StatId.Corruption, statId);
            Assert.AreEqual(65, runState.stats.corruption);
            Assert.IsFalse(runState.HasPerk("AliancaJudiciario"));
        }

        [Test]
        public void Perk_Imorrivel_RescuesAnyStatAt1_AndRechargesAfter18Months()
        {
            var runState = new RunState();
            runState.stats.popularApproval = 5;
            runState.GrantPerk("Imorrivel");

            runState.ApplyStatDelta(StatId.PopularApproval, -10); // Cairia para 0
            bool rescued = runState.CheckAndApplyEmergencyRescue(out string perkId, out StatId statId);

            Assert.IsTrue(rescued);
            Assert.AreEqual("Imorrivel", perkId);
            Assert.AreEqual(StatId.PopularApproval, statId);
            Assert.AreEqual(1, runState.stats.popularApproval);
            Assert.IsFalse(runState.HasPerk("Imorrivel"), "Deve entrar em recarga por 18 meses");

            // Avança 17 meses: ainda em cooldown
            for (int i = 0; i < 17; i++)
                MonthlyEffectsResolver.ResolveMonth(runState);
            Assert.IsFalse(runState.HasPerk("Imorrivel"));

            // 18º mês: recarga concluída
            MonthlyEffectsResolver.ResolveMonth(runState);
            Assert.IsTrue(runState.HasPerk("Imorrivel"));
        }

        [Test]
        public void Perk_ComunicadorNato_BoostsLowestNonCorruptionStatBy30_OnSixthMonthOfEachYear()
        {
            var runState = new RunState();
            runState.calendar.currentMonthIndex = 6; // Mês 6 do Ano 1
            runState.stats.climaticChanges = 50;
            runState.stats.internationalRelations = 35; // Menor atributo (exceto corrupção)
            runState.stats.popularApproval = 70;
            runState.stats.economy = 50;
            runState.stats.corruption = 10;

            runState.GrantPerk("ComunicadorNato");

            MonthlyEffectsResolver.ResolveMonth(runState);

            Assert.AreEqual(65, runState.stats.internationalRelations); // 35 + 30 = 65
        }

        [Test]
        public void Perk_GrandeIrmao_ConvertsPopularityLossToInternationalRelationsLoss()
        {
            var runState = new RunState();
            runState.stats.popularApproval = 60;
            runState.stats.internationalRelations = 50;
            runState.GrantPerk("GrandeIrmao");

            runState.ApplyStatImpacts(new StatBlock(0, 0, -15, 0, 0));

            Assert.AreEqual(60, runState.stats.popularApproval, "Não pode perder popularidade");
            Assert.AreEqual(35, runState.stats.internationalRelations, "Perda convertida em Relações Internacionais");
        }

        [Test]
        public void Perk_ReservaFlorestal_And_ThisIsBrasil_IncreaseMaxStatCaps()
        {
            var runState = new RunState();
            runState.GrantPerk("ReservaFlorestal");
            runState.GrantPerk("ThisIsBrasil");

            Assert.AreEqual(130, runState.stats.GetMax(StatId.ClimaticChanges));
            Assert.AreEqual(120, runState.stats.GetMax(StatId.InternationalRelations));

            runState.ApplyStatImpacts(new StatBlock(100, 100, 100, 0, 0));
            Assert.AreEqual(130, runState.stats.climaticChanges);
            Assert.AreEqual(120, runState.stats.internationalRelations);
            Assert.AreEqual(100, runState.stats.popularApproval);
        }

        [Test]
        public void Perk_InvestimentoUsina_Drains2EcoMonthly_AndGrants25EcoOnMonth12Expiration()
        {
            var runState = new RunState();
            runState.stats.economy = 50;

            var perk = PerkDefinition.CreateRuntimeInstance(
                "InvestimentoUsina",
                "Investimento em Usina",
                "-2 Eco por 12 meses, +25 Eco ao final.",
                monthlyDeltas: new StatBlock(0, 0, 0, -2, 0),
                duration: 12
            );
            perk.expirationStatBonus = new StatBlock(0, 0, 0, 25, 0);
            var perkCatalog = new Dictionary<string, PerkDefinition> { { perk.id, perk } };

            runState.GrantPerk("InvestimentoUsina", duration: 12);

            for (int i = 0; i < 12; i++)
            {
                MonthlyEffectsResolver.ResolveMonth(runState, perkCatalog);
            }

            // 50 - (2 * 12) + 25 = 51
            Assert.AreEqual(51, runState.stats.economy);
            Assert.IsFalse(runState.HasPerk("InvestimentoUsina"));
        }
    }
}
