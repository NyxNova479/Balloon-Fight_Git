namespace NetworkCourse.BalloonFight.Simulation
{
    public readonly struct SimulationConfig
    {
        public const float RequiredTickDeltaTime = 0.02f;

        public readonly float TickDeltaTime;
        public readonly float GravityAcceleration;
        public readonly float LiftAcceleration;
        public readonly float OneBalloonLiftMultiplier;
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
        public readonly SimVector2 BodyHalfExtents;
        public readonly SimVector2 BalloonHalfExtents;
        public readonly SimVector2 Balloon0Offset;
        public readonly SimVector2 Balloon1Offset;
        public readonly float MinimumAttackVerticalAdvantage;
        public readonly ushort HitInvulnerabilityTicks;

        public SimulationConfig(
            float tickDeltaTime,
            float gravityAcceleration,
            float liftAcceleration,
            float oneBalloonLiftMultiplier,
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
            in SimVector2 fighter1Spawn,
            in SimVector2 bodyHalfExtents,
            in SimVector2 balloonHalfExtents,
            in SimVector2 balloon0Offset,
            in SimVector2 balloon1Offset,
            float minimumAttackVerticalAdvantage,
            ushort hitInvulnerabilityTicks)
        {
            TickDeltaTime = tickDeltaTime;
            GravityAcceleration = gravityAcceleration;
            LiftAcceleration = liftAcceleration;
            OneBalloonLiftMultiplier = oneBalloonLiftMultiplier;
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
            BodyHalfExtents = bodyHalfExtents;
            BalloonHalfExtents = balloonHalfExtents;
            Balloon0Offset = balloon0Offset;
            Balloon1Offset = balloon1Offset;
            MinimumAttackVerticalAdvantage = minimumAttackVerticalAdvantage;
            HitInvulnerabilityTicks = hitInvulnerabilityTicks;
        }

        public static SimulationConfig CreateReference()
        {
            SimVector2 fighter0Spawn = new SimVector2(-4.5f, 0f);
            SimVector2 fighter1Spawn = new SimVector2(4.5f, 0f);
            SimVector2 bodyHalfExtents = new SimVector2(0.35f, 0.45f);
            SimVector2 balloonHalfExtents = new SimVector2(0.28f, 0.32f);
            SimVector2 balloon0Offset = new SimVector2(-0.32f, 0.72f);
            SimVector2 balloon1Offset = new SimVector2(0.32f, 0.72f);
            return new SimulationConfig(
                RequiredTickDeltaTime,
                18f,
                13f,
                0.65f,
                4.5f,
                0.18f,
                5f,
                18f,
                10f,
                7f,
                8f,
                -7f,
                7f,
                -4f,
                4f,
                in fighter0Spawn,
                in fighter1Spawn,
                in bodyHalfExtents,
                in balloonHalfExtents,
                in balloon0Offset,
                in balloon1Offset,
                0.2f,
                25);
        }

        public bool IsValid()
        {
            return IsFinite(TickDeltaTime)
                && TickDeltaTime == RequiredTickDeltaTime
                && IsNonNegative(GravityAcceleration)
                && IsNonNegative(LiftAcceleration)
                && IsFinite(OneBalloonLiftMultiplier)
                && OneBalloonLiftMultiplier >= 0f
                && OneBalloonLiftMultiplier <= 1f
                && IsNonNegative(FlapImpulse)
                && IsNonNegative(FlapCooldown)
                && IsNonNegative(MaxHorizontalSpeed)
                && IsNonNegative(HorizontalAcceleration)
                && IsNonNegative(HorizontalDrag)
                && IsNonNegative(MaxRiseSpeed)
                && IsNonNegative(MaxFallSpeed)
                && IsFinite(XMin) && IsFinite(XMax) && XMin < XMax
                && IsFinite(YMin) && IsFinite(YMax) && YMin < YMax
                && IsInsideArena(in Fighter0Spawn)
                && IsInsideArena(in Fighter1Spawn)
                && IsPositiveExtents(in BodyHalfExtents)
                && IsPositiveExtents(in BalloonHalfExtents)
                && IsFiniteVector(in Balloon0Offset)
                && IsFiniteVector(in Balloon1Offset)
                && IsNonNegative(MinimumAttackVerticalAdvantage)
                && HitInvulnerabilityTicks > 0;
        }

        private bool IsInsideArena(in SimVector2 position)
        {
            return IsFiniteVector(in position)
                && position.X >= XMin && position.X <= XMax
                && position.Y >= YMin && position.Y <= YMax;
        }

        private static bool IsPositiveExtents(in SimVector2 value)
        {
            return IsFiniteVector(in value)
                && value.X > 0f
                && value.Y > 0f;
        }

        private static bool IsFiniteVector(in SimVector2 value)
        {
            return IsFinite(value.X) && IsFinite(value.Y);
        }

        private static bool IsNonNegative(float value)
        {
            return IsFinite(value) && value >= 0f;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}