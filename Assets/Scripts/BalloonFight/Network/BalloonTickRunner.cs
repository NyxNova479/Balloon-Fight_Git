using NetworkCourse.BalloonFight.Networking;
using NetworkCourse.BalloonFight.Simulation;
using Unity.Profiling;
using UnityEngine;

namespace NetworkCourse.BalloonFight.Engine
{

    public enum TickRunnerMode : byte
    {
        Dormant = 0,
        Server = 1,
        Client = 2
    }

    public enum TickRunnerStatus : byte
    {
        Idle = 0,
        Running = 1,
        ConfigurationError = 2,
        SimulationError = 3,
        TickDebtExceeded = 4
    }

    public sealed class BalloonTickRunner : MonoBehaviour
    {

        private const int MaxTicksPerFrame = 4;
        private const int MaxTickDebt = 50;

        private static readonly ProfilerMarker SimulationStepMarker =
            new ProfilerMarker("BalloonFight.ServerSimulationStep");
        private static readonly ProfilerMarker RenderStepMarker =
            new ProfilerMarker("BalloonFight.RenderStep");

        [Header("Dependencies")]
        [SerializeField] private BalloonFighterView _fighter0View;
        [SerializeField] private BalloonFighterView _fighter1View;

        [Header("Runtime diagnostics")]
        [SerializeField] private TickRunnerMode _mode;
        [SerializeField] private TickRunnerStatus _status;
        [SerializeField] private uint _completedServerTicks;
        [SerializeField] private uint _lastOfficialServerTick;
        [SerializeField] private int _lastFrameTickCount;
        [SerializeField] private int _maximumFrameTickCount;
        [SerializeField] private int _pendingTickDebt;
        [SerializeField] private int _rejectedOfficialSnapshots;

        private NetworkCommandInbox _inbox;
        private BalloonSimulation _simulation;
        private SimulationSnapshot _latestSimulationSnapshot;
        private RenderSnapshot _latestRenderSnapshot;
        private bool _hasSimulationSnapshot;
        private bool _hasOfficialSnapshot;
        private bool _serverSimulationEnabled;
        private double _accumulatorSeconds;
        private double _tickDeltaSeconds;

        public TickRunnerMode Mode => _mode;
        public TickRunnerStatus Status => _status;
        public uint CompletedServerTicks => _completedServerTicks;
        public uint LastOfficialServerTick => _lastOfficialServerTick;
        public int PendingTickDebt => _pendingTickDebt;
        public int RejectedOfficialSnapshots => _rejectedOfficialSnapshots;

        // TODO : On continue ici

        private void HideViews()
        {
            RenderSnapshot hidden = default;
            _fighter0View.RenderStep(in hidden);
            _fighter1View.RenderStep(in hidden);
        }

        private bool ValidateViews()
        {
            return _fighter0View != null
                && _fighter0View.IsConfigured
                && _fighter0View.SlotIndex == 0
                && _fighter1View != null
                && _fighter1View.IsConfigured
                && _fighter1View.SlotIndex == 1;
        }

        private void StopWithError(
            TickRunnerStatus status,
            string message)
        {
            _status = status;
            Debug.LogError(message, this);
        }

    }

}