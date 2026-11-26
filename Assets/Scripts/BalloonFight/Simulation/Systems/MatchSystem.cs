namespace NetworkCourse.BalloonFight.Simulation
{
    public static class MatchSystem
    {
        public static bool ShouldApplyRoundReset(
            in MatchState state,
            uint currentTick)
        {
            return state.Phase == MatchPhase.RoundOver
                && state.ResetApplyTick == currentTick;
        }

        public static MatchResolution Step(
            ref MatchState state,
            in FighterState fighter0,
            in FighterState fighter1,
            bool fighter0Active,
            bool fighter1Active,
            uint currentTick)
        {
            if (state.Phase != MatchPhase.Playing)
            {
                SimulationEvent none = default;
                return new MatchResolution(false, false, in none);
            }

            bool fighter0Eliminated = fighter0Active
                && fighter0.Phase == FighterPhase.Eliminated;
            bool fighter1Eliminated = fighter1Active
                && fighter1.Phase == FighterPhase.Eliminated;
            if (!fighter0Eliminated && !fighter1Eliminated)
            {
                SimulationEvent none = default;
                return new MatchResolution(false, false, in none);
            }

            byte winner = MatchState.NoWinner;
            SimulationEventType eventType = SimulationEventType.RoundDraw;
            if (fighter0Eliminated && !fighter1Eliminated)
            {
                winner = 1;
                state.Fighter1Score = IncrementSaturated(
                    state.Fighter1Score);
                eventType = SimulationEventType.RoundWon;
            }
            else if (fighter1Eliminated && !fighter0Eliminated)
            {
                winner = 0;
                state.Fighter0Score = IncrementSaturated(
                    state.Fighter0Score);
                eventType = SimulationEventType.RoundWon;
            }

            state.Phase = MatchPhase.RoundOver;
            state.WinnerSlot = winner;
            state.HasLastRoundResult = true;
            state.LastRoundWinnerSlot = winner;
            state.ResetApplyTick = unchecked(currentTick + 1u);
            SimulationEvent matchEvent = new SimulationEvent(
                currentTick,
                eventType,
                winner,
                winner,
                byte.MaxValue);
            return new MatchResolution(true, true, in matchEvent);
        }

        public static void ApplyRoundReset(ref MatchState state)
        {
            state.Phase = MatchPhase.Playing;
            state.RoundIndex = unchecked(state.RoundIndex + 1u);
            state.ResetApplyTick = 0u;
            state.WinnerSlot = MatchState.NoWinner;
        }

        private static uint IncrementSaturated(uint value)
        {
            return value == uint.MaxValue ? value : value + 1u;
        }
    }
}