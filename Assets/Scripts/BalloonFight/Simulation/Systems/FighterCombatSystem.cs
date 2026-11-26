namespace NetworkCourse.BalloonFight.Simulation
{
    public static class FighterCombatSystem
    {
        public static CombatResolution Step(
            ref FighterState fighter0,
            ref FighterState fighter1,
            bool fighter0Active,
            bool fighter1Active,
            uint currentTick,
            in SimulationConfig config)
        {
            AdvanceInvulnerability(ref fighter0, fighter0Active);
            AdvanceInvulnerability(ref fighter1, fighter1Active);

            bool fighter0Hits1 = false;
            bool fighter1Hits0 = false;
            byte fighter1Balloon = byte.MaxValue;
            byte fighter0Balloon = byte.MaxValue;
            if (fighter0Active && fighter1Active)
            {
                fighter0Hits1 = TryFindHitBalloon(
                    in fighter0,
                    in fighter1,
                    in config,
                    out fighter1Balloon);
                fighter1Hits0 = TryFindHitBalloon(
                    in fighter1,
                    in fighter0,
                    in config,
                    out fighter0Balloon);
            }

            if (fighter0Hits1 && fighter1Hits0)
            {
                SimulationEvent clash = new SimulationEvent(
                    currentTick,
                    SimulationEventType.Clash,
                    0,
                    1,
                    byte.MaxValue);
                return CombatResolution.One(
                    CombatResolutionStatus.Clash,
                    in clash);
            }

            SimulationEvent firstEvent = default;
            int eventCount = 0;
            CombatResolutionStatus status = CombatResolutionStatus.NoContact;
            if (fighter0Hits1)
            {
                ApplyBalloonHit(
                    ref fighter1,
                    fighter1Balloon,
                    in config);
                firstEvent = new SimulationEvent(
                    currentTick,
                    SimulationEventType.BalloonPopped,
                    0,
                    1,
                    fighter1Balloon);
                eventCount = 1;
                status = CombatResolutionStatus.BalloonPopped;
            }
            else if (fighter1Hits0)
            {
                ApplyBalloonHit(
                    ref fighter0,
                    fighter0Balloon,
                    in config);
                firstEvent = new SimulationEvent(
                    currentTick,
                    SimulationEventType.BalloonPopped,
                    1,
                    0,
                    fighter0Balloon);
                eventCount = 1;
                status = CombatResolutionStatus.BalloonPopped;
            }

            SimulationEvent secondEvent = default;
            AppendEliminationIfNeeded(
                ref fighter0,
                fighter0Active,
                0,
                currentTick,
                config.YMin,
                ref eventCount,
                ref firstEvent,
                ref secondEvent);
            AppendEliminationIfNeeded(
                ref fighter1,
                fighter1Active,
                1,
                currentTick,
                config.YMin,
                ref eventCount,
                ref firstEvent,
                ref secondEvent);

            if (eventCount == 0) return CombatResolution.None();
            if (eventCount == 1)
            {
                return CombatResolution.One(status, in firstEvent);
            }

            return CombatResolution.Two(
                status,
                in firstEvent,
                in secondEvent);
        }

        public static void ResetForRound(ref FighterState state)
        {
            state.Phase = FighterPhase.Flying;
            state.BalloonMask = FighterState.ValidBalloonMask;
            state.InvulnerabilityTicksRemaining = 0;
        }

        private static bool TryFindHitBalloon(
            in FighterState attacker,
            in FighterState defender,
            in SimulationConfig config,
            out byte balloonIndex)
        {
            balloonIndex = byte.MaxValue;
            if (attacker.Phase != FighterPhase.Flying
                || defender.Phase != FighterPhase.Flying
                || defender.InvulnerabilityTicksRemaining > 0
                || attacker.Position.Y
                    < defender.Position.Y
                        + config.MinimumAttackVerticalAdvantage)
            {
                return false;
            }

            for (byte index = 0; index < 2; index++)
            {
                if (!defender.IsBalloonActive(index)) continue;

                SimVector2 offset = index == 0
                    ? config.Balloon0Offset
                    : config.Balloon1Offset;
                SimVector2 balloonCenter = new SimVector2(
                    defender.Position.X + offset.X,
                    defender.Position.Y + offset.Y);
                if (Overlaps(
                    in attacker.Position,
                    in config.BodyHalfExtents,
                    in balloonCenter,
                    in config.BalloonHalfExtents))
                {
                    balloonIndex = index;
                    return true;
                }
            }

            return false;
        }

        private static void ApplyBalloonHit(
            ref FighterState defender,
            byte balloonIndex,
            in SimulationConfig config)
        {
            byte bit = balloonIndex == 0
                ? FighterState.Balloon0Bit
                : FighterState.Balloon1Bit;
            defender.BalloonMask = (byte)(defender.BalloonMask & ~bit);
            defender.InvulnerabilityTicksRemaining =
                config.HitInvulnerabilityTicks;
            if (defender.BalloonMask == 0)
            {
                defender.Phase = FighterPhase.Falling;
            }
        }

        private static void AppendEliminationIfNeeded(
            ref FighterState fighter,
            bool isActive,
            byte slot,
            uint currentTick,
            float floorY,
            ref int eventCount,
            ref SimulationEvent event0,
            ref SimulationEvent event1)
        {
            if (!isActive
                || fighter.Phase != FighterPhase.Falling
                || fighter.Position.Y > floorY)
            {
                return;
            }

            fighter.Phase = FighterPhase.Eliminated;
            SimulationEvent eliminated = new SimulationEvent(
                currentTick,
                SimulationEventType.FighterEliminated,
                slot,
                slot,
                byte.MaxValue);
            if (eventCount == 0) event0 = eliminated;
            else event1 = eliminated;
            eventCount++;
        }

        private static void AdvanceInvulnerability(
            ref FighterState fighter,
            bool isActive)
        {
            if (isActive && fighter.InvulnerabilityTicksRemaining > 0)
            {
                fighter.InvulnerabilityTicksRemaining--;
            }
        }

        private static bool Overlaps(
            in SimVector2 centerA,
            in SimVector2 halfA,
            in SimVector2 centerB,
            in SimVector2 halfB)
        {
            return Abs(centerA.X - centerB.X) < halfA.X + halfB.X
                && Abs(centerA.Y - centerB.Y) < halfA.Y + halfB.Y;
        }

        private static float Abs(float value)
        {
            return value < 0f ? -value : value;
        }
    }
}