namespace NetworkCourse.BalloonFight.Simulation
{

    public readonly struct PredictionSnapshot
    {

        private readonly FighterState _fighter;

        public uint InputEpoch { get; }
        public uint InputTick { get; }
        public FighterState Fighter => _fighter;

        public PredictionSnapshot(
            uint inputEpoch,
            uint inputTick,
            in FighterState fighter)
        {

            InputEpoch = inputEpoch;
            InputTick = inputTick;
            _fighter = fighter;

        }

    }

}