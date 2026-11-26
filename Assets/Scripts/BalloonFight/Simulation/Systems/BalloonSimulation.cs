namespace NetworkCourse.BalloonFight.Simulation
{
    public sealed class BalloonSimulation
    {
        private readonly SimulationConfig _config;
        private readonly bool _configurationIsValid;
        private readonly SimulationEventBuffer _events =
            new SimulationEventBuffer();
        private SimulationState _state;

        public uint NextTick => _state.NextTick;
        public SimulationState CurrentState => _state;
        public SimulationEventBuffer Events => _events;

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
            _events.Clear();
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
            bool canSimulate = _state.Match.Phase == MatchPhase.Playing
                && !applyReset;

            if (canSimulate)
            {
                FighterState fighter0 = _state.Fighters.Get(0);
                FighterState fighter1 = _state.Fighters.Get(1);
                bool fighter0Active = _state.IsSlotActive(0);
                bool fighter1Active = _state.IsSlotActive(1);

                if (fighter0Active)
                {
                    FighterMotionSystem.Step(
                        ref fighter0,
                        in fighter0Input,
                        in _config);
                    processedSlotOrder = AppendSlot(
                        processedSlotOrder,
                        0);
                }

                if (fighter1Active)
                {
                    FighterMotionSystem.Step(
                        ref fighter1,
                        in fighter1Input,
                        in _config);
                    processedSlotOrder = AppendSlot(
                        processedSlotOrder,
                        1);
                }

                CombatResolution combat = FighterCombatSystem.Step(
                    ref fighter0,
                    ref fighter1,
                    fighter0Active,
                    fighter1Active,
                    expectedTick,
                    in _config);
                if (!AppendCombatEvents(in combat))
                {
                    return SimulationResult.Fail(
                        SimulationStatus.EventBufferFull);
                }

                MatchState matchState = _state.Match;
                MatchResolution match = MatchSystem.Step(
                    ref matchState,
                    in fighter0,
                    in fighter1,
                    fighter0Active,
                    fighter1Active,
                    expectedTick);
                if (match.HasEvent
                    && _events.TryAppend(in match.Event)
                        != SimulationEventBufferStatus.Success)
                {
                    return SimulationResult.Fail(
                        SimulationStatus.EventBufferFull);
                }

                _state.Fighters.Set(0, in fighter0);
                _state.Fighters.Set(1, in fighter1);
                _state.Match = matchState;
            }

            _state.NextTick = unchecked(expectedTick + 1u);
            SimulationSnapshot snapshot = SimulationSnapshot.Capture(
                expectedTick,
                in _state);
            return SimulationResult.Complete(
                expectedTick,
                processedSlotOrder,
                (byte)_events.Count,
                in snapshot);
        }

        private bool AppendCombatEvents(in CombatResolution combat)
        {
            for (int index = 0; index < combat.EventCount; index++)
            {
                if (!combat.TryGetEvent(index, out SimulationEvent value)
                    || _events.TryAppend(in value)
                        != SimulationEventBufferStatus.Success)
                {
                    return false;
                }
            }

            return true;
        }

        private void ApplyRoundReset()
        {
            if (_state.IsSlotActive(0))
            {
                FighterState fighter0 = _state.Fighters.Get(0);
                FighterMotionSystem.ResetForRound(
                    ref fighter0,
                    in _config.Fighter0Spawn);
                FighterCombatSystem.ResetForRound(ref fighter0);
                _state.Fighters.Set(0, in fighter0);
            }

            if (_state.IsSlotActive(1))
            {
                FighterState fighter1 = _state.Fighters.Get(1);
                FighterMotionSystem.ResetForRound(
                    ref fighter1,
                    in _config.Fighter1Spawn);
                FighterCombatSystem.ResetForRound(ref fighter1);
                _state.Fighters.Set(1, in fighter1);
            }

            MatchSystem.ApplyRoundReset(ref _state.Match);
        }

        private static ushort AppendSlot(ushort order, int slotIndex)
        {
            return (ushort)((order << 4) | (slotIndex + 1));
        }
    }
}