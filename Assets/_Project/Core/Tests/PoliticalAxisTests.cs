using Mandato.Core;
using NUnit.Framework;

namespace Mandato.Core.Tests
{
    public class PoliticalAxisTests
    {
        [Test]
        public void DefaultConstructor_InitializesAtCenterAndUnlocked()
        {
            var axis = new PoliticalAxis();
            Assert.AreEqual(0, axis.x);
            Assert.AreEqual(0, axis.y);
            Assert.IsFalse(axis.isLocked);
            Assert.AreEqual("Centro", axis.Quadrant);
        }

        [Test]
        public void ApplyDelta_ChangesCoordinates_WhenUnlocked()
        {
            var axis = new PoliticalAxis(0, 0);
            axis.ApplyDelta(4, -5);

            Assert.AreEqual(4, axis.x);
            Assert.AreEqual(-5, axis.y);
            Assert.AreEqual("Direita Liberal", axis.Quadrant);
        }

        [Test]
        public void ApplyDelta_DoesNotChangeCoordinates_WhenLocked()
        {
            var axis = new PoliticalAxis(3, 3);
            axis.Lock();

            Assert.IsTrue(axis.isLocked);
            axis.ApplyDelta(2, -4);

            Assert.AreEqual(3, axis.x);
            Assert.AreEqual(3, axis.y);
            Assert.AreEqual("Direita Autoritária", axis.Quadrant);

            axis.Unlock();
            axis.ApplyDelta(2, -4);
            Assert.AreEqual(5, axis.x);
            Assert.AreEqual(-1, axis.y);
        }

        [Test]
        public void Coordinates_AreClampedToLimits()
        {
            var axis = new PoliticalAxis(100, -100);
            Assert.AreEqual(PoliticalAxis.MaxValue, axis.x);
            Assert.AreEqual(PoliticalAxis.MinValue, axis.y);

            axis.ApplyDelta(5, -5);
            Assert.AreEqual(PoliticalAxis.MaxValue, axis.x);
            Assert.AreEqual(PoliticalAxis.MinValue, axis.y);
        }

        [Test]
        public void Quadrants_EvaluateCorrectly()
        {
            Assert.AreEqual("Centro", new PoliticalAxis(1, -1).Quadrant);
            Assert.AreEqual("Esquerda Autoritária", new PoliticalAxis(-5, 5).Quadrant);
            Assert.AreEqual("Esquerda Libertária", new PoliticalAxis(-5, -5).Quadrant);
            Assert.AreEqual("Centro-Esquerda", new PoliticalAxis(-5, 0).Quadrant);
            Assert.AreEqual("Direita Autoritária", new PoliticalAxis(5, 5).Quadrant);
            Assert.AreEqual("Direita Liberal", new PoliticalAxis(5, -5).Quadrant);
            Assert.AreEqual("Centro-Direita", new PoliticalAxis(5, 0).Quadrant);
        }

        [Test]
        public void IsInRange_EvaluatesBounds()
        {
            var axis = new PoliticalAxis(3, -2);
            Assert.IsTrue(axis.IsInRange(0, 5, -5, 0));
            Assert.IsFalse(axis.IsInRange(5, 10, -5, 0));
        }

        [Test]
        public void Clone_CopiesStateAndLock()
        {
            var axis = new PoliticalAxis(-4, 6, locked: true);
            var clone = axis.Clone();

            Assert.AreEqual(-4, clone.x);
            Assert.AreEqual(6, clone.y);
            Assert.IsTrue(clone.isLocked);
        }

        [Test]
        public void MoveTowards_MovesByFixedStepTowardsTarget()
        {
            var axis = new PoliticalAxis(0, 0);

            var (dx, dy) = axis.MoveTowards(targetX: 6, targetY: 2, step: PoliticalAxis.DefaultProposalStep);
            Assert.AreEqual(2, dx);
            Assert.AreEqual(2, dy);
            Assert.AreEqual(2, axis.x);
            Assert.AreEqual(2, axis.y);

            // Segunda movimentação
            var (dx2, dy2) = axis.MoveTowards(targetX: 6, targetY: 2, step: PoliticalAxis.DefaultProposalStep);
            Assert.AreEqual(2, dx2);
            Assert.AreEqual(0, dy2); // já estava em 2, não ultrapassa!
            Assert.AreEqual(4, axis.x);
            Assert.AreEqual(2, axis.y);
        }

        [Test]
        public void MoveTowards_DoesNotOvershootTarget()
        {
            var axis = new PoliticalAxis(5, 1);

            // Alvo a 1 de distância, mas step é 2
            var (dx, dy) = axis.MoveTowards(targetX: 6, targetY: 2, step: 2);
            Assert.AreEqual(1, dx);
            Assert.AreEqual(1, dy);
            Assert.AreEqual(6, axis.x);
            Assert.AreEqual(2, axis.y);

            // Já no alvo: delta deve ser 0
            var (dx2, dy2) = axis.MoveTowards(targetX: 6, targetY: 2, step: 2);
            Assert.AreEqual(0, dx2);
            Assert.AreEqual(0, dy2);
            Assert.AreEqual(6, axis.x);
            Assert.AreEqual(2, axis.y);
        }

        [Test]
        public void MoveTowards_DoesNothing_WhenLocked()
        {
            var axis = new PoliticalAxis(0, 0);
            axis.Lock();

            var (dx, dy) = axis.MoveTowards(targetX: 6, targetY: 2, step: 2);
            Assert.AreEqual(0, dx);
            Assert.AreEqual(0, dy);
            Assert.AreEqual(0, axis.x);
            Assert.AreEqual(0, axis.y);
        }

        [Test]
        public void MoveTowards_NegativeDirection_MovesCorrectly()
        {
            var axis = new PoliticalAxis(3, 4);

            var (dx, dy) = axis.MoveTowards(targetX: -3, targetY: -2, step: 1);
            Assert.AreEqual(-1, dx);
            Assert.AreEqual(-1, dy);
            Assert.AreEqual(2, axis.x);
            Assert.AreEqual(3, axis.y);
        }
    }
}
