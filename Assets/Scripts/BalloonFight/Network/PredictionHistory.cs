using NetworkCourse.BalloonFight.Common;
using NetworkCourse.BalloonFight.Networking;

namespace NetworkCourse.BalloonFight.Simulation
{
    public enum PredictionHistoryStatus : byte
    {
        Success = 0,
        WrongInputEpoch = 1,
        StaleOrDuplicate = 2,
        Gap = 3,
        HistoryFull = 4,
        AckAhead = 5,
        NothingToPurge = 6,
        NotFound = 7
    }

    public sealed class PredictionHistory
    {
        public const int Capacity = 256;

        private readonly BoundedRing<PredictionSnapshot> _snapshots =
            new BoundedRing<PredictionSnapshot>(Capacity);

        private uint _inputEpoch;
        private uint _nextInputTick;
        private bool _isReady;
        private bool _hasProduced;
        private uint _lastProducedTick;
        private bool _hasAck;
        private uint _lastAckTick;

        public int Count => _snapshots.Count;
        public uint InputEpoch => _inputEpoch;
        public uint NextInputTick => _nextInputTick;
        public bool IsReady => _isReady;
        public bool HasAck => _hasAck;
        public uint LastAckTick => _lastAckTick;

        public void BeginEpoch(uint inputEpoch)
        {
            _snapshots.Clear();
            _inputEpoch = inputEpoch;
            _nextInputTick = 0u;
            _isReady = true;
            _hasProduced = false;
            _lastProducedTick = 0u;
            _hasAck = false;
            _lastAckTick = 0u;
        }

        public void Reset()
        {
            _snapshots.Clear();
            _inputEpoch = 0u;
            _nextInputTick = 0u;
            _isReady = false;
            _hasProduced = false;
            _lastProducedTick = 0u;
            _hasAck = false;
            _lastAckTick = 0u;
        }

        public PredictionHistoryStatus TryAppend(
            in PredictionSnapshot snapshot)
        {
            if (!_isReady || snapshot.InputEpoch != _inputEpoch)
            {
                return PredictionHistoryStatus.WrongInputEpoch;
            }

            if (snapshot.InputTick != _nextInputTick)
            {
                return SequenceMath.IsAtOrBefore(
                    snapshot.InputTick,
                    _nextInputTick)
                    ? PredictionHistoryStatus.StaleOrDuplicate
                    : PredictionHistoryStatus.Gap;
            }

            if (!_snapshots.TryEnqueue(in snapshot))
            {
                return PredictionHistoryStatus.HistoryFull;
            }

            _lastProducedTick = snapshot.InputTick;
            _hasProduced = true;
            _nextInputTick = unchecked(_nextInputTick + 1u);
            return PredictionHistoryStatus.Success;
        }

        public PredictionHistoryStatus TryGet(
            uint inputEpoch,
            uint inputTick,
            out PredictionSnapshot snapshot)
        {
            if (!_isReady || inputEpoch != _inputEpoch)
            {
                snapshot = default;
                return PredictionHistoryStatus.WrongInputEpoch;
            }

            for (int index = 0; index < _snapshots.Count; index++)
            {
                _snapshots.TryGetAt(index, out PredictionSnapshot candidate);
                if (candidate.InputTick == inputTick)
                {
                    snapshot = candidate;
                    return PredictionHistoryStatus.Success;
                }
            }

            snapshot = default;
            return PredictionHistoryStatus.NotFound;
        }

        public PredictionHistoryStatus PurgeThrough(
            uint inputEpoch,
            uint acknowledgedInputTick)
        {
            if (!_isReady || inputEpoch != _inputEpoch)
            {
                return PredictionHistoryStatus.WrongInputEpoch;
            }

            if (!_hasProduced
                || SequenceMath.IsNewer(
                    acknowledgedInputTick,
                    _lastProducedTick))
            {
                return PredictionHistoryStatus.AckAhead;
            }

            if (_hasAck
                && !SequenceMath.IsNewer(
                    acknowledgedInputTick,
                    _lastAckTick))
            {
                return PredictionHistoryStatus.NothingToPurge;
            }

            while (_snapshots.TryPeek(out PredictionSnapshot oldest)
                && SequenceMath.IsAtOrBefore(
                    oldest.InputTick,
                    acknowledgedInputTick))
            {
                _snapshots.TryDequeue(out _);
            }

            _hasAck = true;
            _lastAckTick = acknowledgedInputTick;
            return PredictionHistoryStatus.Success;
        }

        public bool TryGetNewest(out PredictionSnapshot snapshot)
        {
            return _snapshots.TryGetAt(_snapshots.Count - 1, out snapshot);
        }
    }
}