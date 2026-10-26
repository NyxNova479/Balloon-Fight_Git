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

        public FighterState Fighter0 => Fighters.Get(0);
        public FighterState Fighter1 => Fighters.Get(1);

        public bool IsSlotActive(int slotIndex)
        {
            if(slotIndex == 0) return (ActiveMask & Fighter0Bit) != 0;
            if(slotIndex == 1) return (ActiveMask & Fighter1Bit) != 0;
            return false;
        }

        public SlotOperationStatus TryActivateSlot(
            int slotIndex,
            in SimVector2 spawnPosition)
        {
            if (slotIndex < 0 || slotIndex >= FighterCapacity)
            {
                return SlotOperationStatus.InvalidSlot;
            }

            byte bit = slotIndex == 0 ? Fighter0Bit : Fighter1Bit;

            if ((ActiveMask & bit) != 0)
            {
                return SlotOperationStatus.AlreadyActive;
            }

            FighterState fighter = FighterState.CreateAtSpawn(
                in spawnPosition);
            Fighters.Set(slotIndex, in fighter);

            ActiveMask = (byte)((ActiveMask | bit) & ValidActiveMask);
            return SlotOperationStatus.Success;
        }

        public static SimulationState CreateTwoFighters(in SimVector2 fighter0Position, in SimVector2 fighter1Position)
        {
            SimulationState state = new SimulationState
            {
                NextTick = 0u,
                ActiveMask = 0,
                Fighters = default,
                Match = MatchState.CreatePlaying()
            };

            state.TryActivateSlot(0, in fighter0Position);
            state.TryActivateSlot(1, in fighter1Position);
            return state;

        }

        public static SimulationState CreateSingleFighter(in SimVector2 fighter0Position)
        {
            SimulationState state = new SimulationState
            {
                NextTick = 0u,
                ActiveMask = 0,
                Fighters = default,
                Match = MatchState.CreatePlaying()
            };

            state.TryActivateSlot(0, in fighter0Position);
            return state;

        }


    }


}