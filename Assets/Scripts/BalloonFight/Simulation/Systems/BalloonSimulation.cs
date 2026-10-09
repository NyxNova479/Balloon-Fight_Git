namespace NetworkCourse.BalloonFight.Simulation
{
    public sealed class BalloonSimulation
    {
        private readonly SimulationConfig _config;
        private readonly bool _configurationIsValid;
        private SimulationState _state;

        public uint NextTick => _state.NextTick;
        public SimulationState CurrentState => _state;

        public BalloonSimulation(
            in SimulationConfig config,
            in SimulationState initialState)
        {
            _config = config;
            _configurationIsValid = config.IsValid();
            _state = initialState;
        }

        public SimulationResult Step(
            in FighterInput fighter0Input,
            in FighterInput fighter1Input)
        {
            if (!_configurationIsValid)
            {
                return SimulationResult.Fail(
                    SimulationStatus.InvalidConfiguration,
                    in _state);
            }

            uint expectedTick = _state.NextTick;
            if (fighter0Input.Tick != expectedTick
                || fighter1Input.Tick != expectedTick)
            {
                return SimulationResult.Fail(
                    SimulationStatus.TickMismatch,
                    in _state);
            }

            FighterMotionSystem.Step(
                ref _state.Fighter0,
                in fighter0Input,
                in _config);
            FighterMotionSystem.Step(
                ref _state.Fighter1,
                in fighter1Input,
                in _config);

            _state.NextTick = unchecked(expectedTick + 1u);
            return SimulationResult.Complete(expectedTick, in _state);
        }
    }
}