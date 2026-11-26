using NetworkCourse.BalloonFight.Simulation;
using UnityEngine;

namespace NetworkCourse.BalloonFight.Engine
{
    public enum FighterViewRole : byte
    {
        Unassigned = 0,
        Owner = 1,
        Remote = 2
    }

    public sealed class BalloonFighterView : MonoBehaviour
    {
        private const float CorrectionDecayRate = 14f;
        private const float MaximumCorrectionDistance = 1.5f;
        private const float CorrectionStopDistance = 0.001f;

        [SerializeField, Range(0, 1)] private int _slotIndex;
        [SerializeField] private GameObject _visual;
        [SerializeField] private GameObject _officialGhost;
        [SerializeField] private bool _showOfficialGhost;
        [SerializeField] private FighterViewRole _role;
        [SerializeField] private float _correctionOffsetMagnitude;
        [SerializeField] private uint _smoothedCorrectionCount;
        [SerializeField] private uint _hardSnapCount;

        private VisualCorrectionState _visualCorrection;
        private bool _hasRendered;
        private bool _lastActive;
        private bool _lastGhostActive;

        public int SlotIndex => _slotIndex;
        public FighterViewRole Role => _role;
        public Vector3 PresentedPosition => transform.position;
        public Vector3 OfficialGhostPosition =>
            _officialGhost.transform.position;
        public float CorrectionOffsetMagnitude =>
            _correctionOffsetMagnitude;
        public uint SmoothedCorrectionCount =>
            _smoothedCorrectionCount;
        public uint HardSnapCount => _hardSnapCount;
        public bool HasForbiddenCollider =>
            GetComponent<Collider2D>() != null
            || _visual.GetComponentInChildren<Collider2D>(true) != null
            || _officialGhost.GetComponentInChildren<Collider2D>(true) != null;
        public bool IsConfigured => _visual != null
            && _officialGhost != null
            && (_slotIndex == 0 || _slotIndex == 1);

        public void SetLocalSlot(int localSlotIndex)
        {
            FighterViewRole nextRole = localSlotIndex < 0
                ? FighterViewRole.Unassigned
                : (_slotIndex == localSlotIndex
                    ? FighterViewRole.Owner
                    : FighterViewRole.Remote);
            if (nextRole != _role)
            {
                _visualCorrection.Reset();
                RefreshCorrectionDiagnostics();
            }

            _role = nextRole;
        }

        public bool BeginOwnerPredictionCorrection(
            in FighterState previousPredicted,
            in FighterState newPredicted)
        {
            if (_role != FighterViewRole.Owner) return true;

            Vector3 previous = ToVector3(in previousPredicted.Position);
            Vector3 next = ToVector3(in newPredicted.Position);
            VisualCorrectionResult result = _visualCorrection.Begin(
                in previous,
                in next,
                MaximumCorrectionDistance);
            RefreshCorrectionDiagnostics();
            return result != VisualCorrectionResult.InvalidConfiguration;
        }

        public bool AdvancePredictionCorrection(float unscaledDeltaTime)
        {
            if (_role != FighterViewRole.Owner) return true;

            bool succeeded = _visualCorrection.Advance(
                unscaledDeltaTime,
                CorrectionDecayRate,
                CorrectionStopDistance);
            RefreshCorrectionDiagnostics();
            return succeeded;
        }

        public void CancelPredictionCorrection()
        {
            _visualCorrection.Cancel();
            RefreshCorrectionDiagnostics();
        }

        public bool RenderStep(in RenderSnapshot snapshot)
        {
            if (!isActiveAndEnabled) return true;

            if (!snapshot.TryGetFighter(
                _slotIndex,
                out bool isActive,
                out Vector3 position,
                out Vector3 officialPosition,
                out bool showOfficialGhost))
            {
                return false;
            }

            if (!_hasRendered || isActive != _lastActive)
            {
                _visual.SetActive(isActive);
                _lastActive = isActive;
            }

            bool ghostActive = isActive
                && showOfficialGhost
                && _showOfficialGhost;
            if (!_hasRendered || ghostActive != _lastGhostActive)
            {
                _officialGhost.SetActive(ghostActive);
                _lastGhostActive = ghostActive;
            }

            _hasRendered = true;
            if (!isActive) return true;

            transform.position = _role == FighterViewRole.Owner
                ? position + _visualCorrection.Offset
                : position;
            if (showOfficialGhost)
            {
                _officialGhost.transform.position = officialPosition;
            }

            return true;
        }

        private void RefreshCorrectionDiagnostics()
        {
            _correctionOffsetMagnitude = _visualCorrection.Magnitude;
            _smoothedCorrectionCount =
                _visualCorrection.SmoothedCorrectionCount;
            _hardSnapCount = _visualCorrection.HardSnapCount;
        }

        private static Vector3 ToVector3(in SimVector2 position)
        {
            return new Vector3(position.X, position.Y, 0f);
        }
    }
}