using UnityEngine;

namespace NetworkCourse.BalloonFight.Engine
{
    public sealed class BalloonFighterView : MonoBehaviour
    {
        [SerializeField, Range(0,1)] private int _slotIndex;
        [SerializeField] private GameObject _visual;

        private bool _hasRendered;
        private bool _lastActive;

        public int SlotIndex => _slotIndex;
        public bool IsConfigured => _visual != null && (_slotIndex == 0 || _slotIndex == 1);

        public bool RenderStep(in RenderSnapshot snapshot)
        {
            if (!isActiveAndEnabled) return true;

            if(!snapshot.TryGetFighter(
                _slotIndex,
                out bool isActive,
                out Vector3 position))
            {
                return false;
            }

            if(!_hasRendered || isActive != _lastActive)
            {
                _visual.SetActive(isActive);
                _lastActive = isActive;
                _hasRendered = true;
            }

            if (isActive)
            {
                transform.position = position;
            }

            return true;
        }
    }
}
