using UnityEngine;

namespace NetworkCourse.BalloonFight.Engine
{

    public enum FighterViewRole : byte
    {

        Unassigned = 0,
        Owner = 1,
        Remote = 2,

    }


    public sealed class BalloonFighterView : MonoBehaviour
    {
        [SerializeField, Range(0,1)] private int _slotIndex;
        [SerializeField] private GameObject _visual;
        [SerializeField] private GameObject _officialGhost;
        [SerializeField] private bool _showOfficialGhost;
        [SerializeField] private FighterViewRole _role;

        private bool _hasRendered;
        private bool _lastActive;
        private bool _lastGhostActive;

        public int SlotIndex => _slotIndex;
        public FighterViewRole Role => _role;
        public Vector3 PresentedPosition => transform.position;
        public Vector3 OfficialGhostPosition => _officialGhost.transform.position;

        public bool HasForbiddenCollider =>
            GetComponent<Collider2D>() != null
            || _visual.GetComponentInChildren<Collider2D>(true) != null
            || _officialGhost.GetComponentInChildren<Collider2D>(true) != null;

        public bool IsConfigured => _visual != null
            && _officialGhost != null
            && (_slotIndex == 0 || _slotIndex == 1);

        public void SetLocalSlot(int localSlotIndex)
        {
            _role = localSlotIndex < 0
                ? FighterViewRole.Unassigned
                : (_slotIndex == localSlotIndex
                    ? FighterViewRole.Owner
                    : FighterViewRole.Remote);
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

            transform.position = position;
            if (showOfficialGhost)
            {
                _officialGhost.transform.position = officialPosition;
            }

            return true;
        }
    }
}
