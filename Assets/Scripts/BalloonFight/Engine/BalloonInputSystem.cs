using NetworkCourse.BalloonFight.Simulation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NetworkCourse.BalloonFight.Engine
{
    public enum FighterControlScheme
    {
        AAndDSpace = 0,
        ArrowKeys = 1
    }

    public sealed class BalloonInputCapture : MonoBehaviour
    {
        [SerializeField] private PlayerInput _playerInput;
        [SerializeField] private FighterControlScheme _controlScheme;

        private InputAction _horizontalAction;
        private InputAction _flapAction;
        private float _horizontal;
        private bool _flapHeld;
        private uint _flapPressSequence;
        private uint _lastCreatedFlapPressSequence;

        public float Horizontal => _horizontal;
        public bool FlapHeld => _flapHeld;
        public uint FlapPressSequence => _flapPressSequence;

        private void Start()
        {
            PlayerInput playerInput = _playerInput != null
                ? _playerInput
                : GetComponent<PlayerInput>();
            if (playerInput == null || playerInput.actions == null) return;

            string actionMapName = _controlScheme == FighterControlScheme.AAndDSpace
                ? "BalloonP1"
                : "BalloonP2";
            InputActionMap actionMap = playerInput.actions.FindActionMap(
                actionMapName,
                false);
            if (actionMap == null) return;

            _horizontalAction = actionMap.FindAction("Horizontal", false);
            _flapAction = actionMap.FindAction("Flap", false);
            if (_horizontalAction == null || _flapAction == null) return;

            if (!actionMap.enabled) actionMap.Enable();
        }

        public void FrameUpdate()
        {
            if (_horizontalAction == null || _flapAction == null)
            {
                _horizontal = 0f;
                _flapHeld = false;
                return;
            }

            _horizontal = Mathf.Clamp(
                _horizontalAction.ReadValue<float>(),
                -1f,
                1f);
            _flapHeld = _flapAction.IsPressed();
            if (_flapAction.WasPressedThisFrame())
            {
                _flapPressSequence = unchecked(_flapPressSequence + 1u);
            }
        }

        public void ResetNetworkSequences()
        {
            _flapPressSequence = 0u;
            _lastCreatedFlapPressSequence = 0u;
        }

        public FighterInput CreateInput(uint tick)
        {
            bool flapPressed = _lastCreatedFlapPressSequence
                != _flapPressSequence;
            if (flapPressed)
            {
                _lastCreatedFlapPressSequence = unchecked(
                    _lastCreatedFlapPressSequence + 1u);
            }

            return new FighterInput(tick, _horizontal, flapPressed);
        }
    }
}