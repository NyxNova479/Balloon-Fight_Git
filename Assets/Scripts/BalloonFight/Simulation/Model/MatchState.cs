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

        public const byte NoWinner = byte.MaxValue;

        public MatchPhase Phase;
        public uint RoundIndex;
        public uint ResetApplyTick;
        public uint Fighter0Score;
        public uint Fighter1Score;
        public byte WinnerSlot;
        public bool HasLastRoundResult;
        public byte LastRoundWinnerSlot;

        public static MatchState CreatePlaying()
        {
            return new MatchState
            {
                Phase = MatchPhase.Playing,
                RoundIndex = 0u,
                ResetApplyTick = 0u,
                Fighter0Score = 0u,
                Fighter1Score = 0u,
                WinnerSlot = NoWinner,
                HasLastRoundResult = false,
                LastRoundWinnerSlot = NoWinner
            };
        }


    }


}