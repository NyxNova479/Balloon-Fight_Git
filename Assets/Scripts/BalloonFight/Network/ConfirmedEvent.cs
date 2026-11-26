using NetworkCourse.BalloonFight.Simulation;

namespace NetworkCourse.BalloonFight.Networking
{
    public readonly struct ConfirmedEvent
    {
        public readonly uint EventId;
        public readonly SimulationEvent Event;

        public ConfirmedEvent(
            uint eventId,
            in SimulationEvent simulationEvent)
        {
            EventId = eventId;
            Event = simulationEvent;
        }
    }
}