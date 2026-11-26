namespace NetworkCourse.BalloonFight.Simulation
{

    public readonly struct MatchResolution
    {

        public readonly bool RoundEnded;
        public readonly bool HasEvent;
        public readonly SimulationEvent Event;

        public MatchResolution(
            bool roundEnded,
            bool hasEvent,
            in SimulationEvent simulationEvent)
        {
            RoundEnded = roundEnded;
            HasEvent = hasEvent;
            Event = simulationEvent;
        }

    }


}