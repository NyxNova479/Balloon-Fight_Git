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

    public enum OwnerInputControlStatus : byte
    {
        EpochStarted = 0,
        AckApplied = 1,
        WaitingForFirstAck = 2,
        StaleEpoch = 3,
        AckRejected = 4,
        OwnerStreamDisabled = 5
    }

    public sealed class BalloonTickRunner : MonoBehaviour
    {
        private const int MaxTicksPerFrame = 4;
        private const int MaxTickDebt = 50;
        private const double SimulationTickRate = 50d;
        private const double InterpolationDelayTicks = 5d;
        private const double MaximumClockPhaseErrorTicks = 5d;
        private const double MaximumClockSpeedCorrection = 0.1d;

        private static readonly ProfilerMarker SimulationStepMarker =
            new ProfilerMarker("BalloonFight.ServerSimulationStep");
        private static readonly ProfilerMarker PredictionStepMarker =
            new ProfilerMarker("BalloonFight.OwnerPredictionStep");
        private static readonly ProfilerMarker ReconciliationStepMarker =
            new ProfilerMarker("BalloonFight.OwnerReconciliationStep");
        private static readonly ProfilerMarker RenderStepMarker =
            new ProfilerMarker("BalloonFight.RenderStep");

        [Header("Dependencies")]
        [SerializeField] private BalloonFighterView _fighter0View;
        [SerializeField] private BalloonFighterView _fighter1View;

        [Header("Runtime diagnostics")]
        [SerializeField] private TickRunnerMode _mode;
        [SerializeField] private TickRunnerStatus _status;
        [SerializeField] private int _localSlotIndex = -1;
        [SerializeField] private uint _completedServerTicks;
        [SerializeField] private uint _lastOfficialServerTick;
        [SerializeField] private int _lastFrameTickCount;
        [SerializeField] private int _maximumFrameTickCount;
        [SerializeField] private int _pendingTickDebt;
        [SerializeField] private int _rejectedOfficialSnapshots;
        [SerializeField] private bool _interpolationStarved;
        [SerializeField] private uint _interpolationStarvationCount;
        [SerializeField] private uint _interpolationResetCount;
        [SerializeField] private double _estimatedServerTick;
        [SerializeField] private double _renderTick;
        [SerializeField] private double _secondsSinceLatestSnapshotReceived;
        [SerializeField] private int _ownerInputHistoryCount;
        [SerializeField] private uint _ownerInputEpoch;
        [SerializeField] private uint _nextOwnerInputTick;
        [SerializeField] private bool _hardResyncRequested;
        [SerializeField] private uint _producedOwnerInputCount;
        [SerializeField] private uint _ownerHistoryFullCount;
        [SerializeField] private int _predictionHistoryCount;
        [SerializeField] private bool _showOwnerPrediction = true;
        [SerializeField] private float _latestPredictionPositionError;
        [SerializeField] private float _latestPredictionVelocityError;
        [SerializeField] private float _maximumPredictionPositionError;
        [SerializeField] private ReconciliationStatus _lastReconciliationStatus;
        [SerializeField] private int _lastReplayCount;
        [SerializeField] private uint _totalReplayCount;
        [SerializeField] private uint _correctionCount;
        [SerializeField] private uint _hardResyncCount;
        [SerializeField] private uint _staleOwnerStateCount;
        [SerializeField] private uint _missedConfirmedEvents;
        [SerializeField] private uint _duplicateConfirmedEvents;
        [SerializeField] private uint _invalidConfirmedWindows;

        private readonly SnapshotInterpolationBuffer _snapshotBuffer =
            new SnapshotInterpolationBuffer();
        private readonly EstimatedServerClock _estimatedServerClock =
            new EstimatedServerClock(
                SimulationTickRate,
                MaximumClockPhaseErrorTicks,
                MaximumClockSpeedCorrection);
        private readonly InputHistory _ownerInputHistory =
            new InputHistory();
        private readonly PredictionHistory _predictionHistory =
            new PredictionHistory();
        private readonly PredictionReconciler _predictionReconciler =
            new PredictionReconciler(
                PredictionReconciliationConfig.CreateReference());

        private readonly ConfirmedEventHistory _confirmedEventHistory =
            new ConfirmedEventHistory();
        private readonly ConfirmedEventCursor _confirmedEventCursor =
            new ConfirmedEventCursor();
        private readonly ClientConfirmedEventBuffer _confirmedEventOutput =
            new ClientConfirmedEventBuffer();

        private NetworkCommandInbox _inbox;
        private InputStreamControl _inputStreamControl;
        private OwnerPredictionSimulation _ownerPredictionSimulation;
        private BalloonInputCapture _localInputCapture;
        private BalloonSimulation _simulation;
        private SimulationSnapshot _latestSimulationSnapshot;
        private NetworkSnapshot _latestOfficialSnapshot;
        private RenderSnapshot _latestRenderSnapshot;
        private bool _hasSimulationSnapshot;
        private bool _hasOfficialSnapshot;
        private bool _serverSimulationEnabled;
        private double _accumulatorSeconds;
        private double _tickDeltaSeconds;
        private double _latestSnapshotArrivalTimeSeconds;
        private bool _localOwnerStreamEnabled;
        private double _ownerInputAccumulatorSeconds;
        private uint _lastProducedFlapPressSequence;
        private bool _hardResyncRequestPending;
        private NetworkInputAck _latestSlot0InputAck;
        private NetworkInputAck _latestSlot1InputAck;
        private FighterState _latestPredictedFighter;
        private bool _hasPredictedFighter;
        private bool _hasPendingOwnerOfficialState;
        private uint _pendingOwnerOfficialServerTick;
        private NetworkInputAck _pendingOwnerAck;
        private FighterState _pendingOwnerOfficialFighter;
        private ConfirmedEventWindow _pendingConfirmedEvents;
        private uint _pendingConfirmedServerTick;
        private bool _hasPendingConfirmedEvents;

        public TickRunnerMode Mode => _mode;
        public TickRunnerStatus Status => _status;
        public int LocalSlotIndex => _localSlotIndex;
        public uint CompletedServerTicks => _completedServerTicks;
        public uint LastOfficialServerTick => _lastOfficialServerTick;
        public int PendingTickDebt => _pendingTickDebt;
        public int RejectedOfficialSnapshots =>
            _rejectedOfficialSnapshots;
        public int InterpolationBufferCount => _snapshotBuffer.Count;
        public bool InterpolationStarved => _interpolationStarved;
        public uint InterpolationStarvationCount =>
            _interpolationStarvationCount;
        public uint InterpolationResetCount => _interpolationResetCount;
        public uint InterpolationOverflowCount =>
            _snapshotBuffer.OverflowCount;
        public double EstimatedServerTick => _estimatedServerTick;
        public double RenderTick => _renderTick;
        public double SecondsSinceLatestSnapshotReceived =>
            _secondsSinceLatestSnapshotReceived;
        public int OwnerInputHistoryCount => _ownerInputHistory.Count;
        public uint OwnerInputEpoch => _ownerInputEpoch;
        public uint NextOwnerInputTick => _nextOwnerInputTick;
        public bool HardResyncRequested => _hardResyncRequested;
        public uint ProducedOwnerInputCount => _producedOwnerInputCount;
        public uint OwnerHistoryFullCount => _ownerHistoryFullCount;
        public int PredictionHistoryCount => _predictionHistory.Count;
        public bool ShowOwnerPrediction => _showOwnerPrediction;
        public float LatestPredictionPositionError =>
            _latestPredictionPositionError;
        public float LatestPredictionVelocityError =>
            _latestPredictionVelocityError;
        public float MaximumPredictionPositionError =>
            _maximumPredictionPositionError;
        public ReconciliationStatus LastReconciliationStatus =>
            _lastReconciliationStatus;
        public int LastReplayCount => _lastReplayCount;
        public uint TotalReplayCount => _totalReplayCount;
        public uint CorrectionCount => _correctionCount;
        public uint HardResyncCount => _hardResyncCount;
        public uint StaleOwnerStateCount => _staleOwnerStateCount;
        public uint SynthesizedServerInputCount => _inputStreamControl == null
            ? 0u
            : _inputStreamControl.SynthesizedInputCount;

        private void Awake()
        {
            if (!ValidateViews())
            {
                StopWithError(
                    TickRunnerStatus.ConfigurationError,
                    "BalloonTickRunner views are invalid.");
                return;
            }

            HideViews();
            _mode = TickRunnerMode.Dormant;
            _status = TickRunnerStatus.Idle;
        }

        private void Update()
        {
            if (_status != TickRunnerStatus.Running) return;

            ProcessPendingConfirmedEvents();

            if (_localOwnerStreamEnabled)
            {
                _localInputCapture.FrameUpdate();
                ProcessPendingLocalOfficialState();
                AdvanceOwnerInputClock();
            }

            if (_mode == TickRunnerMode.Server
                && _serverSimulationEnabled)
            {
                AdvanceServerSimulation();
            }

            if (_status != TickRunnerStatus.Running
                || !_hasOfficialSnapshot)
            {
                return;
            }

            using (RenderStepMarker.Auto())
            {
                RenderStep(Time.realtimeSinceStartupAsDouble);
            }
        }

        public bool StartServer(
            NetworkCommandInbox inbox,
            InputStreamControl inputStreamControl)
        {
            if (_status == TickRunnerStatus.ConfigurationError
                || _mode != TickRunnerMode.Dormant
                || inbox == null
                || inputStreamControl == null)
            {
                return false;
            }

            SimulationConfig config = SimulationConfig.CreateReference();
            if (!config.IsValid()) return false;

            SimulationState initialState = SimulationState.CreateTwoFighters(
                in config.Fighter0Spawn,
                in config.Fighter1Spawn);
            _simulation = new BalloonSimulation(in config, in initialState);
            _ownerPredictionSimulation =
                new OwnerPredictionSimulation(in config);
            _inbox = inbox;
            _inputStreamControl = inputStreamControl;
            _tickDeltaSeconds = config.TickDeltaTime;
            _mode = TickRunnerMode.Server;
            _status = TickRunnerStatus.Running;
            return true;
        }

        public void SetServerSimulationEnabled(bool isEnabled)
        {
            if (_mode != TickRunnerMode.Server) return;

            _serverSimulationEnabled = isEnabled;
            if (!isEnabled)
            {
                _accumulatorSeconds = 0d;
                _pendingTickDebt = 0;
            }
        }

        public bool StartClient()
        {
            if (_status == TickRunnerStatus.ConfigurationError
                || _mode != TickRunnerMode.Dormant)
            {
                return false;
            }

            SimulationConfig config = SimulationConfig.CreateReference();
            if (!config.IsValid()) return false;

            _tickDeltaSeconds = config.TickDeltaTime;
            _ownerPredictionSimulation =
                new OwnerPredictionSimulation(in config);
            _mode = TickRunnerMode.Client;
            _status = TickRunnerStatus.Running;
            return true;
        }

        public bool StartLocalOwnerStream(BalloonInputCapture inputCapture)
        {
            if (_status != TickRunnerStatus.Running
                || inputCapture == null
                || _localOwnerStreamEnabled)
            {
                return false;
            }

            _localInputCapture = inputCapture;
            _lastProducedFlapPressSequence =
                inputCapture.FlapPressSequence;
            _localOwnerStreamEnabled = true;
            return true;
        }

        public void SetLocalSlot(int localSlotIndex)
        {
            if (localSlotIndex < -1 || localSlotIndex > 1) return;

            _localSlotIndex = localSlotIndex;
            _fighter0View.SetLocalSlot(localSlotIndex);
            _fighter1View.SetLocalSlot(localSlotIndex);
        }

        public void StopNetworkMode()
        {
            _mode = TickRunnerMode.Dormant;
            _status = TickRunnerStatus.Idle;
            _inbox = null;
            _inputStreamControl = null;
            _localInputCapture = null;
            _ownerPredictionSimulation = null;
            _simulation = null;
            _hasSimulationSnapshot = false;
            _hasOfficialSnapshot = false;
            _serverSimulationEnabled = false;
            _accumulatorSeconds = 0d;
            _pendingTickDebt = 0;
            _completedServerTicks = 0u;
            _lastOfficialServerTick = 0u;
            _lastFrameTickCount = 0;
            _maximumFrameTickCount = 0;
            _rejectedOfficialSnapshots = 0;
            _interpolationStarved = false;
            _interpolationStarvationCount = 0u;
            _interpolationResetCount = 0u;
            _estimatedServerTick = 0d;
            _renderTick = 0d;
            _secondsSinceLatestSnapshotReceived = 0d;
            _latestSnapshotArrivalTimeSeconds = 0d;
            _localOwnerStreamEnabled = false;
            _ownerInputAccumulatorSeconds = 0d;
            _lastProducedFlapPressSequence = 0u;
            _hardResyncRequestPending = false;
            _hardResyncRequested = false;
            _ownerInputHistoryCount = 0;
            _ownerInputEpoch = 0u;
            _nextOwnerInputTick = 0u;
            _producedOwnerInputCount = 0u;
            _ownerHistoryFullCount = 0u;
            _predictionHistoryCount = 0;
            _latestPredictionPositionError = 0f;
            _latestPredictionVelocityError = 0f;
            _maximumPredictionPositionError = 0f;
            _lastReconciliationStatus = ReconciliationStatus.NoCorrection;
            _lastReplayCount = 0;
            _totalReplayCount = 0u;
            _correctionCount = 0u;
            _hardResyncCount = 0u;
            _staleOwnerStateCount = 0u;

            _confirmedEventHistory.Reset();
            _confirmedEventCursor.Reset();
            _confirmedEventOutput.Clear();
            _pendingConfirmedEvents = default;
            _pendingConfirmedServerTick = 0u;
            _hasPendingConfirmedEvents = false;
            _missedConfirmedEvents = 0u;
            _duplicateConfirmedEvents = 0u;
            _invalidConfirmedWindows = 0u;

            _hasPredictedFighter = false;
            _hasPendingOwnerOfficialState = false;
            _latestSlot0InputAck = default;
            _latestSlot1InputAck = default;
            _ownerInputHistory.Reset();
            _predictionHistory.Reset();
            _snapshotBuffer.Reset();
            _estimatedServerClock.Reset();
            SetLocalSlot(-1);
            HideViews();
        }

        public bool TryGetLatestSimulationSnapshot(
            out SimulationSnapshot snapshot,
            out NetworkInputAck slot0InputAck,
            out NetworkInputAck slot1InputAck)
        {
            snapshot = _latestSimulationSnapshot;
            slot0InputAck = _latestSlot0InputAck;
            slot1InputAck = _latestSlot1InputAck;
            return _mode == TickRunnerMode.Server
                && _hasSimulationSnapshot;
        }


        public bool TryCreateNewestInputBatch(
            int localSlotIndex,
            out InputBatch batch)
        {
            if (!_localOwnerStreamEnabled
                || localSlotIndex < 0
                || localSlotIndex > 1)
            {
                batch = default;
                return false;
            }

            return InputBatch.TryCreateNewest(
                (byte)localSlotIndex,
                _ownerInputHistory,
                _confirmedEventCursor.HasSeen,
                _confirmedEventCursor.LastSeenEventId,
                out batch);
        }

        public bool ActivateConfirmedEventSlot(int slot)
        {
            return _mode == TickRunnerMode.Server
                && _confirmedEventHistory.ActivateSlot(slot)
                    == ConfirmedEventCursorStatus.Success;
        }

        public bool DeactivateConfirmedEventSlot(int slot)
        {
            return _mode == TickRunnerMode.Server
                && _confirmedEventHistory.DeactivateSlot(slot)
                    == ConfirmedEventCursorStatus.Success;
        }

        public bool ObserveConfirmedEventCursor(
            int slot,
            bool hasSeen,
            uint lastSeenEventId)
        {
            return _mode == TickRunnerMode.Server
                && _confirmedEventHistory.ObserveCursor(
                    slot,
                    hasSeen,
                    lastSeenEventId) == ConfirmedEventCursorStatus.Success;
        }

        public bool TryCreateConfirmedEventWindows(
            out ConfirmedEventWindow slot0,
            out ConfirmedEventWindow slot1)
        {
            ConfirmedEventCursorStatus status0 =
                _confirmedEventHistory.CreateWindowForSlot(0, out slot0);
            ConfirmedEventCursorStatus status1 =
                _confirmedEventHistory.CreateWindowForSlot(1, out slot1);
            return IsWindowStatusUsable(status0)
                && IsWindowStatusUsable(status1);
        }

        private static bool IsWindowStatusUsable(
            ConfirmedEventCursorStatus status)
        {
            return status == ConfirmedEventCursorStatus.Success
                || status == ConfirmedEventCursorStatus.InactiveSlot;
        }


        public bool StageLocalOfficialState(
            uint serverTick,
            in NetworkInputAck ack,
            in FighterState officialFighter)
        {
            if (!_localOwnerStreamEnabled) return false;

            if (_hasPendingOwnerOfficialState
                && !ShouldReplacePendingOwnerState(serverTick, in ack))
            {
                _staleOwnerStateCount = IncrementSaturated(
                    _staleOwnerStateCount);
                return false;
            }

            _pendingOwnerOfficialServerTick = serverTick;
            _pendingOwnerAck = ack;
            _pendingOwnerOfficialFighter = officialFighter;
            _hasPendingOwnerOfficialState = true;
            return true;
        }

        private void ProcessPendingLocalOfficialState()
        {
            if (!_hasPendingOwnerOfficialState) return;

            NetworkInputAck ack = _pendingOwnerAck;
            FighterState officialFighter = _pendingOwnerOfficialFighter;
            _hasPendingOwnerOfficialState = false;
            using (ReconciliationStepMarker.Auto())
            {
                ApplyLocalOfficialState(in ack, in officialFighter);
            }
        }

        private OwnerInputControlStatus ApplyLocalOfficialState(
            in NetworkInputAck ack,
            in FighterState officialFighter)
        {
            if (!_localOwnerStreamEnabled)
            {
                return OwnerInputControlStatus.OwnerStreamDisabled;
            }

            if (_ownerPredictionSimulation == null)
            {
                RequestHardResync();
                return OwnerInputControlStatus.AckRejected;
            }

            if (!_ownerInputHistory.IsReady
                || ack.InputEpoch != _ownerInputHistory.InputEpoch)
            {
                if (_ownerInputHistory.IsReady
                    && !SequenceMath.IsNewer(
                        ack.InputEpoch,
                        _ownerInputHistory.InputEpoch))
                {
                    _staleOwnerStateCount = IncrementSaturated(
                        _staleOwnerStateCount);
                    return OwnerInputControlStatus.StaleEpoch;
                }

                _ownerInputHistory.BeginEpoch(ack.InputEpoch);
                _predictionHistory.BeginEpoch(ack.InputEpoch);
                OwnerPredictionStatus initializeStatus =
                    _ownerPredictionSimulation.Initialize(
                        ack.InputEpoch,
                        in officialFighter);
                if (initializeStatus != OwnerPredictionStatus.Success)
                {
                    RequestHardResync();
                    return OwnerInputControlStatus.AckRejected;
                }

                _ownerInputAccumulatorSeconds = 0d;
                _lastProducedFlapPressSequence =
                    _localInputCapture.FlapPressSequence;
                _hardResyncRequestPending = false;
                _hardResyncRequested = false;
                _latestPredictedFighter = officialFighter;
                _hasPredictedFighter = true;
                _fighter0View.CancelPredictionCorrection();
                _fighter1View.CancelPredictionCorrection();
                ResetPredictionErrors();
                RefreshOwnerInputDiagnostics();
                return OwnerInputControlStatus.EpochStarted;
            }

            if (!ack.HasProcessedInput)
            {
                if (!_ownerInputHistory.HasProduced)
                {
                    _ownerPredictionSimulation.Initialize(
                        ack.InputEpoch,
                        in officialFighter);
                    _latestPredictedFighter = officialFighter;
                    _hasPredictedFighter = true;
                }

                return OwnerInputControlStatus.WaitingForFirstAck;
            }

            ReconciliationResult result = _predictionReconciler.Reconcile(
                ack.InputEpoch,
                ack.LastProcessedInputTick,
                in officialFighter,
                _ownerPredictionSimulation,
                _ownerInputHistory,
                _predictionHistory);
            _lastReconciliationStatus = result.Status;
            _lastReplayCount = result.ReplayedInputCount;
            if (result.Status == ReconciliationStatus.StaleAck)
            {
                _staleOwnerStateCount = IncrementSaturated(
                    _staleOwnerStateCount);
                return OwnerInputControlStatus.AckApplied;
            }

            MeasurePredictionError(in result);
            if (!result.Succeeded)
            {
                RequestHardResync();
                _fighter0View.CancelPredictionCorrection();
                _fighter1View.CancelPredictionCorrection();
                return OwnerInputControlStatus.AckRejected;
            }

            if (result.Corrected)
            {
                _correctionCount = IncrementSaturated(_correctionCount);
                _totalReplayCount = AddSaturated(
                    _totalReplayCount,
                    result.ReplayedInputCount);
                _latestPredictedFighter = result.NewPredictedCurrent;
                _hasPredictedFighter = true;
                if (!_fighter0View.BeginOwnerPredictionCorrection(
                        in result.PreviousPredictedCurrent,
                        in result.NewPredictedCurrent)
                    || !_fighter1View.BeginOwnerPredictionCorrection(
                        in result.PreviousPredictedCurrent,
                        in result.NewPredictedCurrent))
                {
                    RequestHardResync();
                    return OwnerInputControlStatus.AckRejected;
                }
            }

            RefreshOwnerInputDiagnostics();
            return OwnerInputControlStatus.AckApplied;
        }

        private bool ShouldReplacePendingOwnerState(
            uint serverTick,
            in NetworkInputAck candidateAck)
        {
            if (candidateAck.InputEpoch != _pendingOwnerAck.InputEpoch)
            {
                return SequenceMath.IsNewer(
                    candidateAck.InputEpoch,
                    _pendingOwnerAck.InputEpoch);
            }

            return SequenceMath.IsNewer(
                serverTick,
                _pendingOwnerOfficialServerTick);
        }

        public bool StageLocalConfirmedEvents(
            uint serverTick,
            in ConfirmedEventWindow window)
        {
            if (_hasPendingConfirmedEvents
                && !SequenceMath.IsNewer(
                    serverTick,
                    _pendingConfirmedServerTick))
            {
                return false;
            }

            _pendingConfirmedServerTick = serverTick;
            _pendingConfirmedEvents = window;
            _hasPendingConfirmedEvents = true;
            return true;
        }

        private void ProcessPendingConfirmedEvents()
        {
            if (!_hasPendingConfirmedEvents) return;

            ConfirmedEventWindow window = _pendingConfirmedEvents;
            _hasPendingConfirmedEvents = false;
            ConfirmedEventConsumeStatus status = _confirmedEventCursor.Consume(
                in window,
                _confirmedEventOutput);
            if (status != ConfirmedEventConsumeStatus.Success)
            {
                _invalidConfirmedWindows = IncrementSaturated(
                    _invalidConfirmedWindows);
                return;
            }

            _missedConfirmedEvents =
                _confirmedEventCursor.MissedConfirmedEvents;
            _duplicateConfirmedEvents =
                _confirmedEventCursor.DuplicateEventCount;
        }

        public bool TryTakeHardResyncRequest(out uint inputEpoch)
        {
            if (!_hardResyncRequestPending
                || !_ownerInputHistory.IsReady)
            {
                inputEpoch = 0u;
                return false;
            }

            _hardResyncRequestPending = false;
            inputEpoch = _ownerInputHistory.InputEpoch;
            return true;
        }

        public bool TryReceiveOfficialSnapshot(
            in NetworkSnapshot message,
            double arrivalTimeSeconds)
        {
            SnapshotInsertStatus insertStatus = _snapshotBuffer.Insert(
                in message,
                out long unwrappedTick,
                out bool advancedNewest);
            if (insertStatus == SnapshotInsertStatus.Duplicate
                || insertStatus == SnapshotInsertStatus.TooOld)
            {
                _rejectedOfficialSnapshots++;
                return false;
            }

            if (!_estimatedServerClock.IsAnchored)
            {
                _estimatedServerClock.Anchor(
                    unwrappedTick,
                    arrivalTimeSeconds);
            }
            else if (insertStatus
                == SnapshotInsertStatus.AcceptedWithOverflow)
            {
                ReanchorPresentation(arrivalTimeSeconds);
            }
            else if (advancedNewest)
            {
                ServerClockObservation observation =
                    _estimatedServerClock.Observe(
                        unwrappedTick,
                        arrivalTimeSeconds,
                        out _);
                if (_interpolationStarved
                    || observation
                        == ServerClockObservation.ReanchorRequired)
                {
                    ReanchorPresentation(arrivalTimeSeconds);
                }
            }

            if (advancedNewest || !_hasOfficialSnapshot)
            {
                _latestOfficialSnapshot = message;
                _lastOfficialServerTick = message.ServerTick;
                _latestSnapshotArrivalTimeSeconds = arrivalTimeSeconds;
                _hasOfficialSnapshot = true;
            }

            return true;
        }

        private void ReanchorPresentation(double arrivalTimeSeconds)
        {
            if (!_snapshotBuffer.TryGetNewest(
                out NetworkSnapshot newest,
                out long newestTick))
            {
                return;
            }

            _snapshotBuffer.ClearSamples();
            _snapshotBuffer.Insert(
                in newest,
                out newestTick,
                out _);
            _estimatedServerClock.Anchor(
                newestTick,
                arrivalTimeSeconds);
            _latestOfficialSnapshot = newest;
            _lastOfficialServerTick = newest.ServerTick;
            _hasOfficialSnapshot = true;
            _interpolationStarved = false;
            _interpolationResetCount = IncrementSaturated(
                _interpolationResetCount);
        }

        private void AdvanceOwnerInputClock()
        {
            if (!_ownerInputHistory.IsReady
                || _hardResyncRequested)
            {
                _ownerInputAccumulatorSeconds = 0d;
                return;
            }

            _ownerInputAccumulatorSeconds += Time.unscaledDeltaTime;
            if (_ownerInputAccumulatorSeconds
                > _tickDeltaSeconds * MaxTickDebt)
            {
                RequestHardResync();
                return;
            }

            int producedThisFrame = 0;
            while (_ownerInputAccumulatorSeconds >= _tickDeltaSeconds
                && producedThisFrame < MaxTicksPerFrame)
            {
                if (!ProduceOwnerInput()) return;

                _ownerInputAccumulatorSeconds -= _tickDeltaSeconds;
                producedThisFrame++;
            }
        }

        private bool ProduceOwnerInput()
        {
            if (_ownerPredictionSimulation == null
                || !_ownerPredictionSimulation.IsInitialized
                || _ownerInputHistory.Count >= InputHistory.Capacity
                || _predictionHistory.Count >= PredictionHistory.Capacity)
            {
                _ownerHistoryFullCount = IncrementSaturated(
                    _ownerHistoryFullCount);
                RequestHardResync();
                RefreshOwnerInputDiagnostics();
                return false;
            }

            uint flapPressSequence =
                _localInputCapture.FlapPressSequence;
            bool carriesNewFlapPress = flapPressSequence
                != _lastProducedFlapPressSequence;
            InputCommand command = new InputCommand(
                _ownerInputHistory.NextInputTick,
                _localInputCapture.Horizontal,
                _localInputCapture.FlapHeld || carriesNewFlapPress);
            InputHistoryStatus status = _ownerInputHistory.TryAppend(
                _ownerInputHistory.InputEpoch,
                in command);
            if (status == InputHistoryStatus.HistoryFull)
            {
                _ownerHistoryFullCount = IncrementSaturated(
                    _ownerHistoryFullCount);
                RequestHardResync();
                RefreshOwnerInputDiagnostics();
                return false;
            }

            if (status != InputHistoryStatus.Success)
            {
                RequestHardResync();
                return false;
            }

            OwnerPredictionStatus predictionStatus;
            PredictionSnapshot predictionSnapshot;
            using (PredictionStepMarker.Auto())
            {
                predictionStatus = _ownerPredictionSimulation.Step(
                    _ownerInputHistory.InputEpoch,
                    in command,
                    out predictionSnapshot);
            }

            if (predictionStatus != OwnerPredictionStatus.Success
                || _predictionHistory.TryAppend(in predictionSnapshot)
                    != PredictionHistoryStatus.Success)
            {
                RequestHardResync();
                return false;
            }

            _latestPredictedFighter = predictionSnapshot.Fighter;
            _hasPredictedFighter = true;

            _lastProducedFlapPressSequence = flapPressSequence;
            _producedOwnerInputCount = IncrementSaturated(
                _producedOwnerInputCount);
            RefreshOwnerInputDiagnostics();
            return true;
        }

        private void RequestHardResync()
        {
            if (!_hardResyncRequested)
            {
                _hardResyncCount = IncrementSaturated(_hardResyncCount);
            }

            _hardResyncRequested = true;
            _hardResyncRequestPending = true;
            _ownerInputAccumulatorSeconds = 0d;
        }

        private void RefreshOwnerInputDiagnostics()
        {
            _ownerInputHistoryCount = _ownerInputHistory.Count;
            _ownerInputEpoch = _ownerInputHistory.InputEpoch;
            _nextOwnerInputTick = _ownerInputHistory.NextInputTick;
            _predictionHistoryCount = _predictionHistory.Count;
        }

        private void MeasurePredictionError(in ReconciliationResult result)
        {
            _latestPredictionPositionError = Mathf.Sqrt(
                result.PositionErrorSquared);
            _latestPredictionVelocityError = Mathf.Sqrt(
                result.VelocityErrorSquared);
            if (_latestPredictionPositionError
                > _maximumPredictionPositionError)
            {
                _maximumPredictionPositionError =
                    _latestPredictionPositionError;
            }
        }

        private void ResetPredictionErrors()
        {
            _latestPredictionPositionError = 0f;
            _latestPredictionVelocityError = 0f;
            _maximumPredictionPositionError = 0f;
        }

        private void AdvanceServerSimulation()
        {
            _accumulatorSeconds += Time.unscaledDeltaTime;
            double maximumDebtSeconds = _tickDeltaSeconds * MaxTickDebt;
            if (_accumulatorSeconds > maximumDebtSeconds)
            {
                StopWithError(
                    TickRunnerStatus.TickDebtExceeded,
                    "Balloon Fight server tick debt exceeded 50 ticks.");
                return;
            }

            _lastFrameTickCount = 0;
            while (_accumulatorSeconds >= _tickDeltaSeconds
                && _lastFrameTickCount < MaxTicksPerFrame)
            {
                using (SimulationStepMarker.Auto())
                {
                    if (!ServerSimulationStep()) return;
                }

                _accumulatorSeconds -= _tickDeltaSeconds;
                _lastFrameTickCount++;
                _completedServerTicks++;
            }

            if (_lastFrameTickCount > _maximumFrameTickCount)
            {
                _maximumFrameTickCount = _lastFrameTickCount;
            }

            _pendingTickDebt = (int)(_accumulatorSeconds / _tickDeltaSeconds);
        }

        private bool ServerSimulationStep()
        {
            uint tick = _simulation.NextTick;
            _inputStreamControl.CreateServerInput(
                0,
                tick,
                _inbox,
                out FighterInput fighter0,
                out NetworkInputAck slot0InputAck);
            _inputStreamControl.CreateServerInput(
                1,
                tick,
                _inbox,
                out FighterInput fighter1,
                out NetworkInputAck slot1InputAck);

            SimulationResult result = _simulation.Step(
                in fighter0,
                in fighter1);
            if (!result.Succeeded)
            {
                StopWithError(
                    TickRunnerStatus.SimulationError,
                    "Balloon Fight server simulation step failed.");
                return false;
            }

            _confirmedEventHistory.AppendFrom(_simulation.Events);
            _latestSimulationSnapshot = result.Snapshot;
            _latestSlot0InputAck = slot0InputAck;
            _latestSlot1InputAck = slot1InputAck;
            _hasSimulationSnapshot = true;

            return true;
        }

        private void RenderStep(double localTimeSeconds)
        {
            _secondsSinceLatestSnapshotReceived = System.Math.Max(
                0d,
                localTimeSeconds - _latestSnapshotArrivalTimeSeconds);
            _estimatedServerTick = _estimatedServerClock.Advance(
                localTimeSeconds);
            _renderTick = _estimatedServerTick
                - InterpolationDelayTicks;
            InterpolationSampleStatus sampleStatus =
                _snapshotBuffer.Sample(
                    _renderTick,
                    out InterpolationSample sample);
            if (sampleStatus == InterpolationSampleStatus.Empty) return;

            bool starved = sampleStatus
                == InterpolationSampleStatus.Starved;
            if (starved && !_interpolationStarved)
            {
                _interpolationStarvationCount = IncrementSaturated(
                    _interpolationStarvationCount);
            }

            _interpolationStarved = starved;
            if (!_fighter0View.AdvancePredictionCorrection(
                    Time.unscaledDeltaTime)
                || !_fighter1View.AdvancePredictionCorrection(
                    Time.unscaledDeltaTime))
            {
                RequestHardResync();
                return;
            }

            FighterState predictedOwner = _hasPredictedFighter
                ? _latestPredictedFighter
                : default;
            SimVector2 officialFighter0 = new SimVector2(
                _latestOfficialSnapshot.Fighter0.PositionX,
                _latestOfficialSnapshot.Fighter0.PositionY);
            SimVector2 officialFighter1 = new SimVector2(
                _latestOfficialSnapshot.Fighter1.PositionX,
                _latestOfficialSnapshot.Fighter1.PositionY);
            _latestRenderSnapshot = RenderSnapshot.FromPresentation(
                _latestOfficialSnapshot.ServerTick,
                _latestOfficialSnapshot.SimulationActiveMask,
                in officialFighter0,
                in officialFighter1,
                sample.ActiveMask,
                in sample.Fighter0Position,
                in sample.Fighter1Position,
                _localSlotIndex,
                _showOwnerPrediction && _hasPredictedFighter,
                in predictedOwner);
            if (!_fighter0View.RenderStep(in _latestRenderSnapshot)
                || !_fighter1View.RenderStep(in _latestRenderSnapshot))
            {
                StopWithError(
                    TickRunnerStatus.ConfigurationError,
                    "A fighter view rejected the presentation snapshot.");
            }
        }

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
                && !_fighter0View.HasForbiddenCollider
                && _fighter1View != null
                && _fighter1View.IsConfigured
                && _fighter1View.SlotIndex == 1
                && !_fighter1View.HasForbiddenCollider;
        }

        private void StopWithError(
            TickRunnerStatus status,
            string message)
        {
            _status = status;
            Debug.LogError(message, this);
        }

        private static uint IncrementSaturated(uint value)
        {
            return value == uint.MaxValue ? value : value + 1u;
        }

        private static uint AddSaturated(uint value, int addition)
        {
            if (addition <= 0) return value;

            uint remaining = uint.MaxValue - value;
            uint amount = (uint)addition;
            return amount > remaining ? uint.MaxValue : value + amount;
        }
    }
}