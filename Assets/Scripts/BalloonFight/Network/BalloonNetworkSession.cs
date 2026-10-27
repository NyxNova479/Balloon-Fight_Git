using System;
using Unity.Netcode;
using UnityEngine;

namespace NetworkCourse.BalloonFight.Networking
{
    public enum SlotAssignmentStatus : byte
    {
        Assigned = 0, // false
        AlreadyAssigned = 1,
        InvalidPreferredSlot = 2,
        Full = 3
    }

    public enum SlotClaimStatus : byte
    {
        Valid = 0,
        InvalidSlot = 1,
        UnknownClient = 2,
        NotOwner = 3
    }

    public struct PlayerSlotTable
    {
        private const byte Slot0Bit = 1 << 0;
        private const byte Slot1Bit = 1 << 1;

        private byte _occupiedMask;
        private ulong _slot0ClientId;
        private ulong _slot1ClientId;

        public byte OccupiedMask => _occupiedMask;
        public int Count => ((_occupiedMask & Slot0Bit) != 0 ? 1 : 0)
            + ((_occupiedMask & Slot1Bit) != 0 ? 1 : 0);


        public SlotAssignmentStatus TryAssign(
            ulong clientId,
            int preferredSlot,
            out int assignedSlot)
        {
            if (TryGetSlot(clientId, out assignedSlot))
            {
                return SlotAssignmentStatus.AlreadyAssigned;
            }

            if (preferredSlot < 0 || preferredSlot > 1)
            {
                assignedSlot = -1;
                return SlotAssignmentStatus.InvalidPreferredSlot;
            }

            if (IsFree(preferredSlot))
            {
                Assign(preferredSlot, clientId);
                assignedSlot = preferredSlot;
                return SlotAssignmentStatus.Assigned;
            }

            int alternateSlot = preferredSlot == 0 ? 1 : 0;
            if (IsFree(alternateSlot))
            {
                Assign(alternateSlot, clientId);
                assignedSlot = alternateSlot;
                return SlotAssignmentStatus.Assigned;
            }

            assignedSlot = -1;
            return SlotAssignmentStatus.Full;
        }

        public bool Release(ulong clientId, out int releasedSlot)
        {
            if (!TryGetSlot(clientId, out releasedSlot))
            {
                return false;
            }

            if (releasedSlot == 0)
            {
                _occupiedMask = (byte)(_occupiedMask & ~Slot0Bit);
                _slot0ClientId = 0u;
            }
            else
            {
                _occupiedMask = (byte)(_occupiedMask & ~Slot1Bit);
                _slot1ClientId = 0u;
            }

            return true;
        }

        public SlotClaimStatus ValidateClaim(
            ulong clientId,
            int claimedSlot)
        {
            if (claimedSlot < 0 || claimedSlot > 1)
            {
                return SlotClaimStatus.InvalidSlot;
            }

            if (!TryGetSlot(clientId, out int actualSlot))
            {
                return SlotClaimStatus.UnknownClient;
            }

            return actualSlot == claimedSlot
                ? SlotClaimStatus.Valid
                : SlotClaimStatus.NotOwner;
        }

        public bool TryGetSlot(ulong clientId, out int slot)
        {

            if ((_occupiedMask & Slot0Bit) != 0
                && _slot0ClientId == clientId)
            {
                slot = 0;
                return true;
            }

            if ((_occupiedMask & Slot1Bit) != 0
                && _slot1ClientId == clientId)
            {
                slot = 1;
                return true;
            }

            slot = -1;
            return false;

        }

        public bool TryGetClient(int slot, out ulong clientId)
        {
            if (slot == 0 && (_occupiedMask & Slot0Bit) != 0)
            {
                clientId = _slot0ClientId;
                return true;
            }

            if (slot == 1 && (_occupiedMask & Slot1Bit) != 0)
            {
                clientId = _slot1ClientId;
                return true;
            }

            clientId = 0u;
            return false;
        }

        public void Clear()
        {
            _occupiedMask = 0;
            _slot0ClientId = 0u;
            _slot1ClientId = 0u;
        }

        private bool IsFree(int slot)
        {
            byte bit = slot == 0 ? Slot0Bit : Slot1Bit;
            return (_occupiedMask & bit) == 0;
        }

        private void Assign(int slot, ulong clientId)
        {
            if (slot == 0)
            {
                _slot0ClientId = clientId;
                _occupiedMask = (byte)(_occupiedMask | Slot0Bit);
                return;
            }

            _slot1ClientId = clientId;
            _occupiedMask = (byte)(_occupiedMask | Slot1Bit);
        }

    }

    public sealed class BalloonNetworkSession : MonoBehaviour
    {

        [SerializeField] private NetworkManager _networkManager;
        [SerializeField] private int _assignedConnections;
        [SerializeField] private int _releasedConnections;
        [SerializeField] private int _rejectedConnections;

        private PlayerSlotTable _slots;
        private bool _subscribed;


        private event Action<int> SlotAssigned;
        private event Action<int> SlotReleased;


        public byte OccupiedMask => _slots.OccupiedMask;
        public int ConnectedSlotCount => _slots.Count;
        public int AssignedConnections => _assignedConnections;
        public int ReleasedConnections => _releasedConnections;
        public int RejectedConnections => _rejectedConnections;

        private void Awake()
        {
            if(_networkManager == null)
            {
                enabled = false;
                Debug.LogError("BalloonNetworkSession requires NetworkManager");
                return;
            }

            _networkManager.OnClientConnectedCallback += HandleClientConnected;
            _networkManager.OnClientDisconnectCallback += HandleClientDisconnected;
            _networkManager.OnServerStopped += HandleServerStopped;
            _subscribed = true;

        }
        private void OnDestroy()
        {
            if (!_subscribed || _networkManager == null) return;

            _networkManager.OnClientConnectedCallback -= HandleClientConnected;
            _networkManager.OnClientDisconnectCallback -= HandleClientDisconnected;
            _networkManager.OnServerStopped -= HandleServerStopped;
            _subscribed = false;
        }

        public SlotClaimStatus TryValidateSender(
            ulong senderClientId,
            int claimedSlot,
            out int validatedSlot)
        {
            SlotClaimStatus status = _slots.ValidateClaim(
                senderClientId,
                claimedSlot);
            validatedSlot = status == SlotClaimStatus.Valid
                ? claimedSlot
                : -1;
            return status;
        }

        public bool TryGetClient(int slot, out ulong clientId)
        {
            return _slots.TryGetClient(slot, out clientId);
        }

        private void HandleClientConnected(ulong clientId)
        {
            if (!_networkManager.IsServer) return;

            int preferredSlot = clientId == NetworkManager.ServerClientId
                ? 0
                : 1;
            SlotAssignmentStatus status = _slots.TryAssign(
                clientId,
                preferredSlot,
                out int assignedSlot);

            if (status == SlotAssignmentStatus.Assigned)
            {
                _assignedConnections++;
                SlotAssigned?.Invoke(assignedSlot);
                return;
            }

            if (status == SlotAssignmentStatus.AlreadyAssigned) return;

            _rejectedConnections++;
            _networkManager.DisconnectClient(clientId);
        }

        private void HandleClientDisconnected(ulong clientId)
        {
            if (!_networkManager.IsServer) return;
            if (!_slots.Release(clientId, out int releasedSlot)) return;

            _releasedConnections++;
            SlotReleased?.Invoke(releasedSlot);
        }

        private void HandleServerStopped(bool wasClient)
        {
            ReleaseAllSlots();
        }

        private void ReleaseAllSlots()
        {
            bool slot0WasOccupied = _slots.TryGetClient(0, out _);
            bool slot1WasOccupied = _slots.TryGetClient(1, out _);
            _slots.Clear();

            if (slot0WasOccupied) SlotReleased?.Invoke(0);
            if (slot1WasOccupied) SlotReleased?.Invoke(1);
        }

    }

}