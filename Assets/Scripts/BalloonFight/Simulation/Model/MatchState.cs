namespace NetworkCourse.BalloonFight.Simulation
{

    public enum MatchPhase
    {
        WaitingForPlayers = 0,
        Countdown = 1,
        Playing = 2,
        RoundOver = 3,
        Resetting = 4
    }


    public struct MatchState
    {

        public MatchPhase Phase;
        public uint RoundIndex;
        public uint ResetApplyTick;

        public static MatchState CreatePlaying()
        {
            return new MatchState
            {
                Phase = MatchPhase.Playing,
                RoundIndex = 0u,
                ResetApplyTick = 0u
            };
        }


    }


}