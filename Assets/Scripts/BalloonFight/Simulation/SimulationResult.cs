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
        public readonly SimulationState State;

        public bool Succeeded => Status == SimulationStatus.Completed;

        private SimulationResult(
            SimulationStatus status,
            bool hasProcessedTick,
            uint processedTick,
            in SimulationState state)
        {

            Status = status;
            HasProcessedTick = hasProcessedTick;
            ProcessedTick = processedTick;
            State = state;

        }

        public static SimulationResult Complete(
            uint processedTick,
            in SimulationState state)
        {
            return new SimulationResult(
                SimulationStatus.Completed,
                true,
                processedTick,
                in state);
        }

        public static SimulationResult Fail(
            SimulationStatus status,
            in SimulationState state)
        {
            return new SimulationResult(status, false, 0u, in state);
        }
    }

}