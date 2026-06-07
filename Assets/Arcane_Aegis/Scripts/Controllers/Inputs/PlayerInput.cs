using UnityEngine;
using UnityEngine.InputSystem;

namespace Arcane_Aegis.Controllers.Inputs
{
    /// <summary>
    /// Single input source for the local player, backed by the generated MMO_Inputs actions.
    /// The locomotion FSM reads <see cref="Move"/> / <see cref="ConsumeJump"/>; the camera reads
    /// <see cref="Look"/> / <see cref="Zoom"/> / <see cref="RightClick"/>. (Attack/Dash/Interact wired later.)
    /// </summary>
    public class PlayerInput : MonoBehaviour
    {
        [Header("Camera sensitivity")]
        [SerializeField] private float lookSensitivity = 0.05f; // mouse delta is in pixels → scale down

        private MMO_Inputs _actions;
        private bool _jumpLatched;

        /// <summary>Movement axis (x = strafe, y = forward). Keyboard WASD or gamepad left stick.</summary>
        public Vector2 Move { get; private set; }
        /// <summary>Camera look delta (mouse/right-stick), already scaled by sensitivity.</summary>
        public Vector2 Look { get; private set; }
        /// <summary>Scroll wheel this frame (~±1 per notch).</summary>
        public float Zoom { get; private set; }
        /// <summary>True while the right mouse button is held (camera orbit gate).</summary>
        public bool RightClick { get; private set; }
        /// <summary>True while Dash (Left Shift) is held — faster movement. Default movement is a jog/run.</summary>
        public bool DashHeld { get; private set; }

        private void Awake() => _actions = new MMO_Inputs();

        private void OnEnable()
        {
            _actions.Player.Enable();
            _actions.UI.Enable();   // RightClick + ScrollWheel live in the UI map
        }

        private void OnDisable()
        {
            _actions.Player.Disable();
            _actions.UI.Disable();
        }

        private void OnDestroy() => _actions?.Dispose();

        private void Update()
        {
            Move = _actions.Player.Move.ReadValue<Vector2>();
            Look = _actions.Player.Look.ReadValue<Vector2>() * lookSensitivity;
            Zoom = _actions.UI.ScrollWheel.ReadValue<Vector2>().y / 120f; // 120 = one wheel notch
            RightClick = _actions.UI.RightClick.ReadValue<float>() > 0.5f;
            DashHeld = _actions.Player.Dash.IsPressed();

            if (_actions.Player.Jump.WasPressedThisFrame()) _jumpLatched = true;
        }

        /// <summary>Returns true once per jump press (latched so frame-ordering doesn't drop it).</summary>
        public bool ConsumeJump()
        {
            if (!_jumpLatched) return false;
            _jumpLatched = false;
            return true;
        }
    }
}
