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

        private readonly NetworkCommandInbox _inbox = new NetworkCommandInbox();

        private uint _nextIntentSequence;
        private uint _lastPublishedServerTick;
        private bool _hasPublishedSnapshot;
        private bool _networkTickSubscribed;

        public BalloonBridgeStatus Status => _status;
        public int LocalSlotIndex => _localSlotIndex;

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
            if (_networkManager.IsClient)
            {
                _localInputCapture.FrameUpdate();
            }
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
            _nextIntentSequence = 0u;
            _localSlotIndex = -1;
            _hasPublishedSnapshot = false;
            _networkManager.NetworkTickSystem.Tick += HandleNetworkTick;
            _networkTickSubscribed = true;

            bool runnerStarted = IsServer
                ? _tickRunner.StartServer(_inbox)
                : _tickRunner.StartClient();
            if (!runnerStarted)
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
                _tickRunner.SetServerSimulationEnabled(
                    _session.ConnectedSlotCount == 2);
            }

            _status = BalloonBridgeStatus.Running;
        }

        public override void OnNetworkDespawn()
        {
            DetachNetworkCallbacks();
            _tickRunner.StopNetworkMode();
            _inbox.ResetSlot(0);
            _inbox.ResetSlot(1);
            _localSlotIndex = -1;
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
                SendLocalIntent();
            }

            if (_networkManager.IsServer)
            {
                PublishLatestSnapshot();
            }
        }

        private void SendLocalIntent()
        {
            IntentMessage message = new IntentMessage(
                (byte)_localSlotIndex,
                _localInputCapture.Horizontal,
                _localInputCapture.FlapHeld,
                _nextIntentSequence,
                _localInputCapture.FlapPressSequence);
            _nextIntentSequence = unchecked(_nextIntentSequence + 1u);

            SubmitIntentRpc(message);
        }

        private void PublishLatestSnapshot()
        {
            if (!_tickRunner.TryGetLatestSimulationSnapshot(
                out SimulationSnapshot simulationSnapshot))
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
            NetworkSnapshot message = NetworkSnapshot.Create(
                in simulationSnapshot,
                _session.OccupiedMask,
                slot0ClientId,
                slot1ClientId);

            _lastPublishedServerTick = simulationSnapshot.Tick;
            _hasPublishedSnapshot = true;
            PublishOfficialSnapshotRpc(message);
        }

        [Rpc(
            SendTo.Server,
            Delivery = RpcDelivery.Unreliable,
            InvokePermission = RpcInvokePermission.Everyone)]
        private void SubmitIntentRpc(
            IntentMessage message,
            RpcParams rpcParams = default)
        {
            ulong senderClientId = rpcParams.Receive.SenderClientId;
            SlotClaimStatus claimStatus = _session.TryValidateSender(
                senderClientId,
                message.ClaimedSlot,
                out int validatedSlot);
            if (claimStatus != SlotClaimStatus.Valid) return;

            _inbox.TryAccept(validatedSlot, in message);
        }

        [Rpc(
            SendTo.ClientsAndHost,
            Delivery = RpcDelivery.Unreliable,
            InvokePermission = RpcInvokePermission.Server)]
        private void PublishOfficialSnapshotRpc(NetworkSnapshot message)
        {
            SimVector2 fighter0Position = new SimVector2(
                message.Fighter0.PositionX,
                message.Fighter0.PositionY);
            SimVector2 fighter1Position = new SimVector2(
                message.Fighter1.PositionX,
                message.Fighter1.PositionY);
            bool accepted = _tickRunner.TryReceiveOfficialSnapshot(
                message.ServerTick,
                message.SimulationActiveMask,
                in fighter0Position,
                in fighter1Position);
            if (!accepted) return;

            _localSlotIndex = message.TryGetLocalSlot(
                _networkManager.LocalClientId,
                out int localSlot)
                ? localSlot
                : -1;
        }

        private void HandleSlotAssigned(int slot)
        {
            _inbox.ResetSlot(slot);
            _tickRunner.SetServerSimulationEnabled(
                _session.ConnectedSlotCount == 2);
        }

        private void HandleSlotReleased(int slot)
        {
            _inbox.ResetSlot(slot);
            _tickRunner.SetServerSimulationEnabled(false);
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
    }
}