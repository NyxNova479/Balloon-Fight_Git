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

        public static void Step(
            ref MatchState state,
            uint currentTick,
            bool debugRoundOverRequested)
        {
            if (state.Phase == MatchPhase.Playing
                && debugRoundOverRequested)
            {
                state.Phase = MatchPhase.RoundOver;
                state.ResetApplyTick = unchecked(currentTick + 1u);
                return;
            }

            if (state.Phase == MatchPhase.RoundOver
                && state.ResetApplyTick == currentTick)
            {
                state.Phase = MatchPhase.Resetting;
                return;
            }

            if (state.Phase == MatchPhase.Resetting)
            {
                state.Phase = MatchPhase.Playing;
                state.RoundIndex = unchecked(state.RoundIndex + 1u);
                state.ResetApplyTick = 0u;
            }
        }
    }
}