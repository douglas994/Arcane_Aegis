using UnityEngine;
using ArcaneShared.Enums;
using Arcane_Aegis.Controllers.Locomotion;

namespace Arcane_Aegis.Controllers
{
    /// <summary>
    /// Animation layer: feeds the Animator from the locomotion STATE + a horizontal speed.
    /// - Local player: assign <see cref="fsm"/> → state &amp; speed come from the FSM (accurate, no lag).
    /// - Remote player: its EntityView pushes <see cref="State"/> + <see cref="SourceSpeed"/> from snapshots
    ///   (real networked speed, NOT the lagging transform — so run/dash blend correctly).
    ///
    /// Animator Controller needs a float "<see cref="speedParam"/>" (idle↔run↔dash blend, 0..1) and a bool
    /// "<see cref="groundedParam"/>". Missing params are ignored, so it won't error before you wire them.
    /// </summary>
    public class CharacterAnimator : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private Animator animator;
        [SerializeField] private LocomotionStateMachine fsm; // optional: assign on the LOCAL player

        [Header("Animator parameters")]
        [SerializeField] private string speedParam = "Speed";
        [SerializeField] private string groundedParam = "Grounded";
        [SerializeField] private float speedDamp = 0.1f;
        [SerializeField] private float maxSpeed = 7f; // = DashSpeed; normalizes Speed to 0..1 (idle 0, run ~0.5, dash 1)

        /// <summary>Current locomotion state. Auto-set from the FSM if present; else set by EntityView (remote).</summary>
        public MovementState State { get; set; } = MovementState.Idle;

        /// <summary>Horizontal speed (m/s) for REMOTES, set by EntityView from snapshot positions. Ignored if an FSM is assigned.</summary>
        public float SourceSpeed { get; set; }

        private int _speedHash, _groundedHash;
        private bool _hasSpeed, _hasGrounded;

        private void Start()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();

            if (animator != null)
            {
                _speedHash = Animator.StringToHash(speedParam);
                _groundedHash = Animator.StringToHash(groundedParam);
                _hasSpeed = HasParam(speedParam);
                _hasGrounded = HasParam(groundedParam);
            }
        }

        private void Update()
        {
            if (animator == null) return;

            // Pick the speed source: local FSM (accurate) vs remote networked speed.
            float rawSpeed;
            if (fsm != null)
            {
                State = fsm.Current.NetState;
                rawSpeed = State == MovementState.Locomotion ? fsm.CurrentSpeed : 0f;
            }
            else
            {
                rawSpeed = State == MovementState.Locomotion ? SourceSpeed : 0f;
            }

            float normalized = maxSpeed > 0.01f ? Mathf.Clamp01(rawSpeed / maxSpeed) : rawSpeed;

            if (_hasSpeed) animator.SetFloat(_speedHash, normalized, speedDamp, Time.deltaTime);
            if (_hasGrounded) animator.SetBool(_groundedHash, State != MovementState.Airborne && State != MovementState.Dead);
        }

        /// <summary>Use the local FSM as the speed/state source (local player).</summary>
        public void UseFsm(LocomotionStateMachine source) => fsm = source;

        /// <summary>Use the networked source (State + SourceSpeed pushed by EntityView) — remote players.</summary>
        public void UseNetworkSource() => fsm = null;

        private bool HasParam(string name)
        {
            foreach (var p in animator.parameters)
                if (p.name == name) return true;
            return false;
        }
    }
}
