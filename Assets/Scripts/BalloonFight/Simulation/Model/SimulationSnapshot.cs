namespace NetworkCourse.BalloonFight.Simulation
{
    public readonly struct SimulationSnapshot
    {
        private readonly byte _activeMask;
        private readonly FighterStateCollection _fighters;
        private readonly MatchState _match;

        public uint Tick { get; }
        public uint NextTick { get; }
        public byte ActiveMask => _activeMask;
        public FighterStateCollection Fighters => _fighters;
        public FighterState Fighter0 => _fighters.Get(0);
        public FighterState Fighter1 => _fighters.Get(1);
        public MatchState Match => _match;

        private SimulationSnapshot(
            uint tick,
            uint nextTick,
            byte activeMask,
            in FighterStateCollection fighters,
            in MatchState match)
        {
            Tick = tick;
            NextTick = nextTick;
            _activeMask = (byte)(activeMask & SimulationState.ValidActiveMask);
            _fighters = fighters;
            _match = match;
        }

        public bool IsSlotActive(int slotIndex)
        {
            if (slotIndex == 0)
            {
                return (_activeMask & SimulationState.Fighter0Bit) != 0;
            }

            if (slotIndex == 1)
            {
                return (_activeMask & SimulationState.Fighter1Bit) != 0;
            }

            return false;
        }

        public static SimulationSnapshot Capture(
            uint completedTick,
            in SimulationState state)
        {
            FighterStateCollection fighters = state.Fighters;
            return new SimulationSnapshot(
                completedTick,
                state.NextTick,
                state.ActiveMask,
                in fighters,
                in state.Match);
        }
    }
}