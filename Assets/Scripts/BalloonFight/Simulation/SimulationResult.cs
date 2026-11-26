namespace NetworkCourse.BalloonFight.Simulation
{
    public enum SimulationStatus : byte
    {
        Completed = 0,
        InvalidConfiguration = 1,
        TickMismatch = 2,
        EventBufferFull = 3
    }

    public readonly struct SimulationResult
    {
        public readonly SimulationStatus Status;
        public readonly bool HasProcessedTick;
        public readonly uint ProcessedTick;
        public readonly ushort ProcessedSlotOrder;
        public readonly byte EventCount;
        public readonly SimulationSnapshot Snapshot;

        public bool Succeeded => Status == SimulationStatus.Completed;

        private SimulationResult(
            SimulationStatus status,
            bool hasProcessedTick,
            uint processedTick,
            ushort processedSlotOrder,
            byte eventCount,
            in SimulationSnapshot snapshot)
        {
            Status = status;
            HasProcessedTick = hasProcessedTick;
            ProcessedTick = processedTick;
            ProcessedSlotOrder = processedSlotOrder;
            EventCount = eventCount;
            Snapshot = snapshot;
        }

        public static SimulationResult Complete(
            uint processedTick,
            ushort processedSlotOrder,
            byte eventCount,
            in SimulationSnapshot snapshot)
        {
            return new SimulationResult(
                SimulationStatus.Completed,
                true,
                processedTick,
                processedSlotOrder,
                eventCount,
                in snapshot);
        }

        public static SimulationResult Fail(SimulationStatus status)
        {
            SimulationSnapshot empty = default;
            return new SimulationResult(
                status,
                false,
                0u,
                0,
                0,
                in empty);
        }
    }
}