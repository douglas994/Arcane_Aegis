using UnityEngine;
using UnityEngine.InputSystem;
using Arcane_Aegis.UI;

namespace Arcane_Aegis.Controllers.Inputs
{
    /// <summary>
    /// Single input source for the local player, backed by the generated MMO_Inputs actions.
    /// The locomotion FSM reads <see cref="Move"/> / <see cref="ConsumeJump"/> / <see cref="DashHeld"/>; the camera
    /// reads <see cref="Look"/> / <see cref="Zoom"/> / <see cref="RightClick"/>; combat reads <see cref="ConsumeAttack"/>.
    /// (Interact keys live on each interaction controller — Gather/Seal/Shop — not here.)
    /// </summary>
    public class PlayerInput : MonoBehaviour
    {
        [Header("Camera sensitivity")]
        [SerializeField] private float lookSensitivity = 0.05f; // mouse delta is in pixels → scale down

        private MMO_Inputs _actions;
        private bool _jumpLatched;
        private bool _attackLatched;

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
        /// <summary>True while Space is held — a flying mount ASCENDS (no effect on foot/ground mounts).</summary>
        public bool AscendHeld { get; private set; }
        /// <summary>True while Left Ctrl is held — a flying mount DESCENDS.</summary>
        public bool DescendHeld { get; private set; }

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
            // Camera (mouse) stays live even while typing — it doesn't conflict with a text field.
            Look = _actions.Player.Look.ReadValue<Vector2>() * lookSensitivity;
            Zoom = _actions.UI.ScrollWheel.ReadValue<Vector2>().y / 120f; // 120 = one wheel notch
            RightClick = _actions.UI.RightClick.ReadValue<float>() > 0.5f;

            // Typing in chat/an input → suppress movement/jump/attack so keystrokes don't drive the character.
            if (UiFocus.IsTyping)
            {
                Move = Vector2.zero;
                DashHeld = false;
                AscendHeld = DescendHeld = false;
                return;
            }

            Move = _actions.Player.Move.ReadValue<Vector2>();
            DashHeld = _actions.Player.Dash.IsPressed();
            // Flying-mount altitude (read straight off the keyboard via the new Input System — no extra actions needed).
            var kb = Keyboard.current;
            AscendHeld = kb != null && kb.spaceKey.isPressed;
            DescendHeld = kb != null && kb.leftCtrlKey.isPressed;

            if (_actions.Player.Jump.WasPressedThisFrame()) _jumpLatched = true;
            if (_actions.Player.Attack.WasPressedThisFrame()) _attackLatched = true;
        }

        /// <summary>Returns true once per jump press (latched so frame-ordering doesn't drop it).</summary>
        public bool ConsumeJump()
        {
            if (!_jumpLatched) return false;
            _jumpLatched = false;
            return true;
        }

        /// <summary>Returns true once per attack press (left mouse / gamepad West).</summary>
        public bool ConsumeAttack()
        {
            if (!_attackLatched) return false;
            _attackLatched = false;
            return true;
        }
    }
}
