namespace NetworkCourse.BalloonFight.Simulation
{

    public enum SimulationStatus
    {

        Completed = 0,
        InvalidConfiguration = 1,
        TickMismatch = 2

    }

    public readonly struct SimulationResult
    {
        public readonly SimulationStatus Status;
        public readonly bool HasProcessedTick;
        public readonly uint ProcessedTick;
        public readonly ushort ProcessedSlotOrder;
        public readonly SimulationSnapshot Snapshot;

        public bool Succeeded => Status == SimulationStatus.Completed;

        private SimulationResult(
            SimulationStatus status,
            bool hasProcessedTick,
            uint processedTick,
            ushort processedSlotOrder,
            in SimulationSnapshot snapshot)
        {

            Status = status;
            HasProcessedTick = hasProcessedTick;
            ProcessedTick = processedTick;
            ProcessedSlotOrder = processedSlotOrder;
            Snapshot = snapshot;

        }

        public static SimulationResult Complete(
            uint processedTick,
            ushort processedSlotOrder,
            in SimulationSnapshot snapshot)
        {
            return new SimulationResult(
                SimulationStatus.Completed,
                true,
                processedTick,
                processedSlotOrder,
                in snapshot);
        }

        public static SimulationResult Fail(
            SimulationStatus status)
        {
            SimulationSnapshot empty = default;
            return new SimulationResult(status, false, 0u, 0, in empty);
        }
    }

}