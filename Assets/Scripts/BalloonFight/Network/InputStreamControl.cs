using NetworkCourse.BalloonFight.Simulation;
using Unity.Netcode;

namespace NetworkCourse.BalloonFight.Networking
{
    public enum ServerInputConsumeStatus : byte
    {
        NeutralBeforeFirstCommand = 0,
        ConsumedReceived = 1,
        ConsumedSynthesized = 2,
        InactiveSlot = 3
    }

    public struct NetworkInputAck : INetworkSerializable
    {
        public uint InputEpoch;
        public bool HasProcessedInput;
        public uint LastProcessedInputTick;

        public static NetworkInputAck Unprocessed(uint inputEpoch)
        {
            return new NetworkInputAck
            {
                InputEpoch = inputEpoch,
                HasProcessedInput = false,
                LastProcessedInputTick = 0u
            };
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer)
            where T : IReaderWriter
        {
            serializer.SerializeValue(ref InputEpoch);
            serializer.SerializeValue(ref HasProcessedInput);
            serializer.SerializeValue(ref LastProcessedInputTick);
        }
    }

    public sealed class InputStreamControl
    {
        private struct SlotState
        {
            public bool IsActive;
            public uint InputEpoch;
            public bool HasStarted;
            public uint NextExpectedInputTick;
            public bool HasProcessedInput;
            public uint LastProcessedInputTick;
            public float HeldHorizontal;
            public bool HeldFlap;
        }

        private SlotState _slot0;
        private SlotState _slot1;
        private uint _receivedInputCount;
        private uint _synthesizedInputCount;
        private uint _rebaseCount;

        public uint ReceivedInputCount => _receivedInputCount;
        public uint SynthesizedInputCount => _synthesizedInputCount;
        public uint RebaseCount => _rebaseCount;

        public bool TryActivateSlot(
            int slot,
            NetworkCommandInbox inbox,
            out uint inputEpoch)
        {
            if (inbox == null || !TryGetState(slot, out SlotState state))
            {
                inputEpoch = 0u;
                return false;
            }

            if (state.IsActive)
            {
                inputEpoch = state.InputEpoch;
                return true;
            }

            state.InputEpoch = unchecked(state.InputEpoch + 1u);
            ResetForEpoch(ref state);
            state.IsActive = true;
            SetState(slot, in state);
            inbox.BeginEpoch(slot, state.InputEpoch);
            inputEpoch = state.InputEpoch;
            return true;
        }

        public bool TryDeactivateSlot(
            int slot,
            NetworkCommandInbox inbox)
        {
            if (inbox == null || !TryGetState(slot, out SlotState state))
            {
                return false;
            }

            state.IsActive = false;
            ResetForEpoch(ref state);
            SetState(slot, in state);
            inbox.ResetSlot(slot);
            return true;
        }

        public bool TryRebase(
            int slot,
            uint expectedEpoch,
            NetworkCommandInbox inbox,
            out uint newEpoch)
        {
            if (inbox == null
                || !TryGetState(slot, out SlotState state)
                || !state.IsActive
                || state.InputEpoch != expectedEpoch)
            {
                newEpoch = 0u;
                return false;
            }

            state.InputEpoch = unchecked(state.InputEpoch + 1u);
            ResetForEpoch(ref state);
            SetState(slot, in state);
            inbox.BeginEpoch(slot, state.InputEpoch);
            _rebaseCount = IncrementSaturated(_rebaseCount);
            newEpoch = state.InputEpoch;
            return true;
        }

        public bool TryGetReceiveWindow(
            int slot,
            out uint inputEpoch,
            out bool hasProcessedInput,
            out uint lastProcessedInputTick)
        {
            if (!TryGetState(slot, out SlotState state)
                || !state.IsActive)
            {
                inputEpoch = 0u;
                hasProcessedInput = false;
                lastProcessedInputTick = 0u;
                return false;
            }

            inputEpoch = state.InputEpoch;
            hasProcessedInput = state.HasProcessedInput;
            lastProcessedInputTick = state.LastProcessedInputTick;
            return true;
        }

        public ServerInputConsumeStatus CreateServerInput(
            int slot,
            uint serverTick,
            NetworkCommandInbox inbox,
            out FighterInput input,
            out NetworkInputAck ack)
        {
            if (inbox == null
                || !TryGetState(slot, out SlotState state)
                || !state.IsActive)
            {
                input = FighterInput.Neutral(serverTick);
                ack = default;
                return ServerInputConsumeStatus.InactiveSlot;
            }

            if (!state.HasStarted)
            {
                if (!inbox.TryPeekEarliest(
                    slot,
                    0u,
                    out InputCommand first))
                {
                    input = FighterInput.Neutral(serverTick);
                    ack = NetworkInputAck.Unprocessed(state.InputEpoch);
                    return ServerInputConsumeStatus
                        .NeutralBeforeFirstCommand;
                }

                state.HasStarted = true;
                state.NextExpectedInputTick = first.InputTick;
            }

            bool received = inbox.TryTakeExpected(
                slot,
                state.NextExpectedInputTick,
                out InputCommand command);
            if (received)
            {
                state.HeldHorizontal = command.Horizontal;
                state.HeldFlap = command.FlapHeld;
                _receivedInputCount = IncrementSaturated(
                    _receivedInputCount);
            }
            else
            {
                command = new InputCommand(
                    state.NextExpectedInputTick,
                    state.HeldHorizontal,
                    state.HeldFlap);
                _synthesizedInputCount = IncrementSaturated(
                    _synthesizedInputCount);
            }

            state.HasProcessedInput = true;
            state.LastProcessedInputTick = state.NextExpectedInputTick;
            state.NextExpectedInputTick = unchecked(
                state.NextExpectedInputTick + 1u);
            input = new FighterInput(
                serverTick,
                command.Horizontal,
                command.FlapHeld);
            ack = new NetworkInputAck
            {
                InputEpoch = state.InputEpoch,
                HasProcessedInput = true,
                LastProcessedInputTick = state.LastProcessedInputTick
            };
            SetState(slot, in state);
            return received
                ? ServerInputConsumeStatus.ConsumedReceived
                : ServerInputConsumeStatus.ConsumedSynthesized;
        }

        public bool TryGetAck(int slot, out NetworkInputAck ack)
        {
            if (!TryGetState(slot, out SlotState state)
                || !state.IsActive)
            {
                ack = default;
                return false;
            }

            ack = new NetworkInputAck
            {
                InputEpoch = state.InputEpoch,
                HasProcessedInput = state.HasProcessedInput,
                LastProcessedInputTick = state.LastProcessedInputTick
            };
            return true;
        }

        private bool TryGetState(int slot, out SlotState state)
        {
            if (slot == 0)
            {
                state = _slot0;
                return true;
            }

            if (slot == 1)
            {
                state = _slot1;
                return true;
            }

            state = default;
            return false;
        }

        private void SetState(int slot, in SlotState state)
        {
            if (slot == 0) _slot0 = state;
            else if (slot == 1) _slot1 = state;
        }

        private static void ResetForEpoch(ref SlotState state)
        {
            state.HasStarted = false;
            state.NextExpectedInputTick = 0u;
            state.HasProcessedInput = false;
            state.LastProcessedInputTick = 0u;
            state.HeldHorizontal = 0f;
            state.HeldFlap = false;
        }

        private static uint IncrementSaturated(uint value)
        {
            return value == uint.MaxValue ? value : value + 1u;
        }
    }
}