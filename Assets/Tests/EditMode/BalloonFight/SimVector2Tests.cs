using NUnit.Framework;
using NetworkCourse.BalloonFight.Simulation;

namespace NetworkCourse.BalloonFight.Tests
{
    public sealed class SimVector2Tests
    {
        [Test]
        public void Constructor_StoresComponents()
        {
            var value = new SimVector2(2.5f, -4f);

            Assert.That(value.X, Is.EqualTo(2.5f));
            Assert.That(value.Y, Is.EqualTo(-4f));
        }

        [Test]
        public void Add_ReturnsComponentWiseSum()
        {
            var left = new SimVector2(2f, -1f);
            var right = new SimVector2(-3f, 5f);

            SimVector2 result = SimVector2.Add(in left, in right);

            Assert.That(result.X, Is.EqualTo(-1f));
            Assert.That(result.Y, Is.EqualTo(4f));
        }

        [Test]
        public void LengthSquared_DoesNotTakeSquareRoot()
        {
            var value = new SimVector2(3f, 4f);

            Assert.That(value.LengthSquared(), Is.EqualTo(25f));
        }
    }
}
