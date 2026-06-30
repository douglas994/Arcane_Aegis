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
        [SerializeField] private string attackTrigger = "Attack";
        [SerializeField] private string hitTrigger = "Hit"; // brief flinch on taking damage
        [SerializeField] private string deadParam = "Dead";
        [SerializeField] private string gatheringParam = "Gathering"; // bool: true while harvesting
        [SerializeField] private string gatherTypeParam = "GatherType"; // int: which action (= Profession byte: 0 chop, 1 mine, …)
        [SerializeField] private string mountedParam = "Mounted"; // bool: true while riding a mount (sit pose) — author as an Any-State transition
        [SerializeField] private float speedDamp = 0.1f;
        [SerializeField] private float maxSpeed = 7f; // = DashSpeed; normalizes Speed to 0..1 (idle 0, run ~0.5, dash 1)

        /// <summary>Current locomotion state. Auto-set from the FSM if present; else set by EntityView (remote).</summary>
        public MovementState State { get; set; } = MovementState.Idle;

        /// <summary>Horizontal speed (m/s) for REMOTES, set by EntityView from snapshot positions. Ignored if an FSM is assigned.</summary>
        public float SourceSpeed { get; set; }

        private int _speedHash, _groundedHash, _attackHash, _hitHash, _deadHash, _gatheringHash, _gatherTypeHash, _mountedHash;
        private bool _hasSpeed, _hasGrounded, _hasAttack, _hasHit, _hasDead, _hasGathering, _hasGatherType, _hasMounted;

        // Last requested bool states — remembered so a setter called BEFORE Start (e.g. seated on the same frame the
        // view re-spawns when re-entering AoI) still applies once the params are cached.
        private bool _mounted, _dead;

        private void Start()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();

            if (animator != null)
            {
                _speedHash = Animator.StringToHash(speedParam);
                _groundedHash = Animator.StringToHash(groundedParam);
                _hasSpeed = HasParam(speedParam);
                _hasGrounded = HasParam(groundedParam);
                _attackHash = Animator.StringToHash(attackTrigger);
                _hasAttack = HasParam(attackTrigger);
                _hitHash = Animator.StringToHash(hitTrigger);
                _hasHit = HasParam(hitTrigger);
                _deadHash = Animator.StringToHash(deadParam);
                _hasDead = HasParam(deadParam);
                _gatheringHash = Animator.StringToHash(gatheringParam);
                _hasGathering = HasParam(gatheringParam);
                _gatherTypeHash = Animator.StringToHash(gatherTypeParam);
                _hasGatherType = HasParam(gatherTypeParam);
                _mountedHash = Animator.StringToHash(mountedParam);
                _hasMounted = HasParam(mountedParam);

                // Re-apply any bool state requested before the params were cached (spawn-then-seat in one frame).
                if (_hasMounted) animator.SetBool(_mountedHash, _mounted);
                if (_hasDead) animator.SetBool(_deadHash, _dead);
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
            // Grounded stays TRUE while Dead (the corpse is on the ground). If we forced it false here, the Any-State→
            // Airborne transition (Grounded==false) would fire on death and fight the Death state → wrong anim.
            if (_hasGrounded) animator.SetBool(_groundedHash, State != MovementState.Airborne);
        }

        /// <summary>Use the local FSM as the speed/state source (local player).</summary>
        public void UseFsm(LocomotionStateMachine source) => fsm = source;

        /// <summary>Use the networked source (State + SourceSpeed pushed by EntityView) — remote players.</summary>
        public void UseNetworkSource() => fsm = null;

        /// <summary>Fires the attack animation (no-op if the controller has no such trigger yet).</summary>
        public void TriggerAttack()
        {
            if (animator != null && _hasAttack) animator.SetTrigger(_attackHash);
        }

        /// <summary>Fires a brief flinch when the entity takes damage (no-op if the controller has no "Hit" trigger).</summary>
        public void TriggerHit()
        {
            if (animator != null && _hasHit) animator.SetTrigger(_hitHash);
        }

        /// <summary>Fires a named trigger (a skill's own animTrigger). Falls back to the generic attack if the
        /// trigger is empty or the controller doesn't have it — so a skill without a custom anim still animates.</summary>
        public void TriggerNamed(string trigger)
        {
            if (animator == null) return;
            if (!string.IsNullOrEmpty(trigger) && HasParam(trigger)) animator.SetTrigger(Animator.StringToHash(trigger));
            else TriggerAttack();
        }

        /// <summary>Sets the Dead bool so the controller plays/exits the death animation (no-op if absent).</summary>
        public void SetDead(bool dead)
        {
            _dead = dead; // remembered → re-applied in Start if the params aren't cached yet
            if (animator != null && _hasDead) animator.SetBool(_deadHash, dead);
        }

        /// <summary>Sets the Mounted bool so the rider plays a sitting pose while on a mount (no-op if absent).
        /// Author the "Mounted" state as an Any-State transition so it dominates locomotion while true.</summary>
        public void SetMounted(bool mounted)
        {
            _mounted = mounted; // remembered → re-applied in Start if the params aren't cached yet (spawn-then-seat)
            if (animator != null && _hasMounted) animator.SetBool(_mountedHash, mounted);
        }

        /// <summary>Drives the gather animation: <paramref name="gatherType"/> picks the action (= Profession byte:
        /// 0 chop / 1 mine / 2 pick / …) and <paramref name="on"/> holds it while harvesting. Author one looping
        /// state per profession gated on GatherType, transitioning back to locomotion when Gathering is false.
        /// Missing params are ignored, so it won't error before you wire them.</summary>
        public void SetGather(bool on, int gatherType = 0)
        {
            if (animator == null) return;
            if (on && _hasGatherType) animator.SetInteger(_gatherTypeHash, gatherType);
            if (_hasGathering) animator.SetBool(_gatheringHash, on);
        }

        private bool HasParam(string name)
        {
            foreach (var p in animator.parameters)
                if (p.name == name) return true;
            return false;
        }
    }
}
