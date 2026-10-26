namespace NetworkCourse.BalloonFight.Simulation
{

    public enum SlotOperationStatus
    {
        Success = 0,
        InvalidSlot = 1,
        AlreadyActive = 2
    }

    public struct SimulationState
    {

        public const int FighterCapacity = FighterStateCollection.Capacity;
        public const byte Fighter0Bit = 1 << 0;
        public const byte Fighter1Bit = 1 << 1;
        public const byte ValidActiveMask = Fighter0Bit | Fighter1Bit;

        public uint NextTick;

        public byte ActiveMask;
        public FighterStateCollection Fighters;
        public MatchState Match;

        public FighterState Fighter0;
        public FighterState Fighter1;

        public static SimulationState CreateSingleFighter(in SimVector2 fighter0Position)
        {
            SimulationState state = new SimulationState
            {
                NextTick = 0u,
                ActiveMask = 0,
                Fighters = default,
                Match = MatchState.CreatePlaying()
            };

            //TODO : A finir
            return state;

        }


    }


}