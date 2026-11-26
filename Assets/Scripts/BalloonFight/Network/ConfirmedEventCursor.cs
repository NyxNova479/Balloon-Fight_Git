namespace NetworkCourse.BalloonFight.Networking
{
    public enum ConfirmedEventConsumeStatus : byte
    {
        Success = 0,
        InvalidWindow = 1,
        OutputTooSmall = 2
    }

    public sealed class ConfirmedEventCursor
    {
        private bool _hasSeen;
        private uint _lastSeenEventId;
        private uint _missedConfirmedEvents;
        private uint _duplicateEventCount;

        public bool HasSeen => _hasSeen;
        public uint LastSeenEventId => _lastSeenEventId;
        public uint MissedConfirmedEvents => _missedConfirmedEvents;
        public uint DuplicateEventCount => _duplicateEventCount;

        public void Reset()
        {
            _hasSeen = false;
            _lastSeenEventId = 0u;
            _missedConfirmedEvents = 0u;
            _duplicateEventCount = 0u;
        }

        public ConfirmedEventConsumeStatus Consume(
            in ConfirmedEventWindow window,
            ClientConfirmedEventBuffer output)
        {
            if (output == null
                || window.Count > ConfirmedEventWindow.Capacity
                || (!window.HasBaseline && window.BaselineEventId != 0u)
                || (window.HistoryGap && window.Count == 0))
            {
                return ConfirmedEventConsumeStatus.InvalidWindow;
            }

            output.Clear();
            if (_hasSeen
                && window.HasBaseline
                && SequenceMath.IsNewer(
                    window.BaselineEventId,
                    _lastSeenEventId))
            {
                return ConfirmedEventConsumeStatus.InvalidWindow;
            }

            bool baselineHasSeen = _hasSeen || window.HasBaseline;
            uint baselineLastSeen = _hasSeen
                ? _lastSeenEventId
                : (window.HasBaseline ? window.BaselineEventId : 0u);
            if (window.Count == 0)
            {
                _hasSeen = baselineHasSeen;
                _lastSeenEventId = baselineLastSeen;
                return ConfirmedEventConsumeStatus.Success;
            }

            if (!window.TryGet(0, out ConfirmedEvent first))
            {
                return ConfirmedEventConsumeStatus.InvalidWindow;
            }

            uint expected = baselineHasSeen
                ? unchecked(baselineLastSeen + 1u)
                : 0u;
            bool newGap = window.HistoryGap
                && SequenceMath.IsNewer(first.EventId, expected);
            if (newGap)
            {
                expected = first.EventId;
            }

            int newEventCount = 0;
            uint newestAccepted = baselineLastSeen;
            bool willHaveSeen = baselineHasSeen;
            uint duplicateCount = 0u;
            for (int index = 0; index < window.Count; index++)
            {
                if (!window.TryGet(index, out ConfirmedEvent value))
                {
                    return ConfirmedEventConsumeStatus.InvalidWindow;
                }

                if (baselineHasSeen && SequenceMath.IsAtOrBefore(
                    value.EventId,
                    baselineLastSeen))
                {
                    duplicateCount++;
                    continue;
                }

                if (value.EventId != expected)
                {
                    return ConfirmedEventConsumeStatus.InvalidWindow;
                }

                expected = unchecked(expected + 1u);
                newestAccepted = value.EventId;
                willHaveSeen = true;
                newEventCount++;
            }

            if (newEventCount > ClientConfirmedEventBuffer.Capacity)
            {
                return ConfirmedEventConsumeStatus.OutputTooSmall;
            }

            for (int index = 0; index < window.Count; index++)
            {
                window.TryGet(index, out ConfirmedEvent value);
                if (baselineHasSeen && SequenceMath.IsAtOrBefore(
                    value.EventId,
                    baselineLastSeen))
                {
                    continue;
                }

                output.TryAppend(in value);
            }

            if (newGap)
            {
                _missedConfirmedEvents = IncrementSaturated(
                    _missedConfirmedEvents);
            }

            _duplicateEventCount = AddSaturated(
                _duplicateEventCount,
                duplicateCount);
            _hasSeen = willHaveSeen;
            _lastSeenEventId = newestAccepted;
            return ConfirmedEventConsumeStatus.Success;
        }

        private static uint IncrementSaturated(uint value)
        {
            return value == uint.MaxValue ? value : value + 1u;
        }

        private static uint AddSaturated(uint value, uint addition)
        {
            uint remaining = uint.MaxValue - value;
            return addition > remaining ? uint.MaxValue : value + addition;
        }
    }
}