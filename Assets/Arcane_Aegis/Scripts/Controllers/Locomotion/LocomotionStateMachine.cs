using UnityEngine;
using KinematicCharacterController;
using Arcane_Aegis.Controllers.Inputs;
using Arcane_Aegis.Controllers.Locomotion.States;

namespace Arcane_Aegis.Controllers.Locomotion
{
    /// <summary>
    /// Drives the player's locomotion FSM. Holds the current state, runs transitions in Update, and
    /// owns the shared context the states need (motor, input, tuning). Does NOT touch networking.
    /// </summary>
    [RequireComponent(typeof(KinematicCharacterMotor))]
    public class LocomotionStateMachine : MonoBehaviour
    {
        [Header("Refs")]
        public KinematicCharacterMotor Motor;
        public PlayerInput Input;

        [Header("Tuning")]
        public float RunSpeed = 3.5f;   // default movement (a jog/run)
        public float DashSpeed = 7f;    // while Shift (Dash) is held
        public float JumpSpeed = 7f;
        public float Gravity = 25f;
        public float RotationSharpness = 12f;
        public float AirControl = 5f;

        // ── crowd-control (set by the network's S2C_ControlState; §12.2) ──
        /// <summary>Can't move or act (stun). Set from the server's CC replication.</summary>
        public bool Stunned { get; private set; }
        /// <summary>Can't move (root) — can still act.</summary>
        public bool Rooted { get; private set; }
        /// <summary>Move-speed multiplier (1 = normal, &lt;1 = slowed).</summary>
        public float SpeedMult { get; private set; } = 1f;
        /// <summary>True while a stun/root forbids horizontal movement.</summary>
        public bool MoveBlocked => Stunned || Rooted;

        /// <summary>Applies the server's authoritative CC state to the local input (no rubber-band).</summary>
        public void SetControl(bool stunned, bool rooted, float speedMult)
        {
            Stunned = stunned; Rooted = rooted;
            SpeedMult = speedMult > 0f ? speedMult : 0f;
        }

        /// <summary>True while Dash (Shift) is held.</summary>
        public bool Dashing => Input != null && Input.DashHeld;
        /// <summary>Ground move speed: jog by default, dash while Shift is held (scaled by any slow).</summary>
        public float CurrentSpeed => (Dashing ? DashSpeed : RunSpeed) * SpeedMult;

        public ILocomotionState Current { get; private set; }
        public IdleState Idle { get; private set; }
        public LocomotionState Loco { get; private set; }
        public AirborneState Air { get; private set; }

        private bool _jumpQueued;

        private void Awake()
        {
            if (Motor == null) Motor = GetComponent<KinematicCharacterMotor>();
            if (Input == null) Input = GetComponent<PlayerInput>();

            Idle = new IdleState(this);
            Loco = new LocomotionState(this);
            Air = new AirborneState(this);

            Current = Idle;
            Current.Enter();
        }

        private void Update() => Current.Tick(Time.deltaTime);

        public void ChangeState(ILocomotionState next)
        {
            if (next == null || next == Current) return;
            Current.Exit();
            Current = next;
            Current.Enter();
        }

        public bool IsGrounded => Motor.GroundingStatus.IsStableOnGround;

        /// <summary>Queues a jump impulse and ungrounds the motor (call on a grounded jump press).</summary>
        public void RequestJump()
        {
            if (MoveBlocked) return; // can't jump while stunned/rooted
            _jumpQueued = true;
            Motor.ForceUnground();
        }

        /// <summary>Consumes the queued jump (applied once by the airborne state).</summary>
        public bool TakeJump()
        {
            if (!_jumpQueued) return false;
            _jumpQueued = false;
            return true;
        }

        private Transform _cam;

        /// <summary>Move direction from input, RELATIVE to the camera's facing (XZ plane, magnitude 0..1).</summary>
        public Vector3 MoveDirection()
        {
            if (MoveBlocked) return Vector3.zero; // stunned/rooted → no horizontal move (server would reject it anyway)
            Vector2 m = Input != null ? Input.Move : Vector2.zero;
            if (m.sqrMagnitude < 1e-6f) return Vector3.zero;

            if (_cam == null && Camera.main != null) _cam = Camera.main.transform;

            Vector3 dir;
            if (_cam != null)
            {
                Vector3 fwd = _cam.forward; fwd.y = 0f;
                Vector3 right = _cam.right; right.y = 0f;
                dir = fwd.normalized * m.y + right.normalized * m.x;   // W = away from camera
            }
            else
            {
                dir = new Vector3(m.x, 0f, m.y);                        // fallback: world-relative
            }
            return dir.sqrMagnitude > 1f ? dir.normalized : dir;
        }
    }
}
