using NetworkCourse.BalloonFight.Common;
using NetworkCourse.BalloonFight.Simulation;

namespace NetworkCourse.BalloonFight.Networking
{
    public enum ConfirmedEventCursorStatus : byte
    {
        Success = 0,
        InvalidSlot = 1,
        InactiveSlot = 2,
        CursorAhead = 3,
        CursorRegression = 4
    }

    public sealed class ConfirmedEventHistory
    {
        public const int Capacity = 32;

        private readonly BoundedRing<ConfirmedEvent> _events =
            new BoundedRing<ConfirmedEvent>(Capacity);
        private readonly bool[] _slotActive = new bool[2];
        private readonly bool[] _slotHasSeen = new bool[2];
        private readonly uint[] _slotLastSeen = new uint[2];
        private uint _nextEventId;
        private bool _hasProduced;
        private uint _lastProducedEventId;
        private uint _overflowCount;

        public int Count => _events.Count;
        public bool HasProduced => _hasProduced;
        public uint LastProducedEventId => _lastProducedEventId;
        public uint OverflowCount => _overflowCount;

        public void Reset()
        {
            _events.Clear();
            _slotActive[0] = false;
            _slotActive[1] = false;
            _slotHasSeen[0] = false;
            _slotHasSeen[1] = false;
            _slotLastSeen[0] = 0u;
            _slotLastSeen[1] = 0u;
            _nextEventId = 0u;
            _hasProduced = false;
            _lastProducedEventId = 0u;
            _overflowCount = 0u;
        }

        public void AppendFrom(SimulationEventBuffer source)
        {
            for (int index = 0; index < source.Count; index++)
            {
                source.TryGetAt(index, out SimulationEvent value);
                ConfirmedEvent confirmed = new ConfirmedEvent(
                    _nextEventId,
                    in value);
                if (!_events.TryEnqueue(in confirmed))
                {
                    _events.TryDequeue(out _);
                    _events.TryEnqueue(in confirmed);
                    _overflowCount = IncrementSaturated(_overflowCount);
                }

                _lastProducedEventId = _nextEventId;
                _nextEventId = unchecked(_nextEventId + 1u);
                _hasProduced = true;
            }
        }

        public ConfirmedEventCursorStatus ActivateSlot(int slot)
        {
            if (!IsValidSlot(slot))
            {
                return ConfirmedEventCursorStatus.InvalidSlot;
            }

            _slotActive[slot] = true;
            _slotHasSeen[slot] = _hasProduced;
            _slotLastSeen[slot] = _lastProducedEventId;
            return ConfirmedEventCursorStatus.Success;
        }

        public ConfirmedEventCursorStatus DeactivateSlot(int slot)
        {
            if (!IsValidSlot(slot))
            {
                return ConfirmedEventCursorStatus.InvalidSlot;
            }

            _slotActive[slot] = false;
            _slotHasSeen[slot] = false;
            _slotLastSeen[slot] = 0u;
            return ConfirmedEventCursorStatus.Success;
        }

        public ConfirmedEventCursorStatus ObserveCursor(
            int slot,
            bool hasSeen,
            uint lastSeenEventId)
        {
            if (!IsValidSlot(slot))
            {
                return ConfirmedEventCursorStatus.InvalidSlot;
            }

            if (!_slotActive[slot])
            {
                return ConfirmedEventCursorStatus.InactiveSlot;
            }

            if (!hasSeen)
            {
                return _slotHasSeen[slot]
                    ? ConfirmedEventCursorStatus.CursorRegression
                    : ConfirmedEventCursorStatus.Success;
            }

            if (!_hasProduced
                || SequenceMath.IsNewer(
                    lastSeenEventId,
                    _lastProducedEventId))
            {
                return ConfirmedEventCursorStatus.CursorAhead;
            }

            if (_slotHasSeen[slot]
                && SequenceMath.IsNewer(
                    _slotLastSeen[slot],
                    lastSeenEventId))
            {
                return ConfirmedEventCursorStatus.CursorRegression;
            }

            _slotHasSeen[slot] = true;
            _slotLastSeen[slot] = lastSeenEventId;
            return ConfirmedEventCursorStatus.Success;
        }

        public ConfirmedEventCursorStatus CreateWindowForSlot(
            int slot,
            out ConfirmedEventWindow window)
        {
            window = default;
            if (!IsValidSlot(slot))
            {
                return ConfirmedEventCursorStatus.InvalidSlot;
            }

            if (!_slotActive[slot])
            {
                return ConfirmedEventCursorStatus.InactiveSlot;
            }

            window.HasBaseline = _slotHasSeen[slot];
            window.BaselineEventId = window.HasBaseline
                ? _slotLastSeen[slot]
                : 0u;
            if (_events.Count == 0)
            {
                return ConfirmedEventCursorStatus.Success;
            }

            _events.TryGetAt(0, out ConfirmedEvent oldest);
            bool hasSeen = _slotHasSeen[slot];
            uint lastSeen = _slotLastSeen[slot];
            int firstIndex = -1;
            if (!hasSeen)
            {
                firstIndex = 0;
                window.HistoryGap = oldest.EventId != 0u;
            }
            else
            {
                uint expected = unchecked(lastSeen + 1u);
                for (int index = 0; index < _events.Count; index++)
                {
                    _events.TryGetAt(index, out ConfirmedEvent candidate);
                    if (candidate.EventId == expected)
                    {
                        firstIndex = index;
                        break;
                    }
                }

                if (firstIndex < 0)
                {
                    if (lastSeen == _lastProducedEventId)
                    {
                        return ConfirmedEventCursorStatus.Success;
                    }

                    if (SequenceMath.IsNewer(
                        lastSeen,
                        _lastProducedEventId))
                    {
                        return ConfirmedEventCursorStatus.CursorAhead;
                    }

                    firstIndex = 0;
                    window.HistoryGap = true;
                }
            }

            for (int index = firstIndex;
                index < _events.Count
                    && window.Count < ConfirmedEventWindow.Capacity;
                index++)
            {
                _events.TryGetAt(index, out ConfirmedEvent value);
                window.TryAppend(in value);
            }

            return ConfirmedEventCursorStatus.Success;
        }

        private static bool IsValidSlot(int slot)
        {
            return slot == 0 || slot == 1;
        }

        private static uint IncrementSaturated(uint value)
        {
            return value == uint.MaxValue ? value : value + 1u;
        }
    }
}