using UnityEngine;

namespace NetworkCourse.BalloonFight.Engine
{
    public enum VisualCorrectionResult : byte
    {
        Smoothed = 0,
        HardSnap = 1,
        InvalidConfiguration = 2
    }

    public struct VisualCorrectionState
    {
        private Vector3 _offset;
        private uint _smoothedCorrectionCount;
        private uint _hardSnapCount;

        public Vector3 Offset => _offset;
        public float Magnitude => _offset.magnitude;
        public uint SmoothedCorrectionCount => _smoothedCorrectionCount;
        public uint HardSnapCount => _hardSnapCount;

        public VisualCorrectionResult Begin(
            in Vector3 previousPredictedPosition,
            in Vector3 newPredictedPosition,
            float maximumDistance)
        {
            float maximumDistanceSquared = maximumDistance
                * maximumDistance;
            if (!IsFinite(maximumDistance)
                || !IsFinite(maximumDistanceSquared)
                || maximumDistance < 0f)
            {
                _offset = Vector3.zero;
                return VisualCorrectionResult.InvalidConfiguration;
            }

            Vector3 candidate = _offset
                + previousPredictedPosition
                - newPredictedPosition;
            if (!IsFinite(candidate.x)
                || !IsFinite(candidate.y)
                || !IsFinite(candidate.z)
                || candidate.sqrMagnitude > maximumDistanceSquared)
            {
                _offset = Vector3.zero;
                _hardSnapCount = IncrementSaturated(_hardSnapCount);
                return VisualCorrectionResult.HardSnap;
            }

            _offset = candidate;
            _smoothedCorrectionCount = IncrementSaturated(
                _smoothedCorrectionCount);
            return VisualCorrectionResult.Smoothed;
        }

        public bool Advance(
            float unscaledDeltaTime,
            float decayRate,
            float stopDistance)
        {
            float stopDistanceSquared = stopDistance * stopDistance;
            if (!IsFinite(unscaledDeltaTime)
                || !IsFinite(decayRate)
                || !IsFinite(stopDistance)
                || !IsFinite(stopDistanceSquared)
                || unscaledDeltaTime < 0f
                || decayRate <= 0f
                || stopDistance < 0f)
            {
                _offset = Vector3.zero;
                return false;
            }

            float blend = 1f - Mathf.Exp(
                -decayRate * unscaledDeltaTime);
            _offset = Vector3.Lerp(_offset, Vector3.zero, blend);
            if (_offset.sqrMagnitude <= stopDistanceSquared)
            {
                _offset = Vector3.zero;
            }

            return true;
        }

        public void Cancel()
        {
            _offset = Vector3.zero;
        }

        public void Reset()
        {
            _offset = Vector3.zero;
            _smoothedCorrectionCount = 0u;
            _hardSnapCount = 0u;
        }

        private static uint IncrementSaturated(uint value)
        {
            return value == uint.MaxValue ? value : value + 1u;
        }

        // B3 adaptation : float.IsFinite absent (.NET Standard 2.0 du projet)
        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}