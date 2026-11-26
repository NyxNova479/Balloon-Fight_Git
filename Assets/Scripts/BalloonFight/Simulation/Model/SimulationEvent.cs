namespace NetworkCourse.BalloonFight.Simulation
{
    public enum SimulationEventType : byte
    {
        BalloonPopped = 0,
        Clash = 1,
        FighterEliminated = 2,
        RoundWon = 3,
        RoundDraw = 4
    }

    public readonly struct SimulationEvent
    {
        public readonly uint Tick;
        public readonly SimulationEventType Type;
        public readonly byte ActorSlot;
        public readonly byte TargetSlot;
        public readonly byte BalloonIndex;

        public SimulationEvent(
            uint tick,
            SimulationEventType type,
            byte actorSlot,
            byte targetSlot,
            byte balloonIndex)
        {
            Tick = tick;
            Type = type;
            ActorSlot = actorSlot;
            TargetSlot = targetSlot;
            BalloonIndex = balloonIndex;
        }
    }
}