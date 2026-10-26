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
            in FighterInput fighter1Input,
            bool debugRoundOverRequested = false)
        {
            if (!_configurationIsValid)
            {
                return SimulationResult.Fail(
                    SimulationStatus.InvalidConfiguration);
            }

            uint expectedTick = _state.NextTick;
            if (fighter0Input.Tick != expectedTick
                || fighter1Input.Tick != expectedTick)
            {
                return SimulationResult.Fail(
                    SimulationStatus.TickMismatch);
            }

            bool applyReset = MatchSystem.ShouldApplyRoundReset(
                in _state.Match,
                expectedTick);
            if (applyReset)
            {
                ApplyRoundReset();
            }

            ushort processedSlotOrder = 0;
            bool canMove = _state.Match.Phase == MatchPhase.Playing
                && !applyReset;
            if (canMove && _state.IsSlotActive(0))
            {
                FighterState fighter0 = _state.Fighters.Get(0);
                FighterMotionSystem.Step(
                    ref fighter0,
                    in fighter0Input,
                    in _config);
                _state.Fighters.Set(0, in fighter0);
                processedSlotOrder = AppendSlot(processedSlotOrder, 0);
            }

            if (canMove && _state.IsSlotActive(1))
            {
                FighterState fighter1 = _state.Fighters.Get(1);
                FighterMotionSystem.Step(
                    ref fighter1,
                    in fighter1Input,
                    in _config);
                _state.Fighters.Set(1, in fighter1);
                processedSlotOrder = AppendSlot(processedSlotOrder, 1);
            }

            MatchSystem.Step(
                ref _state.Match,
                expectedTick,
                debugRoundOverRequested);

            _state.NextTick = unchecked(expectedTick + 1u);
            SimulationSnapshot snapshot = SimulationSnapshot.Capture(
                expectedTick,
                in _state);
            return SimulationResult.Complete(
                expectedTick,
                processedSlotOrder,
                in snapshot);
        }

        private void ApplyRoundReset()
        {
            if (_state.IsSlotActive(0))
            {
                FighterState fighter0 = _state.Fighters.Get(0);
                FighterMotionSystem.ResetForRound(
                    ref fighter0,
                    in _config.Fighter0Spawn);
                _state.Fighters.Set(0, in fighter0);
            }

            if (_state.IsSlotActive(1))
            {
                FighterState fighter1 = _state.Fighters.Get(1);
                FighterMotionSystem.ResetForRound(
                    ref fighter1,
                    in _config.Fighter1Spawn);
                _state.Fighters.Set(1, in fighter1);
            }
        }

        private static ushort AppendSlot(ushort order, int slotIndex)
        {
            return (ushort)((order << 4) | (slotIndex + 1));
        }
    }
}