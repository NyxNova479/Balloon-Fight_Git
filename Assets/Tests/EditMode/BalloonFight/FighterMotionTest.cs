using NUnit.Framework;
using NetworkCourse.BalloonFight.Simulation;

namespace NetworkCourse.BalloonFight.Tests
{
    public sealed class FighterMotionTest
    {
        [Test]
        public void FlapPressAppliesOnImpulse()
        {

            SimulationConfig config = SimulationConfig.CreateReference();
            FighterState state = FighterState.CreateActive(
                new SimVector2(0f,0f));
            FighterInput flapPress = new FighterInput(0u, 0f, true);

            FighterMotionSystem.Step(ref state, in flapPress, in config);
            float velocityAfterPress = state.Velocity.Y;

            FighterInput nextTick = FighterInput.Neutral(1u);
            FighterMotionSystem.Step(ref state, in nextTick, in config);

            Assert.That(velocityAfterPress, Is.EqualTo(4.4f).Within(0.0001f));
            Assert.That(state.Velocity.Y, Is.EqualTo(4.3f).Within(0.0001f));
            Assert.That(state.FlapCooldownRemaining, Is.GreaterThan(0f));

        }

        [Test]
        public void HorizontalVelocityReturnsTowardZero()
        {
            SimulationConfig config = SimulationConfig.CreateReference();
            FighterState state = FighterState.CreateActive(
                new SimVector2(0f, 0f));
            state.Velocity = new SimVector2(3f, 0f);
            FighterInput input = FighterInput.Neutral(0u);

            FighterMotionSystem.Step(ref state, in input, in config);

            Assert.That(state.Velocity.X, Is.GreaterThanOrEqualTo(0f));
            Assert.That(state.Velocity.X, Is.LessThan(3f));
        }

        [Test]
        public void ArenaBoundaryStopsOutwardVelocity()
        {
            SimulationConfig config = SimulationConfig.CreateReference();
            FighterState state = FighterState.CreateActive(
                new SimVector2(config.XMax, 0f));
            state.Velocity = new SimVector2(2f, 0f);
            FighterInput input = new FighterInput(0u, 1f, false);

            FighterMotionSystem.Step(ref state, in input, in config);

            Assert.That(state.Position.X, Is.EqualTo(config.XMax));
            Assert.That(state.Velocity.X, Is.EqualTo(0f));
        }

        [Test]
        public void TickMismatchDoesNotMutateState()
        {
            SimulationConfig config = SimulationConfig.CreateReference();
            SimulationState initial = SimulationState.CreateSingleFighter(
                new SimVector2(0f, 0f));
            BalloonSimulation simulation = new BalloonSimulation(
                in config,
                in initial);
            FighterInput wrong = FighterInput.Neutral(1u);
            FighterInput expected = FighterInput.Neutral(0u);

            SimulationResult result = simulation.Step(in wrong, in expected);
            SimulationState after = simulation.CurrentState;

            Assert.That(result.Status, Is.EqualTo(SimulationStatus.TickMismatch));
            Assert.That(result.HasProcessedTick, Is.False);
            Assert.That(after.NextTick, Is.EqualTo(0u));
            Assert.That(after.Fighter0.Position.X, Is.EqualTo(0f));
            Assert.That(after.Fighter0.Position.Y, Is.EqualTo(0f));
        }

        [Test]
        public void SameInputsProduceSameState()
        {
            SimulationConfig config = SimulationConfig.CreateReference();
            SimulationState initial = SimulationState.CreateSingleFighter(
                new SimVector2(0f, 0f));
            BalloonSimulation first = new BalloonSimulation(in config, in initial);
            BalloonSimulation second = new BalloonSimulation(in config, in initial);

            for (uint tick = 0u; tick < 300u; tick++)
            {
                float horizontal = tick < 100u ? 1f : tick < 200u ? 0f : -1f;
                bool flapPressed = tick % 17u == 0u;
                FighterInput input = new FighterInput(
                    tick,
                    horizontal,
                    flapPressed);
                FighterInput neutral = FighterInput.Neutral(tick);

                SimulationResult firstResult = first.Step(in input, in neutral);
                SimulationResult secondResult = second.Step(in input, in neutral);

                Assert.That(firstResult.Succeeded, Is.True);
                Assert.That(secondResult.Succeeded, Is.True);
            }

            SimulationState firstState = first.CurrentState;
            SimulationState secondState = second.CurrentState;

            Assert.That(firstState.NextTick, Is.EqualTo(secondState.NextTick));
            Assert.That(
                firstState.Fighter0.Position.X,
                Is.EqualTo(secondState.Fighter0.Position.X));
            Assert.That(
                firstState.Fighter0.Position.Y,
                Is.EqualTo(secondState.Fighter0.Position.Y));
            Assert.That(
                firstState.Fighter0.Velocity.X,
                Is.EqualTo(secondState.Fighter0.Velocity.X));
            Assert.That(
                firstState.Fighter0.Velocity.Y,
                Is.EqualTo(secondState.Fighter0.Velocity.Y));
            Assert.That(
                firstState.Fighter0.FlapCooldownRemaining,
                Is.EqualTo(secondState.Fighter0.FlapCooldownRemaining));

        }

    }


}

