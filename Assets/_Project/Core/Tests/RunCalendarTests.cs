using NUnit.Framework;
using Mandato.Core;

namespace Mandato.Core.Tests
{
    public class RunCalendarTests
    {
        [Test]
        public void InitialDate_IsJanuary2026()
        {
            var calendar = new RunCalendar();

            Assert.AreEqual(1, calendar.currentMonthIndex);
            Assert.AreEqual(1, calendar.MonthInYear);
            Assert.AreEqual(2026, calendar.Year);
            Assert.AreEqual("Janeiro", calendar.MonthName);
            Assert.AreEqual("Janeiro 2026", calendar.DisplayDate);
            Assert.IsFalse(calendar.IsLastMonth);
            Assert.IsFalse(calendar.IsTermCompleted);
        }

        [Test]
        public void Month12_IsDecember2026()
        {
            var calendar = new RunCalendar(12);

            Assert.AreEqual(12, calendar.MonthInYear);
            Assert.AreEqual(2026, calendar.Year);
            Assert.AreEqual("Dezembro 2026", calendar.DisplayDate);
        }

        [Test]
        public void Month13_IsJanuary2027()
        {
            var calendar = new RunCalendar(13);

            Assert.AreEqual(1, calendar.MonthInYear);
            Assert.AreEqual(2027, calendar.Year);
            Assert.AreEqual("Janeiro 2027", calendar.DisplayDate);
        }

        [Test]
        public void Month48_IsDecember2029_AndIsLastMonth()
        {
            var calendar = new RunCalendar(48);

            Assert.AreEqual(12, calendar.MonthInYear);
            Assert.AreEqual(2029, calendar.Year);
            Assert.AreEqual("Dezembro 2029", calendar.DisplayDate);
            Assert.IsTrue(calendar.IsLastMonth);
            Assert.IsFalse(calendar.IsTermCompleted);
        }

        [Test]
        public void AdvancingPast48_MarksTermCompleted()
        {
            var calendar = new RunCalendar(48);
            calendar.Advance();

            Assert.AreEqual(49, calendar.currentMonthIndex);
            Assert.IsTrue(calendar.IsTermCompleted);
        }
    }
}
