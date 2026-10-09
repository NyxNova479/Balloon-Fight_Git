namespace NetworkCourse.BalloonFight.Simulation
{

    public struct SimulationState
    {

        public uint NextTick;

        public FighterState Fighter0;
        public FighterState Fighter1;

        public static SimulationState CreateSingleFighter(in SimVector2 fighter0Position)
        {
            return new SimulationState
            {
                NextTick = 0u,
                Fighter0 = FighterState.CreateActive(fighter0Position),
                Fighter1 = FighterState.CreateInactive()
            };

        }


    }


}