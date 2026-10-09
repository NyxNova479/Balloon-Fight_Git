
namespace NetworkCourse.BalloonFight.Simulation
{
    public readonly struct SimulationConfig
    {
        public const float RequiredTickDeltaTime = 0.02f;

        public readonly float TickDeltaTime;
        public readonly float GravityAcceleration;
        public readonly float LiftAcceleration;
        public readonly float FlapImpulse;
        public readonly float FlapCooldown;
        public readonly float MaxHorizontalSpeed;
        public readonly float HorizontalAcceleration;
        public readonly float HorizontalDrag;
        public readonly float MaxRiseSpeed;
        public readonly float MaxFallSpeed;
        public readonly float XMin;
        public readonly float XMax;
        public readonly float YMin;
        public readonly float YMax;

        public SimulationConfig(
            float tickDeltaTime,
            float gravityAcceleration,
            float liftAcceleration,
            float flapImpulse,
            float flapCooldown,
            float maxHorizontalSpeed,
            float horizontalAcceleration,
            float horizontalDrag,
            float maxRiseSpeed,
            float maxFallSpeed,
            float xMin,
            float xMax,
            float yMin,
            float yMax)
        {
            TickDeltaTime = tickDeltaTime;
            GravityAcceleration = gravityAcceleration;
            LiftAcceleration = liftAcceleration;
            FlapImpulse = flapImpulse;
            FlapCooldown = flapCooldown;
            MaxHorizontalSpeed = maxHorizontalSpeed;
            HorizontalAcceleration = horizontalAcceleration;
            HorizontalDrag = horizontalDrag;
            MaxRiseSpeed = maxRiseSpeed;
            MaxFallSpeed = maxFallSpeed;
            XMin = xMin;
            XMax = xMax;
            YMin = yMin;
            YMax = yMax;
        }

        public static SimulationConfig CreateReference()
        {
            return new SimulationConfig(
                tickDeltaTime: RequiredTickDeltaTime,
                gravityAcceleration: 18f,
                liftAcceleration: 13f,
                flapImpulse: 4.5f,
                flapCooldown: 0.18f,
                maxHorizontalSpeed: 5f,
                horizontalAcceleration: 18f,
                horizontalDrag: 10f,
                maxRiseSpeed: 7f,
                maxFallSpeed: 8f,
                xMin: -7f,
                xMax: 7f,
                yMin: -4f,
                yMax: 4f);
        }

        public bool IsValid()
        {
            return IsFinite(TickDeltaTime)
                && TickDeltaTime == RequiredTickDeltaTime
                && IsFinite(GravityAcceleration) && GravityAcceleration >= 0f
                && IsFinite(LiftAcceleration) && LiftAcceleration >= 0f
                && IsFinite(FlapImpulse) && FlapImpulse >= 0f
                && IsFinite(FlapCooldown) && FlapCooldown >= 0f
                && IsFinite(MaxHorizontalSpeed) && MaxHorizontalSpeed >= 0f
                && IsFinite(HorizontalAcceleration) && HorizontalAcceleration >= 0f
                && IsFinite(HorizontalDrag) && HorizontalDrag >= 0f
                && IsFinite(MaxRiseSpeed) && MaxRiseSpeed >= 0f
                && IsFinite(MaxFallSpeed) && MaxFallSpeed >= 0f
                && IsFinite(XMin) && IsFinite(XMax) && XMin < XMax
                && IsFinite(YMin) && IsFinite(YMax) && YMin < YMax;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}