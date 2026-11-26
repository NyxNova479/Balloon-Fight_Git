namespace NetworkCourse.BalloonFight.Simulation
{
    public enum OwnerPredictionStatus : byte
    {
        Success = 0,
        NotInitialized = 1,
        InvalidConfiguration = 2,
        WrongInputEpoch = 3,
        TickMismatch = 4,
        InvalidState
    }

    public sealed class OwnerPredictionSimulation
    {
        private readonly SimulationConfig _config;
        private readonly bool _configurationIsValid;
        private FighterState _fighter;
        private uint _inputEpoch;
        private uint _nextInputTick;
        private bool _isInitialized;

        public bool IsInitialized => _isInitialized;
        public uint InputEpoch => _inputEpoch;
        public uint NextInputTick => _nextInputTick;
        public FighterState CurrentFighter => _fighter;

        public OwnerPredictionSimulation(in SimulationConfig config)
        {
            _config = config;
            _configurationIsValid = config.IsValid();
            _fighter = default;
            _inputEpoch = 0u;
            _nextInputTick = 0u;
            _isInitialized = false;
        }

        public OwnerPredictionStatus Initialize(
            uint inputEpoch,
            in FighterState officialFighter)
        {
            if (!_configurationIsValid)
            {
                return OwnerPredictionStatus.InvalidConfiguration;
            }

            if(!IsValidFighterState(in officialFighter))
            {
                return OwnerPredictionStatus.InvalidState;
            }

            _fighter = officialFighter;
            _inputEpoch = inputEpoch;
            _nextInputTick = 0u;
            _isInitialized = true;
            return OwnerPredictionStatus.Success;
        }

        public OwnerPredictionStatus Step(
            uint inputEpoch,
            in InputCommand command,
            out PredictionSnapshot snapshot)
        {
            if (!_isInitialized)
            {
                snapshot = default;
                return OwnerPredictionStatus.NotInitialized;
            }

            if (!_configurationIsValid)
            {
                snapshot = default;
                return OwnerPredictionStatus.InvalidConfiguration;
            }

            if (inputEpoch != _inputEpoch)
            {
                snapshot = default;
                return OwnerPredictionStatus.WrongInputEpoch;
            }

            if (command.InputTick != _nextInputTick)
            {
                snapshot = default;
                return OwnerPredictionStatus.TickMismatch;
            }

            FighterInput input = new FighterInput(
                command.InputTick,
                command.Horizontal,
                command.FlapHeld);
            FighterMotionSystem.Step(
                ref _fighter,
                in input,
                in _config);
            snapshot = new PredictionSnapshot(
                _inputEpoch,
                command.InputTick,
                in _fighter);
            _nextInputTick = unchecked(_nextInputTick + 1u);
            return OwnerPredictionStatus.Success;
        }

        public OwnerPredictionStatus Restore(
            uint inputEpoch,
            uint nextInputTick,
            in FighterState officialFighter)
        {
            if (!_isInitialized)
            {
                return OwnerPredictionStatus.NotInitialized;
            }

            if (!_configurationIsValid)
            {
                return OwnerPredictionStatus.InvalidConfiguration;
            }

            if (inputEpoch != _inputEpoch)
            {
                return OwnerPredictionStatus.WrongInputEpoch;
            }

            if (!IsValidFighterState(in officialFighter))
            {
                return OwnerPredictionStatus.InvalidState;
            }

            _fighter = officialFighter;
            _nextInputTick = nextInputTick;
            return OwnerPredictionStatus.Success;
        }

        public static bool IsValidFighterState(in FighterState fighter)
        {

            bool phaseIsValid = fighter.Phase == FighterPhase.Flying
                || fighter.Phase == FighterPhase.Falling
                || fighter.Phase == FighterPhase.Eliminated;

            bool balloonMaskIsValid =
                (fighter.BalloonMask & ~FighterState.ValidBalloonMask) == 0
                && (fighter.Phase == FighterPhase.Flying
                    ? fighter.BalloonMask != 0
                    : fighter.BalloonMask == 0);

            return phaseIsValid
                && balloonMaskIsValid
                && IsFinite(fighter.Position.X)
                && IsFinite(fighter.Position.Y)
                && IsFinite(fighter.Velocity.X)
                && IsFinite(fighter.Velocity.Y)
                && IsFinite(fighter.FlapCooldownRemaining)
                && fighter.FlapCooldownRemaining >= 0f;

        }

        private static bool IsFinite (float value)
        {
            return !float.IsNaN(value) && ! float.IsInfinity(value);
        }
    }
}