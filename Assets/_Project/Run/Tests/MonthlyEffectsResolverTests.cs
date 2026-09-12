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
        public void ResolveMonth_TriggersCampaignEvent_AtMonth37()
        {
            var runState = new RunState();
            runState.calendar.currentMonthIndex = 37;

            var report = MonthlyEffectsResolver.ResolveMonth(runState);

            Assert.IsTrue(report.triggeredEventIds.Contains(MonthlyEffectsResolver.CampaignEventId));
            Assert.IsTrue(runState.activeEvents.Exists(e => e.eventId == MonthlyEffectsResolver.CampaignEventId));
        }
    }
}
