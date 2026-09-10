using NUnit.Framework;
using Mandato.Core;
using Mandato.Run;

namespace Mandato.Run.Tests
{
    public class RunRulesTests
    {
        [Test]
        public void NormalStats_ResultInOngoingStatus()
        {
            var stats = new StatBlock(50, 50, 50, 50, 0);
            var calendar = new RunCalendar(1);

            var result = RunRules.Evaluate(stats, calendar);

            Assert.AreEqual(RunStatus.Ongoing, result.status);
            Assert.IsTrue(result.IsOngoing);
        }

        [Test]
        public void ZeroEconomy_TriggersDefeat()
        {
            var stats = new StatBlock(50, 50, 50, 0, 0);
            var calendar = new RunCalendar(10);

            var result = RunRules.Evaluate(stats, calendar);

            Assert.AreEqual(RunStatus.Defeat, result.status);
            Assert.IsTrue(result.IsDefeat);
            StringAssert.Contains("Econômico", result.reason);
        }

        [Test]
        public void MaxCorruption_TriggersDefeat()
        {
            var stats = new StatBlock(50, 50, 50, 50, 100);
            var calendar = new RunCalendar(15);

            var result = RunRules.Evaluate(stats, calendar);

            Assert.AreEqual(RunStatus.Defeat, result.status);
            Assert.IsTrue(result.IsDefeat);
            StringAssert.Contains("Corrupção", result.reason);
        }

        [Test]
        public void Completing48Months_TriggersVictory()
        {
            var stats = new StatBlock(50, 50, 50, 50, 20);
            var calendar = new RunCalendar(49); // mês 49 significa que o mês 48 foi concluído

            var result = RunRules.Evaluate(stats, calendar);

            Assert.AreEqual(RunStatus.Victory, result.status);
            Assert.IsTrue(result.IsVictory);
        }
    }
}
