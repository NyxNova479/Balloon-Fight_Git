namespace NetworkCourse.BalloonFight.Simulation
{
    public enum CombatResolutionStatus : byte
    {
        NoContact = 0,
        BalloonPopped = 1,
        Clash = 2
    }

    public readonly struct CombatResolution
    {
        public readonly CombatResolutionStatus Status;
        public readonly byte EventCount;
        private readonly SimulationEvent _event0;
        private readonly SimulationEvent _event1;

        private CombatResolution(
            CombatResolutionStatus status,
            byte eventCount,
            in SimulationEvent event0,
            in SimulationEvent event1)
        {
            Status = status;
            EventCount = eventCount;
            _event0 = event0;
            _event1 = event1;
        }

        public bool TryGetEvent(int index, out SimulationEvent value)
        {
            if (index == 0 && EventCount > 0)
            {
                value = _event0;
                return true;
            }

            if (index == 1 && EventCount > 1)
            {
                value = _event1;
                return true;
            }

            value = default;
            return false;
        }

        public static CombatResolution None()
        {
            SimulationEvent empty = default;
            return new CombatResolution(
                CombatResolutionStatus.NoContact,
                0,
                in empty,
                in empty);
        }

        public static CombatResolution One(
            CombatResolutionStatus status,
            in SimulationEvent simulationEvent)
        {
            SimulationEvent empty = default;
            return new CombatResolution(
                status,
                1,
                in simulationEvent,
                in empty);
        }

        public static CombatResolution Two(
            CombatResolutionStatus status,
            in SimulationEvent event0,
            in SimulationEvent event1)
        {
            return new CombatResolution(
                status,
                2,
                in event0,
                in event1);
        }
    }
}