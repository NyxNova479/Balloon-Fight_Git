using NetworkCourse.BalloonFight.Simulation;

namespace NetworkCourse.BalloonFight.Networking
{
    public enum InputBatchAcceptStatus : byte
    {
        Accepted = 0,
        NoNewCommands = 1,
        WrongSlot = 2,
        WrongInputEpoch = 3,
        InvalidBatch = 4,
        OutsideReceiveWindow = 5,
        InboxFull = 6
    }

    public readonly struct InputBatchAcceptResult
    {
        public readonly InputBatchAcceptStatus Status;
        public readonly byte AcceptedCount;
        public readonly byte DuplicateCount;
        public readonly byte StaleCount;

        public InputBatchAcceptResult(
            InputBatchAcceptStatus status,
            byte acceptedCount,
            byte duplicateCount,
            byte staleCount)
        {
            Status = status;
            AcceptedCount = acceptedCount;
            DuplicateCount = duplicateCount;
            StaleCount = staleCount;
        }
    }

    public sealed class NetworkCommandInbox
    {
        public const int CapacityPerSlot = 64;

        private readonly InputCommand[] _slot0 =
            new InputCommand[CapacityPerSlot];
        private readonly InputCommand[] _slot1 =
            new InputCommand[CapacityPerSlot];

        private int _slot0Count;
        private int _slot1Count;
        private uint _slot0Epoch;
        private uint _slot1Epoch;
        private bool _slot0Ready;
        private bool _slot1Ready;

        public int GetCount(int slot)
        {
            return slot == 0 ? _slot0Count
                : slot == 1 ? _slot1Count
                : 0;
        }

        public bool BeginEpoch(int slot, uint inputEpoch)
        {
            if (slot == 0)
            {
                _slot0Count = 0;
                _slot0Epoch = inputEpoch;
                _slot0Ready = true;
                return true;
            }

            if (slot == 1)
            {
                _slot1Count = 0;
                _slot1Epoch = inputEpoch;
                _slot1Ready = true;
                return true;
            }

            return false;
        }

        public void ResetSlot(int slot)
        {
            if (slot == 0)
            {
                _slot0Count = 0;
                _slot0Epoch = 0u;
                _slot0Ready = false;
            }
            else if (slot == 1)
            {
                _slot1Count = 0;
                _slot1Epoch = 0u;
                _slot1Ready = false;
            }
        }

        public InputBatchAcceptResult TryAcceptBatch(
            int validatedSlot,
            uint activeEpoch,
            bool hasProcessedInput,
            uint lastProcessedInputTick,
            in InputBatch batch)
        {
            if (validatedSlot < 0
                || validatedSlot > 1
                || batch.ClaimedSlot != validatedSlot)
            {
                return Result(InputBatchAcceptStatus.WrongSlot);
            }

            if (!IsReady(validatedSlot)
                || GetEpoch(validatedSlot) != activeEpoch
                || batch.InputEpoch != activeEpoch)
            {
                return Result(InputBatchAcceptStatus.WrongInputEpoch);
            }

            if (!batch.IsStructurallyValid())
            {
                return Result(InputBatchAcceptStatus.InvalidBatch);
            }

            if (hasProcessedInput
                && batch.Count > 0
                && batch.TryGet(
                    batch.Count - 1,
                    out InputCommand newest)
                && SequenceMath.IsNewer(
                    newest.InputTick,
                    lastProcessedInputTick)
                && !SequenceMath.TryGetForwardDistance(
                    newest.InputTick,
                    lastProcessedInputTick,
                    CapacityPerSlot,
                    out _))
            {
                return Result(
                    InputBatchAcceptStatus.OutsideReceiveWindow);
            }

            byte accepted = 0;
            byte duplicates = 0;
            byte stale = 0;
            for (int index = 0; index < batch.Count; index++)
            {
                batch.TryGet(index, out InputCommand command);
                if (hasProcessedInput
                    && !SequenceMath.IsNewer(
                        command.InputTick,
                        lastProcessedInputTick))
                {
                    stale++;
                    continue;
                }

                if (Contains(validatedSlot, command.InputTick))
                {
                    duplicates++;
                    continue;
                }

                if (!TryAdd(validatedSlot, in command))
                {
                    return new InputBatchAcceptResult(
                        InputBatchAcceptStatus.InboxFull,
                        accepted,
                        duplicates,
                        stale);
                }

                accepted++;
            }

            return new InputBatchAcceptResult(
                accepted > 0
                    ? InputBatchAcceptStatus.Accepted
                    : InputBatchAcceptStatus.NoNewCommands,
                accepted,
                duplicates,
                stale);
        }

        public bool TryPeekEarliest(
            int slot,
            uint referenceTick,
            out InputCommand command)
        {
            InputCommand[] commands = GetCommands(slot);
            int count = GetCount(slot);
            if (commands == null || count == 0)
            {
                command = default;
                return false;
            }

            bool found = false;
            uint bestDistance = uint.MaxValue;
            command = default;
            for (int index = 0; index < count; index++)
            {
                uint tick = commands[index].InputTick;
                uint distance = unchecked(tick - referenceTick);
                if (distance >= 0x8000_0000u
                    || (found && distance >= bestDistance))
                {
                    continue;
                }

                command = commands[index];
                bestDistance = distance;
                found = true;
            }

            return found;
        }

        public bool TryTakeExpected(
            int slot,
            uint expectedInputTick,
            out InputCommand command)
        {
            InputCommand[] commands = GetCommands(slot);
            int count = GetCount(slot);
            if (commands == null)
            {
                command = default;
                return false;
            }

            for (int index = 0; index < count; index++)
            {
                if (commands[index].InputTick != expectedInputTick)
                {
                    continue;
                }

                command = commands[index];
                int lastIndex = count - 1;
                commands[index] = commands[lastIndex];
                commands[lastIndex] = default;
                SetCount(slot, lastIndex);
                return true;
            }

            command = default;
            return false;
        }

        private bool Contains(int slot, uint inputTick)
        {
            InputCommand[] commands = GetCommands(slot);
            int count = GetCount(slot);
            if (commands == null) return false;

            for (int index = 0; index < count; index++)
            {
                if (commands[index].InputTick == inputTick) return true;
            }

            return false;
        }

        private bool TryAdd(int slot, in InputCommand command)
        {
            InputCommand[] commands = GetCommands(slot);
            int count = GetCount(slot);
            if (commands == null || count >= commands.Length) return false;

            commands[count] = command;
            SetCount(slot, count + 1);
            return true;
        }

        private InputCommand[] GetCommands(int slot)
        {
            return slot == 0 ? _slot0 : slot == 1 ? _slot1 : null;
        }

        private uint GetEpoch(int slot)
        {
            return slot == 0 ? _slot0Epoch : _slot1Epoch;
        }

        private bool IsReady(int slot)
        {
            return slot == 0 ? _slot0Ready : _slot1Ready;
        }

        private void SetCount(int slot, int count)
        {
            if (slot == 0) _slot0Count = count;
            else if (slot == 1) _slot1Count = count;
        }

        private static InputBatchAcceptResult Result(
            InputBatchAcceptStatus status)
        {
            return new InputBatchAcceptResult(status, 0, 0, 0);
        }
    }
}