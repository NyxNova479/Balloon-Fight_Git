using NetworkCourse.BalloonFight.Simulation;
using Unity.Profiling;
using UnityEngine;

namespace NetworkCourse.BalloonFight.Engine
{
    public enum OfflineRunnerStatus : byte
    {
        Idle = 0,
        Running = 1,
        ConfigurationError = 2,
        SimulationError = 3
    }

    public sealed class BalloonOfflineTickRunner : MonoBehaviour
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
        [SerializeField] private BalloonInputCapture _fighter0InputCapture;
        [SerializeField] private BalloonInputCapture _fighter1InputCapture;
        [SerializeField] private BalloonFighterView _fighter0View;
        [SerializeField] private BalloonFighterView _fighter1View;

        [Header("Runtime diagnostics")]
        [SerializeField] private OfflineRunnerStatus _status;
        [SerializeField] private uint _completedTicks;
        [SerializeField] private int _lastFrameTickCount;
        [SerializeField] private int _maximumFrameTickCount;
        [SerializeField] private int _pendingTickDebt;
        [SerializeField] private ushort _lastProcessedSlotOrder;

        private BalloonSimulation _simulation;
        private RenderSnapshot _renderSnapshot;
        private double _accumulatorSeconds;
        private double _tickDeltaSeconds;
        private bool _hasWarnedAboutTickDebt;
        private bool _clockStarted;
        private bool _debugRoundOverRequested;

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
                    "BalloonOfflineTickRunner dependencies are invalid.");
                return;
            }

            SimulationConfig config = SimulationConfig.CreateReference();
            if (!config.IsValid())
            {
                StopWithError(
                    OfflineRunnerStatus.ConfigurationError,
                    "Balloon Fight simulation config is invalid.");
                return;
            }

            SimulationState initialState = SimulationState.CreateTwoFighters(
                in config.Fighter0Spawn,
                in config.Fighter1Spawn);
            _simulation = new BalloonSimulation(in config, in initialState);
            _tickDeltaSeconds = config.TickDeltaTime;
            _renderSnapshot = RenderSnapshot.FromSimulationState(in initialState);
            _status = OfflineRunnerStatus.Running;

            if (!_fighter0View.RenderStep(in _renderSnapshot)
                || !_fighter1View.RenderStep(in _renderSnapshot))
            {
                StopWithError(
                    OfflineRunnerStatus.ConfigurationError,
                    "A fighter view rejected its initial snapshot.");
            }
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

            if (_lastFrameTickCount > _maximumFrameTickCount)
            {
                _maximumFrameTickCount = _lastFrameTickCount;
            }

            _pendingTickDebt = (int)(_accumulatorSeconds / _tickDeltaSeconds);
            if (_pendingTickDebt <= MaxTickDebt)
            {
                _hasWarnedAboutTickDebt = false;
            }

            using (RenderStepMarker.Auto())
            {
                RenderStep();
            }
        }

        private void FrameUpdate()
        {
            _fighter0InputCapture.FrameUpdate();
            _fighter1InputCapture.FrameUpdate();
            if (!_clockStarted)
            {
                _clockStarted = true;
                return;
            }

            _accumulatorSeconds += Time.unscaledDeltaTime;

            double maximumDebtSeconds = _tickDeltaSeconds * MaxTickDebt;
            if (_accumulatorSeconds > maximumDebtSeconds
                && !_hasWarnedAboutTickDebt)
            {
                _hasWarnedAboutTickDebt = true;
                Debug.LogWarning(
                    "Balloon Fight tick debt exceeded 50 ticks. "
                    + "The runner is continuing to catch up.",
                    this);
            }
        }

        private bool SimulationStep()
        {
            uint tick = _simulation.NextTick;
            FighterInput fighter0Input =
                _fighter0InputCapture.CreateInput(tick);
            FighterInput fighter1Input =
                _fighter1InputCapture.CreateInput(tick);

            SimulationResult result = _simulation.Step(
                in fighter0Input,
                in fighter1Input,
                _debugRoundOverRequested);
            if (!result.Succeeded)
            {
                StopWithError(
                    OfflineRunnerStatus.SimulationError,
                    "Balloon Fight simulation step failed.");
                return false;
            }

            _debugRoundOverRequested = false;
            _lastProcessedSlotOrder = result.ProcessedSlotOrder;
            SimulationSnapshot snapshot = result.Snapshot;
            _renderSnapshot = RenderSnapshot.FromSimulationSnapshot(in snapshot);
            return true;
        }

        private void RenderStep()
        {
            if (!_fighter0View.RenderStep(in _renderSnapshot)
                || !_fighter1View.RenderStep(in _renderSnapshot))
            {
                StopWithError(
                    OfflineRunnerStatus.ConfigurationError,
                    "A fighter view rejected a render snapshot.");
            }
        }

        private bool ValidateReferences()
        {
            return _fighter0InputCapture != null
                && _fighter1InputCapture != null
                && _fighter0View != null
                && _fighter0View.IsConfigured
                && _fighter0View.SlotIndex == 0
                && _fighter1View != null
                && _fighter1View.IsConfigured
                && _fighter1View.SlotIndex == 1;
        }

        private void StopWithError(
            OfflineRunnerStatus status,
            string message)
        {
            _status = status;
            enabled = false;
            Debug.LogError(message, this);
        }

#if UNITY_EDITOR
        private bool _renderingSettingsSaved;
        private int _previousTargetFrameRate;
        private int _previousVSyncCount;

        [ContextMenu("Course/Request Debug Round Over")]
        private void RequestDebugRoundOver()
        {
            _debugRoundOverRequested = true;
        }

        [ContextMenu("Course/Inject 12 Ticks Of Debt")]
        private void InjectRecoverableDebt()
        {
            _accumulatorSeconds += _tickDeltaSeconds * 12d;
            _pendingTickDebt = (int)(_accumulatorSeconds / _tickDeltaSeconds);
            if (_pendingTickDebt <= MaxTickDebt)
            {
                _hasWarnedAboutTickDebt = false;
            }
        }

        [ContextMenu("Course/Inject 51 Ticks Of Debt")]
        private void InjectDebtOverWarningThreshold()
        {
            _accumulatorSeconds += _tickDeltaSeconds * 51d;
            _pendingTickDebt = (int)(_accumulatorSeconds / _tickDeltaSeconds);
            if (_pendingTickDebt <= MaxTickDebt)
            {
                _hasWarnedAboutTickDebt = false;
            }
        }

        [ContextMenu("Course/Limit Rendering To 30 FPS")]
        private void LimitRenderingTo30Fps()
        {
            ApplyRenderingLimit(30);
        }

        [ContextMenu("Course/Limit Rendering To 144 FPS")]
        private void LimitRenderingTo144Fps()
        {
            ApplyRenderingLimit(144);
        }

        [ContextMenu("Course/Restore Rendering Limit")]
        private void RestoreRenderingLimit()
        {
            if (!_renderingSettingsSaved) return;

            QualitySettings.vSyncCount = _previousVSyncCount;
            Application.targetFrameRate = _previousTargetFrameRate;
            _renderingSettingsSaved = false;
        }

        private void ApplyRenderingLimit(int targetFrameRate)
        {
            if (!_renderingSettingsSaved)
            {
                _previousVSyncCount = QualitySettings.vSyncCount;
                _previousTargetFrameRate = Application.targetFrameRate;
                _renderingSettingsSaved = true;
            }

            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = targetFrameRate;
        }

        private void OnDestroy()
        {
            RestoreRenderingLimit();
        }
#endif
    }
}
