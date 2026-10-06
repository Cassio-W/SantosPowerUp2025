using NUnit.Framework;
using Mandato.Core;
using Mandato.Run;

namespace Mandato.Run.Tests
{
    public class RunStateTests
    {
        [Test]
        public void FullRunSimulation_48Months_LeadsToVictory()
        {
            var run = new RunState(seed: 12345);

            Assert.IsTrue(run.termination.IsOngoing);
            Assert.AreEqual(1, run.calendar.currentMonthIndex);

            // Simula 48 meses de mandato com decisões estáveis
            for (int month = 1; month <= 48; month++)
            {
                Assert.IsTrue(run.termination.IsOngoing, $"Deveria estar em andamento no mês {month}");
                
                // Aplica pequenas variações saudáveis
                run.ApplyStatDelta(StatId.PopularApproval, (month % 2 == 0) ? 2 : -1);
                run.ApplyStatDelta(StatId.Economy, 1);
                run.ApplyPoliticalDelta(1, 0);

                run.AdvanceMonth();
            }

            // Após avançar o 48º mês (chegando ao mês 49)
            Assert.AreEqual(49, run.calendar.currentMonthIndex);
            Assert.IsTrue(run.termination.IsVictory);
            Assert.IsFalse(run.termination.IsOngoing);
        }

        [Test]
        public void CorruptionSpike_AtMonth20_LeadsToDefeat()
        {
            var run = new RunState();

            // Avança até o mês 20
            for (int i = 1; i < 20; i++)
            {
                run.AdvanceMonth();
            }

            Assert.AreEqual(20, run.calendar.currentMonthIndex);
            Assert.IsTrue(run.termination.IsOngoing);

            // Escândalo massivo de corrupção
            run.ApplyStatDelta(StatId.Corruption, 100);

            Assert.IsTrue(run.termination.IsDefeat);
            Assert.IsFalse(run.termination.IsOngoing);
            StringAssert.Contains("Corrupção", run.termination.reason);

            // Tentar avançar o mês após derrota não altera o estado
            run.AdvanceMonth();
            Assert.AreEqual(20, run.calendar.currentMonthIndex);
        }

        [Test]
        public void Snapshot_CapturesStateWithoutDirectCoupling()
        {
            var run = new RunState();
            run.ApplyStatDelta(StatId.Economy, 25);
            run.ApplyPoliticalDelta(5, -4);

            RunSnapshot snapshot = run.GetSnapshot();

            Assert.AreEqual(75, snapshot.Stats.economy);
            Assert.AreEqual(5, snapshot.PoliticalAxis.x);
            Assert.AreEqual(-4, snapshot.PoliticalAxis.y);
            Assert.AreEqual("01/2026", snapshot.DisplayDate);
            Assert.IsTrue(snapshot.IsOngoing);
            Assert.IsFalse(snapshot.IsDefeat);

            // Modificar a run depois não altera o snapshot já emitido
            run.ApplyStatDelta(StatId.Economy, -30);
            Assert.AreEqual(75, snapshot.Stats.economy);
        }

        [Test]
        public void MonthDecisionRecord_AddAndRetrieve_WorksCorrectly()
        {
            var run = new RunState();
            var record1 = new MonthDecisionRecord
            {
                monthIndex = 1,
                cardId = "card_01",
                title = "Reforma Tributária",
                npcName = "Ministro da Fazenda",
                isApproved = true,
                choiceLabel = "Aprovar Projeto",
                stat1Text = "+10 Economia",
                stat1Positive = true
            };

            run.RecordMonthDecision(record1);

            var retrieved = run.GetDecisionForMonth(1);
            Assert.IsNotNull(retrieved);
            Assert.AreEqual("Reforma Tributária", retrieved.title);
            Assert.AreEqual("Ministro da Fazenda", retrieved.npcName);
            Assert.IsTrue(retrieved.isApproved);
            Assert.AreEqual("+10 Economia", retrieved.stat1Text);

            // Substitui caso já exista para o mesmo mês
            var record1Updated = new MonthDecisionRecord
            {
                monthIndex = 1,
                cardId = "card_01_alt",
                title = "Reforma Tributária Atualizada",
                isApproved = false
            };
            run.RecordMonthDecision(record1Updated);
            Assert.AreEqual(1, run.pastDecisions.Count);
            Assert.AreEqual("Reforma Tributária Atualizada", run.GetDecisionForMonth(1).title);
            Assert.IsFalse(run.GetDecisionForMonth(1).isApproved);
        }

        [Test]
        public void RunSaveData_PersistsPastDecisionsCorrectly()
        {
            var run = new RunState();
            run.RecordMonthDecision(new MonthDecisionRecord
            {
                monthIndex = 1,
                title = "Decisão 1",
                isApproved = true
            });
            run.RecordMonthDecision(new MonthDecisionRecord
            {
                monthIndex = 2,
                title = "Festa Corporativa",
                isEvent = true,
                isApproved = true
            });

            var deck = new DeckState();
            var saveData = RunSaveData.FromRuntime(run, deck);

            Assert.IsNotNull(saveData.pastDecisions);
            Assert.AreEqual(2, saveData.pastDecisions.Count);
            Assert.AreEqual("Decisão 1", saveData.pastDecisions[0].title);
            Assert.AreEqual("Festa Corporativa", saveData.pastDecisions[1].title);
            Assert.IsTrue(saveData.pastDecisions[1].isEvent);

            var restoredRun = new RunState();
            saveData.ApplyToRuntime(restoredRun, deck);

            Assert.AreEqual(2, restoredRun.pastDecisions.Count);
            var dec2 = restoredRun.GetDecisionForMonth(2);
            Assert.IsNotNull(dec2);
            Assert.AreEqual("Festa Corporativa", dec2.title);
            Assert.IsTrue(dec2.isEvent);
        }
    }
}
