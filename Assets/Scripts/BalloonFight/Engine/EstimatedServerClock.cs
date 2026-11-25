using System;

namespace NetworkCourse.BalloonFight.Engine
{
    public enum ServerClockObservation : byte
    {
        Slewing = 0,
        ReanchorRequired = 1
    }

    public sealed class EstimatedServerClock
    {
        private const double CorrectionGain = 0.02d;

        private readonly double _tickRate;
        private readonly double _maximumPhaseErrorTicks;
        private readonly double _minimumSpeedScale;
        private readonly double _maximumSpeedScale;

        private bool _isAnchored;
        private double _estimatedTick;
        private double _lastLocalTimeSeconds;
        private double _speedScale = 1d;

        public EstimatedServerClock(
            double tickRate,
            double maximumPhaseErrorTicks,
            double maximumSpeedCorrection)
        {
            if (tickRate <= 0d) throw new ArgumentOutOfRangeException(
                nameof(tickRate));
            if (maximumPhaseErrorTicks <= 0d)
                throw new ArgumentOutOfRangeException(
                    nameof(maximumPhaseErrorTicks));
            if (maximumSpeedCorrection < 0d
                || maximumSpeedCorrection >= 1d)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maximumSpeedCorrection));
            }

            _tickRate = tickRate;
            _maximumPhaseErrorTicks = maximumPhaseErrorTicks;
            _minimumSpeedScale = 1d - maximumSpeedCorrection;
            _maximumSpeedScale = 1d + maximumSpeedCorrection;
        }

        public bool IsAnchored => _isAnchored;
        public double EstimatedTick => _estimatedTick;
        public double SpeedScale => _speedScale;

        public void Anchor(long serverTick, double localTimeSeconds)
        {
            _estimatedTick = serverTick;
            _lastLocalTimeSeconds = localTimeSeconds;
            _speedScale = 1d;
            _isAnchored = true;
        }

        public double Advance(double localTimeSeconds)
        {
            if (!_isAnchored) return 0d;

            double elapsed = localTimeSeconds - _lastLocalTimeSeconds;
            if (elapsed <= 0d) return _estimatedTick;

            _estimatedTick += elapsed * _tickRate * _speedScale;
            _lastLocalTimeSeconds = localTimeSeconds;
            return _estimatedTick;
        }

        public ServerClockObservation Observe(
            long serverTick,
            double localTimeSeconds,
            out double phaseErrorTicks)
        {
            Advance(localTimeSeconds);
            phaseErrorTicks = serverTick - _estimatedTick;
            if (Math.Abs(phaseErrorTicks) > _maximumPhaseErrorTicks)
            {
                return ServerClockObservation.ReanchorRequired;
            }

            _speedScale = Clamp(
                1d + (phaseErrorTicks * CorrectionGain),
                _minimumSpeedScale,
                _maximumSpeedScale);
            return ServerClockObservation.Slewing;
        }

        public void Reset()
        {
            _isAnchored = false;
            _estimatedTick = 0d;
            _lastLocalTimeSeconds = 0d;
            _speedScale = 1d;
        }

        private static double Clamp(
            double value,
            double minimum,
            double maximum)
        {
            if (value < minimum) return minimum;
            return value > maximum ? maximum : value;
        }
    }
}