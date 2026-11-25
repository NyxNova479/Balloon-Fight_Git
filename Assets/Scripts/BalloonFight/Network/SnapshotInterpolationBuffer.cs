using NetworkCourse.BalloonFight.Simulation;

namespace NetworkCourse.BalloonFight.Networking
{
    public enum SnapshotInsertStatus : byte
    {
        Accepted = 0,
        AcceptedWithOverflow = 1,
        Duplicate = 2,
        TooOld = 3
    }

    public enum InterpolationSampleStatus : byte
    {
        Empty = 0,
        Interpolated = 1,
        Holding = 2,
        Starved = 3
    }

    public readonly struct InterpolationSample
    {
        public readonly long LowerTick;
        public readonly long UpperTick;
        public readonly float Alpha;
        public readonly byte ActiveMask;
        public readonly SimVector2 Fighter0Position;
        public readonly SimVector2 Fighter1Position;

        public InterpolationSample(
            long lowerTick,
            long upperTick,
            float alpha,
            byte activeMask,
            in SimVector2 fighter0Position,
            in SimVector2 fighter1Position)
        {
            LowerTick = lowerTick;
            UpperTick = upperTick;
            Alpha = alpha;
            ActiveMask = activeMask;
            Fighter0Position = fighter0Position;
            Fighter1Position = fighter1Position;
        }
    }

    public sealed class SnapshotInterpolationBuffer
    {
        public const int Capacity = 32;

        private readonly NetworkSnapshot[] _snapshots =
            new NetworkSnapshot[Capacity];
        private readonly long[] _unwrappedTicks = new long[Capacity];

        private int _count;
        private bool _hasUnwrapReference;
        private uint _latestNetworkTick;
        private long _latestUnwrappedTick;
        private bool _hasOldestUsefulTick;
        private long _oldestUsefulTick;
        private uint _overflowCount;
        private uint _duplicateCount;
        private uint _tooOldCount;

        public int Count => _count;
        public uint OverflowCount => _overflowCount;
        public uint DuplicateCount => _duplicateCount;
        public uint TooOldCount => _tooOldCount;
        public long NewestUnwrappedTick =>
            _count == 0 ? 0L : _unwrappedTicks[_count - 1];

        public SnapshotInsertStatus Insert(
            in NetworkSnapshot snapshot,
            out long unwrappedTick,
            out bool advancedNewest)
        {
            unwrappedTick = Unwrap(snapshot.ServerTick);
            advancedNewest = !_hasUnwrapReference
                || unwrappedTick > _latestUnwrappedTick;

            if (_hasOldestUsefulTick
                && unwrappedTick < _oldestUsefulTick)
            {
                _tooOldCount = IncrementSaturated(_tooOldCount);
                return SnapshotInsertStatus.TooOld;
            }

            int insertionIndex = FindInsertionIndex(unwrappedTick);
            if (insertionIndex < _count
                && _unwrappedTicks[insertionIndex] == unwrappedTick)
            {
                _duplicateCount = IncrementSaturated(_duplicateCount);
                return SnapshotInsertStatus.Duplicate;
            }

            SnapshotInsertStatus status = SnapshotInsertStatus.Accepted;
            if (_count == Capacity)
            {
                if (insertionIndex == 0)
                {
                    _oldestUsefulTick = _unwrappedTicks[0];
                    _hasOldestUsefulTick = true;
                    _tooOldCount = IncrementSaturated(_tooOldCount);
                    return SnapshotInsertStatus.TooOld;
                }

                RemoveOldest();
                insertionIndex--;
                _overflowCount = IncrementSaturated(_overflowCount);
                status = SnapshotInsertStatus.AcceptedWithOverflow;
            }

            for (int index = _count; index > insertionIndex; index--)
            {
                _snapshots[index] = _snapshots[index - 1];
                _unwrappedTicks[index] = _unwrappedTicks[index - 1];
            }

            _snapshots[insertionIndex] = snapshot;
            _unwrappedTicks[insertionIndex] = unwrappedTick;
            _count++;

            if (advancedNewest)
            {
                _latestNetworkTick = snapshot.ServerTick;
                _latestUnwrappedTick = unwrappedTick;
                _hasUnwrapReference = true;
            }

            return status;
        }

        public InterpolationSampleStatus Sample(
            double renderTick,
            out InterpolationSample sample)
        {
            if (_count == 0)
            {
                sample = default;
                return InterpolationSampleStatus.Empty;
            }

            if (_count == 1)
            {
                sample = CreateHeldSample(0);
                return renderTick > _unwrappedTicks[0]
                    ? InterpolationSampleStatus.Starved
                    : InterpolationSampleStatus.Holding;
            }

            if (renderTick <= _unwrappedTicks[0])
            {
                sample = CreateHeldSample(0);
                return InterpolationSampleStatus.Holding;
            }

            int newestIndex = _count - 1;
            if (renderTick > _unwrappedTicks[newestIndex])
            {
                MarkOldestUseful(newestIndex);
                sample = CreateHeldSample(0);
                return InterpolationSampleStatus.Starved;
            }

            int upperIndex = 1;
            while (renderTick > _unwrappedTicks[upperIndex])
            {
                upperIndex++;
            }

            int lowerIndex = upperIndex - 1;
            long lowerTick = _unwrappedTicks[lowerIndex];
            long upperTick = _unwrappedTicks[upperIndex];
            float alpha = (float)((renderTick - lowerTick)
                / (upperTick - lowerTick));
            NetworkSnapshot lower = _snapshots[lowerIndex];
            NetworkSnapshot upper = _snapshots[upperIndex];
            byte activeMask = alpha < 1f
                ? lower.SimulationActiveMask
                : upper.SimulationActiveMask;
            SimVector2 fighter0 = Lerp(
                in lower.Fighter0,
                in upper.Fighter0,
                alpha);
            SimVector2 fighter1 = Lerp(
                in lower.Fighter1,
                in upper.Fighter1,
                alpha);

            sample = new InterpolationSample(
                lowerTick,
                upperTick,
                alpha,
                activeMask,
                in fighter0,
                in fighter1);
            MarkOldestUseful(lowerIndex);
            return InterpolationSampleStatus.Interpolated;
        }

        public bool TryGetNewest(
            out NetworkSnapshot snapshot,
            out long unwrappedTick)
        {
            if (_count == 0)
            {
                snapshot = default;
                unwrappedTick = 0L;
                return false;
            }

            int index = _count - 1;
            snapshot = _snapshots[index];
            unwrappedTick = _unwrappedTicks[index];
            return true;
        }

        public bool TryGetUnwrappedTickAt(int index, out long tick)
        {
            if (index < 0 || index >= _count)
            {
                tick = 0L;
                return false;
            }

            tick = _unwrappedTicks[index];
            return true;
        }

        public void ClearSamples()
        {
            _count = 0;
            _hasOldestUsefulTick = false;
            _oldestUsefulTick = 0L;
        }

        public void Reset()
        {
            ClearSamples();
            _hasUnwrapReference = false;
            _latestNetworkTick = 0u;
            _latestUnwrappedTick = 0L;
            _overflowCount = 0u;
            _duplicateCount = 0u;
            _tooOldCount = 0u;
        }

        private long Unwrap(uint networkTick)
        {
            if (!_hasUnwrapReference) return networkTick;

            int signedDistance = unchecked(
                (int)(networkTick - _latestNetworkTick));
            return _latestUnwrappedTick + signedDistance;
        }

        private int FindInsertionIndex(long unwrappedTick)
        {
            int index = 0;
            while (index < _count
                && _unwrappedTicks[index] < unwrappedTick)
            {
                index++;
            }

            return index;
        }

        private void RemoveOldest()
        {
            for (int index = 1; index < _count; index++)
            {
                _snapshots[index - 1] = _snapshots[index];
                _unwrappedTicks[index - 1] = _unwrappedTicks[index];
            }

            _count--;
        }

        private void MarkOldestUseful(int usefulIndex)
        {
            long usefulTick = _unwrappedTicks[usefulIndex];
            if (usefulIndex > 0)
            {
                int remaining = _count - usefulIndex;
                for (int index = 0; index < remaining; index++)
                {
                    _snapshots[index] = _snapshots[index + usefulIndex];
                    _unwrappedTicks[index] =
                        _unwrappedTicks[index + usefulIndex];
                }

                _count = remaining;
            }

            _oldestUsefulTick = usefulTick;
            _hasOldestUsefulTick = true;
        }

        private InterpolationSample CreateHeldSample(int index)
        {
            NetworkSnapshot snapshot = _snapshots[index];
            SimVector2 fighter0 = new SimVector2(
                snapshot.Fighter0.PositionX,
                snapshot.Fighter0.PositionY);
            SimVector2 fighter1 = new SimVector2(
                snapshot.Fighter1.PositionX,
                snapshot.Fighter1.PositionY);
            long tick = _unwrappedTicks[index];
            return new InterpolationSample(
                tick,
                tick,
                0f,
                snapshot.SimulationActiveMask,
                in fighter0,
                in fighter1);
        }

        private static SimVector2 Lerp(
            in NetworkFighterState from,
            in NetworkFighterState to,
            float alpha)
        {
            return new SimVector2(
                from.PositionX + ((to.PositionX - from.PositionX) * alpha),
                from.PositionY + ((to.PositionY - from.PositionY) * alpha));
        }

        private static uint IncrementSaturated(uint value)
        {
            return value == uint.MaxValue ? value : value + 1u;
        }
    }
}