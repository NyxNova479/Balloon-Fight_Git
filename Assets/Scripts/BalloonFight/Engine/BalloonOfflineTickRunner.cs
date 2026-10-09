using UnityEngine;
using Unity.Profiling;
using NetworkCourse.BalloonFight.Simulation;
using System;
using UnityEditor;

namespace NetworkCourse.BalloonFight.Engine
{
    public enum OfflineRunnerStatus
    {
        Idle = 0,
        Running = 1,
        ConfigurationError = 2,
        SimulationError = 3,
        TickDebtExceeded = 4
    }


    public class BalloonOfflineTickRunner : MonoBehaviour
    {
        private const int MaxTicksPerFrame = 4;
        private const int MaxTickDebt = 50;

        private static readonly ProfilerMarker FrameUpdateMarker =
            new ProfilerMarker("BalloonFight.FrameUpdate");
        private static readonly ProfilerMarker SimulationStepMarker =
            new ProfilerMarker("BalloonFight.SimulationStep");
        private static readonly ProfilerMarker RenderStepMarker =
            new ProfilerMarker("BalloonFight.RenderStep");

        [Header("Dependencies")]
        [SerializeField] private BalloonInputCapture _inputCapture;
        [SerializeField] private BalloonFighterView _fighter0View;
        [SerializeField] private BalloonFighterView _fighter1View;

        [Header("Initial state")]
        [SerializeField] private Vector2 _fighter0InitialPosition;

        [Header("Runtime diagnostics")]
        [SerializeField] private OfflineRunnerStatus _status;
        [SerializeField] private uint _completedTicks;
        [SerializeField] private int _lastFrameTickCount;
        [SerializeField] private int _maximumFrameTickCount;
        [SerializeField] private int _pendingTickDebt;

        private BalloonSimulation _simulation;
        private RenderSnapshot _renderSnapshot;
        private double _accumulatorSeconds;
        private double _tickDeltaSeconds;
        private bool _clockStarted;

        public OfflineRunnerStatus Status => _status;
        public uint CompletedTicks => _completedTicks;
        public int LastFrameTickCount => _lastFrameTickCount;
        public int MaximumFrameTickCount => _maximumFrameTickCount;
        public int PendingTickDebt => _pendingTickDebt;

        private void Awake()
        {
            if (!ValidateReferences())
            {
                StopWithError(
                    OfflineRunnerStatus.ConfigurationError,
                    "BallonFightOfflineTickRunner depencies are invalid");
                return;
            }

            SimulationConfig config = SimulationConfig.CreateReference();
            if (!config.IsValid()) return;

            SimVector2 initialPosition = new SimVector2(
                _fighter0InitialPosition.x,
                _fighter0InitialPosition.y);
            SimulationState initialState =
                SimulationState.CreateSingleFighter(in initialPosition);

            _simulation = new BalloonSimulation(in config, in initialState);
            _tickDeltaSeconds = config.TickDeltaTime;
            _renderSnapshot = RenderSnapshot.FromSimulation(in initialState);
            _status = OfflineRunnerStatus.Running;

            if (!_fighter0View.RenderStep(in _renderSnapshot)) return;

            if (!_fighter1View.RenderStep(in _renderSnapshot) || _fighter1View != null) return;

        }

        private void Update()
        {
            if (_status != OfflineRunnerStatus.Running) return;

            using (FrameUpdateMarker.Auto())
            {
                FrameUpdate();
            }

            if (_status != OfflineRunnerStatus.Running) return;

            _lastFrameTickCount = 0;
            while (_accumulatorSeconds >= _tickDeltaSeconds
                && _lastFrameTickCount < MaxTicksPerFrame)
            {
                using (SimulationStepMarker.Auto())
                {
                    if (!SimulationStep()) return;
                }

                _accumulatorSeconds -= _tickDeltaSeconds;
                _lastFrameTickCount++;
                _completedTicks++;
            }

            if(_lastFrameTickCount > _maximumFrameTickCount)
            {
                _maximumFrameTickCount = _lastFrameTickCount;
            }

            _pendingTickDebt = (int)(_accumulatorSeconds / _tickDeltaSeconds);

            using (RenderStepMarker.Auto())
            {
                RenderStep();
            }

        }

        private void FrameUpdate()
        {
            throw new NotImplementedException();

        }

        private bool SimulationStep()
        {
            throw new NotImplementedException();

        }

        private void RenderStep()
        {
            throw new NotImplementedException();
        }

        private bool ValidateReferences()
        {
            if (_inputCapture == null) return false;
            if (_fighter0View == null || !_fighter0View.IsConfigured) return false;
            if (_fighter0View.SlotIndex != 0) return false;

            return _fighter1View == null
                || (_fighter1View.IsConfigured && _fighter1View.SlotIndex == 1);
        }

        private void StopWithError(OfflineRunnerStatus status, string message)
        {
            _status = status;
            enabled = false;
            Debug.LogError(message, this);
        }

    }
}
