using NUnit.Framework;
using Mandato.Core;

namespace Mandato.Core.Tests
{
    public class StatBlockTests
    {
        [Test]
        public void Defaults_AreCorrect()
        {
            var stats = new StatBlock();

            Assert.AreEqual(50, stats.climaticChanges);
            Assert.AreEqual(50, stats.internationalRelations);
            Assert.AreEqual(50, stats.popularApproval);
            Assert.AreEqual(50, stats.economy);
            Assert.AreEqual(0, stats.corruption);
        }

        [Test]
        public void Clamping_DoesNotExceedBounds()
        {
            var stats = new StatBlock(150, -30, 200, -10, 120);

            Assert.AreEqual(100, stats.climaticChanges);
            Assert.AreEqual(0, stats.internationalRelations);
            Assert.AreEqual(100, stats.popularApproval);
            Assert.AreEqual(0, stats.economy);
            Assert.AreEqual(100, stats.corruption);
        }

        [Test]
        public void ApplyDelta_ModifiesStatsWithClamping()
        {
            var stats = new StatBlock();
            stats.ApplyDelta(StatId.Economy, -60);
            stats.ApplyDelta(StatId.Corruption, 70);

            Assert.AreEqual(0, stats.economy);
            Assert.AreEqual(70, stats.corruption);
        }

        [Test]
        public void Clone_CreatesIndependentCopy()
        {
            var original = new StatBlock(40, 60, 70, 80, 10);
            var clone = original.Clone();

            clone.economy = 100;

            Assert.AreEqual(80, original.economy);
            Assert.AreEqual(100, clone.economy);
        }
    }
}
