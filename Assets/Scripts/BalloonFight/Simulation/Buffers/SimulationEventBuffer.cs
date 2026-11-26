namespace NetworkCourse.BalloonFight.Simulation
{
    public enum SimulationEventBufferStatus : byte
    {
        Success = 0,
        Full = 1
    }

    public sealed class SimulationEventBuffer
    {
        public const int Capacity = 16;

        private readonly SimulationEvent[] _events =
            new SimulationEvent[Capacity];
        private int _count;

        public int Count => _count;

        public void Clear()
        {
            _count = 0;
        }

        public SimulationEventBufferStatus TryAppend(
            in SimulationEvent simulationEvent)
        {
            if (_count >= Capacity)
            {
                return SimulationEventBufferStatus.Full;
            }

            _events[_count] = simulationEvent;
            _count++;
            return SimulationEventBufferStatus.Success;
        }

        public bool TryGetAt(int index, out SimulationEvent simulationEvent)
        {
            if (index < 0 || index >= _count)
            {
                simulationEvent = default;
                return false;
            }

            simulationEvent = _events[index];
            return true;
        }
    }
}