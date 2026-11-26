using NetworkCourse.BalloonFight.Engine;
using NetworkCourse.BalloonFight.Simulation;
using Unity.Netcode;
using UnityEngine;

namespace NetworkCourse.BalloonFight.Networking
{
    public enum BalloonBridgeStatus : byte
    {
        Idle = 0,
        Running = 1,
        ConfigurationError = 2,
        RunnerError = 3
    }

    public sealed class BalloonNetworkBridge : NetworkBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private NetworkManager _networkManager;
        [SerializeField] private BalloonNetworkSession _session;
        [SerializeField] private BalloonTickRunner _tickRunner;
        [SerializeField] private BalloonInputCapture _localInputCapture;

        [Header("Runtime diagnostics")]
        [SerializeField] private BalloonBridgeStatus _status;
        [SerializeField] private int _localSlotIndex = -1;
        [SerializeField] private int _inputBatchesPerSecond;
        [SerializeField] private int _acceptedCommandsPerSecond;
        [SerializeField] private int _snapshotsPerSecond;
        [SerializeField] private ulong _currentRttMilliseconds;
        [SerializeField] private uint _ownershipRejections;
        [SerializeField] private uint _validationRejections;
        [SerializeField] private uint _wrongEpochRejections;
        [SerializeField] private uint _duplicateCommands;
        [SerializeField] private uint _staleCommands;
        [SerializeField] private uint _serverInboxFullCount;
        [SerializeField] private uint _hardRebaseRequestCount;
        [SerializeField] private uint _totalSentBatches;
        [SerializeField] private uint _totalReceivedSnapshots;

        private readonly NetworkCommandInbox _inbox =
            new NetworkCommandInbox();
        private readonly InputStreamControl _inputStreamControl =
            new InputStreamControl();

        private uint _lastPublishedServerTick;
        private bool _hasPublishedSnapshot;
        private bool _networkTickSubscribed;
        private float _rateWindowSeconds;
        private int _sentBatchesInWindow;
        private int _acceptedCommandsInWindow;
        private int _receivedSnapshotsInWindow;

        public BalloonBridgeStatus Status => _status;
        public int LocalSlotIndex => _localSlotIndex;
        public int InputBatchesPerSecond => _inputBatchesPerSecond;
        public int AcceptedCommandsPerSecond =>
            _acceptedCommandsPerSecond;
        public int SnapshotsPerSecond => _snapshotsPerSecond;
        public ulong CurrentRttMilliseconds => _currentRttMilliseconds;
        public uint OwnershipRejections => _ownershipRejections;
        public uint ValidationRejections => _validationRejections;
        public uint WrongEpochRejections => _wrongEpochRejections;
        public uint DuplicateCommands => _duplicateCommands;
        public uint StaleCommands => _staleCommands;
        public uint ServerInboxFullCount => _serverInboxFullCount;
        public uint HardRebaseRequestCount => _hardRebaseRequestCount;
        public uint TotalSentBatches => _totalSentBatches;
        public uint TotalReceivedSnapshots => _totalReceivedSnapshots;

        private void Awake()
        {
            if (!ValidateReferences())
            {
                StopWithError(
                    BalloonBridgeStatus.ConfigurationError,
                    "BalloonNetworkBridge dependencies are invalid.");
                return;
            }

            _status = BalloonBridgeStatus.Idle;
        }

        private void Update()
        {
            if (_status != BalloonBridgeStatus.Running) return;

            UpdateRateWindow(Time.unscaledDeltaTime);
        }

        public override void OnNetworkSpawn()
        {
            if (_status == BalloonBridgeStatus.ConfigurationError) return;

            if (NetworkManager != _networkManager
                || _networkManager.NetworkTickSystem == null)
            {
                StopWithError(
                    BalloonBridgeStatus.ConfigurationError,
                    "The injected NetworkManager does not own this bridge.");
                return;
            }

            _localInputCapture.ResetNetworkSequences();
            _inbox.ResetSlot(0);
            _inbox.ResetSlot(1);
            _localSlotIndex = -1;
            _tickRunner.SetLocalSlot(-1);
            _hasPublishedSnapshot = false;
            _networkManager.NetworkTickSystem.Tick += HandleNetworkTick;
            _networkTickSubscribed = true;

            bool runnerStarted = IsServer
                ? _tickRunner.StartServer(_inbox, _inputStreamControl)
                : _tickRunner.StartClient();
            bool ownerStreamStarted = !_networkManager.IsClient
                || _tickRunner.StartLocalOwnerStream(_localInputCapture);
            if (!runnerStarted || !ownerStreamStarted)
            {
                DetachNetworkCallbacks();
                StopWithError(
                    BalloonBridgeStatus.RunnerError,
                    "BalloonTickRunner refused its network role.");
                return;
            }

            if (IsServer)
            {
                _session.SlotAssigned += HandleSlotAssigned;
                _session.SlotReleased += HandleSlotReleased;
                ActivateOccupiedSlot(0);
                ActivateOccupiedSlot(1);
                _tickRunner.SetServerSimulationEnabled(
                    _session.ConnectedSlotCount == 2);
            }

            _status = BalloonBridgeStatus.Running;
        }

        public override void OnNetworkDespawn()
        {
            DetachNetworkCallbacks();
            _tickRunner.StopNetworkMode();
            _inputStreamControl.TryDeactivateSlot(0, _inbox);
            _inputStreamControl.TryDeactivateSlot(1, _inbox);
            _localSlotIndex = -1;
            _currentRttMilliseconds = 0ul;
            _status = BalloonBridgeStatus.Idle;
        }

        public override void OnDestroy()
        {
            DetachNetworkCallbacks();
            base.OnDestroy();
        }

        private void HandleNetworkTick()
        {
            if (_status != BalloonBridgeStatus.Running) return;

            if (_networkManager.IsClient && _localSlotIndex >= 0)
            {
                if (!SendHardRebaseRequestIfNeeded())
                {
                    SendNewestInputBatch();
                }
            }

            if (_networkManager.IsServer)
            {
                PublishLatestSnapshot();
            }
        }

        private void SendNewestInputBatch()
        {
            if (!_tickRunner.TryCreateNewestInputBatch(
                _localSlotIndex,
                out InputBatch batch))
            {
                return;
            }

            SubmitInputBatchRpc(batch);
            CountSentBatch();
        }

        private bool SendHardRebaseRequestIfNeeded()
        {
            if (!_tickRunner.TryTakeHardResyncRequest(
                out uint inputEpoch))
            {
                return false;
            }

            RequestInputRebaseRpc((byte)_localSlotIndex, inputEpoch);
            return true;
        }

        private void PublishLatestSnapshot()
        {
            if (!_tickRunner.TryGetLatestSimulationSnapshot(
                out SimulationSnapshot simulationSnapshot,
                out NetworkInputAck slot0InputAck,
                out NetworkInputAck slot1InputAck))
            {
                return;
            }

            if (_hasPublishedSnapshot
                && simulationSnapshot.Tick == _lastPublishedServerTick)
            {
                return;
            }

            _session.TryGetClient(0, out ulong slot0ClientId);
            _session.TryGetClient(1, out ulong slot1ClientId);

            if (!_tickRunner.TryCreateConfirmedEventWindows(
                out ConfirmedEventWindow slot0Events,
                out ConfirmedEventWindow slot1Events))
            {
                return;
            }

            NetworkSnapshot message = NetworkSnapshot.Create(
                in simulationSnapshot,
                _session.OccupiedMask,
                slot0ClientId,
                slot1ClientId,
                in slot0InputAck,
                in slot1InputAck,
                in slot0Events,
                in slot1Events);

            _lastPublishedServerTick = simulationSnapshot.Tick;
            _hasPublishedSnapshot = true;
            PublishOfficialSnapshotRpc(message);
        }

        [Rpc(
            SendTo.Server,
            Delivery = RpcDelivery.Unreliable,
            InvokePermission = RpcInvokePermission.Everyone)]
        private void SubmitInputBatchRpc(
            InputBatch batch,
            RpcParams rpcParams = default)
        {
            ulong senderClientId = rpcParams.Receive.SenderClientId;
            SlotClaimStatus claimStatus = _session.TryValidateSender(
                senderClientId,
                batch.ClaimedSlot,
                out int validatedSlot);
            if (claimStatus != SlotClaimStatus.Valid)
            {
                _ownershipRejections = IncrementSaturated(
                    _ownershipRejections);
                return;
            }

            if (!_inputStreamControl.TryGetReceiveWindow(
                validatedSlot,
                out uint inputEpoch,
                out bool hasProcessedInput,
                out uint lastProcessedInputTick))
            {
                _validationRejections = IncrementSaturated(
                    _validationRejections);
                return;
            }

            if (!batch.IsStructurallyValid())
            {
                _validationRejections = IncrementSaturated(
                    _validationRejections);
                return;
            }

            if (batch.InputEpoch == inputEpoch
                && !_tickRunner.ObserveConfirmedEventCursor(
                    validatedSlot,
                    batch.HasSeenConfirmedEvent,
                    batch.LastSeenConfirmedEventId))
            {
                _validationRejections = IncrementSaturated(
                    _validationRejections);
            }

            InputBatchAcceptResult result = _inbox.TryAcceptBatch(
                validatedSlot,
                inputEpoch,
                hasProcessedInput,
                lastProcessedInputTick,
                in batch);
            CountBatchResult(in result);
            if (result.Status == InputBatchAcceptStatus.InboxFull)
            {
                _serverInboxFullCount = IncrementSaturated(
                    _serverInboxFullCount);
                _inputStreamControl.TryRebase(
                    validatedSlot,
                    inputEpoch,
                    _inbox,
                    out _);
            }
        }

        [Rpc(
            SendTo.Server,
            Delivery = RpcDelivery.Reliable,
            InvokePermission = RpcInvokePermission.Everyone)]
        private void RequestInputRebaseRpc(
            byte claimedSlot,
            uint expectedEpoch,
            RpcParams rpcParams = default)
        {
            ulong senderClientId = rpcParams.Receive.SenderClientId;
            if (_session.TryValidateSender(
                senderClientId,
                claimedSlot,
                out int validatedSlot) != SlotClaimStatus.Valid)
            {
                _ownershipRejections = IncrementSaturated(
                    _ownershipRejections);
                return;
            }

            if (_inputStreamControl.TryRebase(
                validatedSlot,
                expectedEpoch,
                _inbox,
                out _))
            {
                _hardRebaseRequestCount = IncrementSaturated(
                    _hardRebaseRequestCount);
            }
        }

        [Rpc(
            SendTo.ClientsAndHost,
            Delivery = RpcDelivery.Unreliable,
            InvokePermission = RpcInvokePermission.Server)]
        private void PublishOfficialSnapshotRpc(NetworkSnapshot message)
        {
            bool accepted = _tickRunner.TryReceiveOfficialSnapshot(
                in message,
                Time.realtimeSinceStartupAsDouble);
            if (!accepted) return;

            _localSlotIndex = message.TryGetLocalSlot(
                _networkManager.LocalClientId,
                out int localSlot)
                ? localSlot
                : -1;

            _tickRunner.SetLocalSlot(_localSlotIndex);

            if (_localSlotIndex >= 0)
            {
                if (message.TryGetLocalConfirmedEvents(
                    _networkManager.LocalClientId,
                    out ConfirmedEventWindow confirmedEvents))
                {
                    _tickRunner.StageLocalConfirmedEvents(
                        message.ServerTick,
                        in confirmedEvents);
                }
                else
                {
                    _validationRejections = IncrementSaturated(
                        _validationRejections);
                }
            }

            if (message.TryGetLocalPredictionBase(
                _networkManager.LocalClientId,
                out NetworkInputAck ack,
                out FighterState officialFighter))
            {
                _tickRunner.StageLocalOfficialState(
                    message.ServerTick,
                    in ack,
                    in officialFighter);
            }

            _receivedSnapshotsInWindow++;
            _totalReceivedSnapshots = IncrementSaturated(
                _totalReceivedSnapshots);
        }

        private void HandleSlotReleased(int slot)
        {
            _inputStreamControl.TryDeactivateSlot(slot, _inbox);
            _tickRunner.DeactivateConfirmedEventSlot(slot);
            _tickRunner.SetServerSimulationEnabled(false);
        }

        private void HandleSlotAssigned(int slot)
        {
            _inputStreamControl.TryActivateSlot(slot, _inbox, out _);
            _tickRunner.ActivateConfirmedEventSlot(slot);
            _tickRunner.SetServerSimulationEnabled(
                _session.ConnectedSlotCount == 2);
        }

        private void ActivateOccupiedSlot(int slot)
        {
            if (!_session.TryGetClient(slot, out _)) return;

            _inputStreamControl.TryActivateSlot(slot, _inbox, out _);
            _tickRunner.ActivateConfirmedEventSlot(slot);
        }

        private void CountSentBatch()
        {
            _sentBatchesInWindow++;
            _totalSentBatches = IncrementSaturated(_totalSentBatches);
        }

        private void CountBatchResult(in InputBatchAcceptResult result)
        {
            _acceptedCommandsInWindow += result.AcceptedCount;
            _duplicateCommands = AddSaturated(
                _duplicateCommands,
                result.DuplicateCount);
            _staleCommands = AddSaturated(
                _staleCommands,
                result.StaleCount);

            if (result.Status == InputBatchAcceptStatus.WrongInputEpoch)
            {
                _wrongEpochRejections = IncrementSaturated(
                    _wrongEpochRejections);
            }
            else if (result.Status != InputBatchAcceptStatus.Accepted
                && result.Status != InputBatchAcceptStatus.NoNewCommands
                && result.Status != InputBatchAcceptStatus.InboxFull)
            {
                _validationRejections = IncrementSaturated(
                    _validationRejections);
            }
        }

        private void UpdateRateWindow(float unscaledDeltaTime)
        {
            _rateWindowSeconds += unscaledDeltaTime;
            if (_rateWindowSeconds < 1f) return;

            float inverseDuration = 1f / _rateWindowSeconds;
            _inputBatchesPerSecond = Mathf.RoundToInt(
                _sentBatchesInWindow * inverseDuration);
            _acceptedCommandsPerSecond = Mathf.RoundToInt(
                _acceptedCommandsInWindow * inverseDuration);
            _snapshotsPerSecond = Mathf.RoundToInt(
                _receivedSnapshotsInWindow * inverseDuration);
            _currentRttMilliseconds = _networkManager.IsClient
                ? _networkManager.NetworkConfig.NetworkTransport
                    .GetCurrentRtt(NetworkManager.ServerClientId)
                : 0ul;

            _rateWindowSeconds = 0f;
            _sentBatchesInWindow = 0;
            _acceptedCommandsInWindow = 0;
            _receivedSnapshotsInWindow = 0;
        }

        private void DetachNetworkCallbacks()
        {
            if (_networkTickSubscribed
                && _networkManager != null
                && _networkManager.NetworkTickSystem != null)
            {
                _networkManager.NetworkTickSystem.Tick -= HandleNetworkTick;
            }

            _networkTickSubscribed = false;
            if (_session != null)
            {
                _session.SlotAssigned -= HandleSlotAssigned;
                _session.SlotReleased -= HandleSlotReleased;
            }
        }

        private bool ValidateReferences()
        {
            return _networkManager != null
                && _session != null
                && _tickRunner != null
                && _localInputCapture != null;
        }

        private void StopWithError(
            BalloonBridgeStatus status,
            string message)
        {
            _status = status;
            Debug.LogError(message, this);
        }

        private static uint IncrementSaturated(uint value)
        {
            return value == uint.MaxValue ? value : value + 1u;
        }

        private static uint AddSaturated(uint value, byte addition)
        {
            uint remaining = uint.MaxValue - value;
            return addition > remaining ? uint.MaxValue : value + addition;
        }
    }
}