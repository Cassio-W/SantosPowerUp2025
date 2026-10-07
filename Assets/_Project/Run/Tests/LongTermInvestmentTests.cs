using System.Collections.Generic;
using Mandato.Content;
using Mandato.Core;
using Mandato.Run;
using NUnit.Framework;

namespace Mandato.Run.Tests
{
    public class LongTermInvestmentTests
    {
        [Test]
        public void StartInvestment_AddsActiveInvestment_WithCorrectDuration()
        {
            var run = new RunState();
            run.StartInvestment("inv_usina_solar", 6);

            Assert.AreEqual(1, run.activeInvestments.Count);
            Assert.AreEqual("inv_usina_solar", run.activeInvestments[0].investmentId);
            Assert.AreEqual(6, run.activeInvestments[0].remainingMonths);
            Assert.AreEqual(6, run.activeInvestments[0].totalMonths);
        }

        [Test]
        public void StartInvestment_Reinitializes_ExistingInvestment()
        {
            var run = new RunState();
            run.StartInvestment("inv_metro", 12);
            run.StartInvestment("inv_metro", 6); // reinicia

            Assert.AreEqual(1, run.activeInvestments.Count);
            Assert.AreEqual(6, run.activeInvestments[0].remainingMonths);
            Assert.IsFalse(run.activeInvestments[0].isCancelled);
        }

        [Test]
        public void CancelInvestment_MarksAsCancelled_WithoutRemoving()
        {
            var run = new RunState();
            run.StartInvestment("inv_aeroporto", 10);
            run.CancelInvestment("inv_aeroporto");

            Assert.AreEqual(1, run.activeInvestments.Count);
            Assert.IsTrue(run.activeInvestments[0].isCancelled);
        }

        [Test]
        public void ResolveMonth_TicksInvestment_AndAppliesMonthlyCost()
        {
            var run = new RunState();
            run.stats.economy = 60;
            run.StartInvestment("inv_ferrovia", 3);

            var invDef = LongTermInvestmentDefinition.CreateRuntimeInstance(
                id: "inv_ferrovia",
                title: "Ferrovia Nacional",
                description: "Constrói uma ferrovia.",
                durationMonths: 3,
                costPerMonth: new StatBlock(0, 0, 0, -5, 0) // -5 Economia por mês
            );
            var catalog = new Dictionary<string, LongTermInvestmentDefinition>
            {
                { "inv_ferrovia", invDef }
            };

            var report = MonthlyEffectsResolver.ResolveMonth(run, null, null, catalog);

            Assert.AreEqual(55, run.stats.economy, "Economia deve ter sido decrementada em 5.");
            Assert.AreEqual(1, run.activeInvestments.Count, "Investimento ainda ativo (2 meses restantes).");
            Assert.AreEqual(2, run.activeInvestments[0].remainingMonths);
            Assert.IsEmpty(report.completedInvestmentIds);
        }

        [Test]
        public void ResolveMonth_CompletesInvestment_GrantsPerkAndRemovesFromActive()
        {
            var run = new RunState();
            run.StartInvestment("inv_parque_solar", 1);

            var invDef = LongTermInvestmentDefinition.CreateRuntimeInstance(
                id: "inv_parque_solar",
                title: "Parque Solar",
                description: "Energia renovável.",
                durationMonths: 1,
                costPerMonth: null,
                completionBonus: new StatBlock(0, 0, 0, 10, 0)
            );
            invDef.completionPerkIds.Add("perk_energia_verde");

            var perkDef = PerkDefinition.CreateRuntimeInstance(
                id: "perk_energia_verde",
                title: "Energia Verde",
                description: "Reduz emissões.",
                monthlyDeltas: new StatBlock(0, 0, 1, 0, 0),
                duration: 0
            );

            var invCatalog = new Dictionary<string, LongTermInvestmentDefinition> { { "inv_parque_solar", invDef } };
            var perkCatalog = new Dictionary<string, PerkDefinition> { { "perk_energia_verde", perkDef } };

            int economyBefore = run.stats.economy;
            var report = MonthlyEffectsResolver.ResolveMonth(run, perkCatalog, null, invCatalog);

            Assert.AreEqual(economyBefore + 10, run.stats.economy, "Bônus de conclusão de +10 economia deveria ter sido aplicado.");
            Assert.IsEmpty(run.activeInvestments, "Investimento concluído deve ser removido da lista.");
            CollectionAssert.Contains(report.completedInvestmentIds, "inv_parque_solar");
            CollectionAssert.Contains(report.investmentGrantedPerkIds, "perk_energia_verde");
            CollectionAssert.Contains(run.activePerkIds, "perk_energia_verde");
        }

        [Test]
        public void ResolveMonth_CancelledInvestment_IsRemovedWithoutEffects()
        {
            var run = new RunState();
            run.StartInvestment("inv_usina_nuclear", 5);
            run.CancelInvestment("inv_usina_nuclear");

            var invDef = LongTermInvestmentDefinition.CreateRuntimeInstance(
                id: "inv_usina_nuclear",
                title: "Usina Nuclear",
                description: "Energia nuclear.",
                durationMonths: 5,
                completionBonus: new StatBlock(0, 0, 50, 0, 0)
            );
            invDef.completionPerkIds.Add("perk_nuclear");

            var invCatalog = new Dictionary<string, LongTermInvestmentDefinition> { { "inv_usina_nuclear", invDef } };

            int relBefore = run.stats.internationalRelations;
            var report = MonthlyEffectsResolver.ResolveMonth(run, null, null, invCatalog);

            Assert.IsEmpty(run.activeInvestments, "Cancelado deve ser removido.");
            Assert.AreEqual(relBefore, run.stats.internationalRelations, "Bônus não deve ser aplicado em investimento cancelado.");
            CollectionAssert.Contains(report.cancelledInvestmentIds, "inv_usina_nuclear");
            Assert.IsEmpty(report.completedInvestmentIds);
        }

        [Test]
        public void ActiveInvestmentState_Progress_IsCorrect()
        {
            var inv = new ActiveInvestmentState("inv_test", 4);
            Assert.AreEqual(0f, inv.Progress, 0.01f, "Progresso inicial deve ser 0.");
            inv.TickMonth();
            Assert.AreEqual(0.25f, inv.Progress, 0.01f);
            inv.TickMonth();
            Assert.AreEqual(0.5f, inv.Progress, 0.01f);
            inv.TickMonth();
            Assert.AreEqual(0.75f, inv.Progress, 0.01f);
            bool completed = inv.TickMonth();
            Assert.IsTrue(completed, "Último tick deve retornar true.");
            Assert.AreEqual(1f, inv.Progress, 0.01f);
        }

        [Test]
        public void RunReset_ClearsActiveInvestments()
        {
            var run = new RunState();
            run.StartInvestment("inv_reset_test", 6);
            run.Reset();
            Assert.IsEmpty(run.activeInvestments);
        }
    }
}
