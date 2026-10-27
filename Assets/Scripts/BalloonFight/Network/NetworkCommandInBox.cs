using NetworkCourse.BalloonFight.Simulation;

namespace NetworkCourse.BalloonFight.Networking
{

    public enum IntentAcceptStatus : byte
    {
        Accepted = 0,
        InvalidSlot = 1,
        InvalidPayload = 2,
        StaleOrDuplicate = 3,
        SequenceGapTooLarge = 4,
        InvalidFlapSequence = 5
    }

    internal struct SlotCommandState
    {
        public bool HasIntent;
        public float Horizontal;
        public bool FlapHeld;
        public uint LastIntentSequence;
        public uint LastFlapPressSequence;
        public bool PendingFlapPress;
    }

    public sealed class NetworkCommandInbox
    {

        private const uint MaxIntentSequenceAdvance = 1_024u;
        private const uint MaxFlapSequenceAdvance = 32u;

        private SlotCommandState _slot0;
        private SlotCommandState _slot1;

        public uint AcceptedIntents { get; private set; }
        public uint RejectedIntents { get; private set; }
        public uint CoalescedFlapPresses { get; private set; }

        public IntentAcceptStatus TryAccept(
            int slot,
            in IntentMessage message)
        {
            if (slot < 0 || slot > 1)
            {
                RejectedIntents = IncrementSaturated(RejectedIntents);
                return IntentAcceptStatus.InvalidSlot;
            }

            if (!message.IsPayloadValid() || message.ClaimedSlot != slot)
            {
                RejectedIntents = IncrementSaturated(RejectedIntents);
                return IntentAcceptStatus.InvalidPayload;
            }

            ref SlotCommandState state = ref GetState(slot);
            if (!TryValidateIntentSequence(in state, in message))
            {
                RejectedIntents = IncrementSaturated(RejectedIntents);
                return ClassifyIntentSequenceFailure(in state, in message);
            }

            uint flapDistance = unchecked(
                message.FlapPressSequence - state.LastFlapPressSequence);
            if (flapDistance > 0x7FFF_FFFFu
                || flapDistance > MaxFlapSequenceAdvance)
            {
                RejectedIntents = IncrementSaturated(RejectedIntents);
                return IntentAcceptStatus.InvalidFlapSequence;
            }

            if (flapDistance > 0u)
            {
                uint coalesced = state.PendingFlapPress
                    ? flapDistance
                    : flapDistance - 1u;
                CoalescedFlapPresses = AddSaturated(
                    CoalescedFlapPresses,
                    coalesced);
                state.PendingFlapPress = true;
                state.LastFlapPressSequence = message.FlapPressSequence;
            }

            state.HasIntent = true;
            state.Horizontal = message.Horizontal;
            state.FlapHeld = message.FlapHeld;
            state.LastIntentSequence = message.IntentSequence;
            AcceptedIntents = IncrementSaturated(AcceptedIntents);
            return IntentAcceptStatus.Accepted;
        }

        // Coalescing != Buffering

        public FighterInput CreateInput(
            int slot,
            uint tick,
            bool flapCanExecute)
        {
            if (slot < 0 || slot > 1)
            {
                return FighterInput.Neutral(tick);
            }

            ref SlotCommandState state = ref GetState(slot);
            if (!state.HasIntent)
            {
                return FighterInput.Neutral(tick);
            }

            bool consumePending = state.PendingFlapPress && flapCanExecute;
            bool flapRequested = state.FlapHeld || consumePending;
            if (consumePending)
            {
                state.PendingFlapPress = false;
            }

            return new FighterInput(tick, state.Horizontal, flapRequested);
        }

        public void ResetSlot(int slot)
        {
            if (slot == 0)
            {
                _slot0 = default;
            }
            else if (slot == 1)
            {
                _slot1 = default;
            }
        }

        public bool HasPendingFlapPress(int slot)
        {
            if (slot == 0) return _slot0.PendingFlapPress;
            if (slot == 1) return _slot1.PendingFlapPress;
            return false;
        }

        private static bool TryValidateIntentSequence(
            in SlotCommandState state,
            in IntentMessage message)
        {
            if (!state.HasIntent)
            {
                return message.IntentSequence <= MaxIntentSequenceAdvance;
            }

            return SequenceMath.TryGetForwardDistance(
                message.IntentSequence,
                state.LastIntentSequence,
                MaxIntentSequenceAdvance,
                out _
            );
        }

        private static IntentAcceptStatus ClassifyIntentSequenceFailure(
            in SlotCommandState state,
            in IntentMessage message)
        {
            if (!state.HasIntent)
            {
                return IntentAcceptStatus.SequenceGapTooLarge;
            }

            uint distance = unchecked(
                message.IntentSequence - state.LastIntentSequence);
            return distance == 0u || distance > 0x7FFF_FFFFu
                ? IntentAcceptStatus.StaleOrDuplicate
                : IntentAcceptStatus.SequenceGapTooLarge;
        }

        private ref SlotCommandState GetState(int slot)
        {
            if (slot == 0) return ref _slot0;
            return ref _slot1;
        }

        private static uint IncrementSaturated(uint value)
        {
            return value == uint.MaxValue ? value : value + 1u;
        }

        private static uint AddSaturated(uint value, uint increment)
        {
            return uint.MaxValue - value < increment
                ? uint.MaxValue
                : value + increment;
        }

    }

}