
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

        public readonly SimVector2 Fighter0Spawn;
        public readonly SimVector2 Fighter1Spawn;

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
            float yMax,
            in SimVector2 fighter0Spawn,
            in SimVector2 fighter1Spawn)
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
            Fighter0Spawn = fighter0Spawn;
            Fighter1Spawn = fighter1Spawn;
        }

        public static SimulationConfig CreateReference()
        {

            SimVector2 fighter0Spawn = new SimVector2(-4.5f, 0f);
            SimVector2 fighter1Spawn = new SimVector2(4.5f, 0f);

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
                yMax: 4f,
                fighter0Spawn : in fighter0Spawn,
                fighter1Spawn : in fighter1Spawn);
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
                && IsFinite(YMin) && IsFinite(YMax) && YMin < YMax
                && IsInsideArena(in Fighter0Spawn)
                && IsInsideArena(in Fighter1Spawn);
        }

        private bool IsInsideArena(in SimVector2 position)
        {
            return IsFinite(position.X) && IsFinite(position.Y) && position.X >= XMin && position.X <= XMax && position.Y >= YMin && position.Y <= YMax;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}