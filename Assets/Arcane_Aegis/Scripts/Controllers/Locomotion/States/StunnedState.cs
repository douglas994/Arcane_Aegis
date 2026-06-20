using UnityEngine;
using ArcaneShared.Enums;

namespace Arcane_Aegis.Controllers.Locomotion.States
{
    /// <summary>
    /// Stunned: a server-replicated crowd-control (S2C_ControlState → SetControl) forbids acting/moving. Like
    /// <see cref="DeadState"/> but recoverable — movement/jump/rotation are locked and only gravity applies; reports
    /// <see cref="MovementState.Stunned"/> so remotes animate the stun. Exits to Idle/Airborne when the stun clears.
    /// </summary>
    public sealed class StunnedState : ILocomotionState
    {
        private readonly LocomotionStateMachine _sm;
        public StunnedState(LocomotionStateMachine sm) => _sm = sm;

        public MovementState NetState => MovementState.Stunned;

        public void Enter() { }
        public void Exit() { }

        public void Tick(float dt)
        {
            if (!_sm.Stunned) _sm.ChangeState(_sm.IsGrounded ? _sm.Idle : _sm.Air); // stun cleared (death is handled centrally)
        }

        public void UpdateVelocity(ref Vector3 velocity, float dt)
        {
            velocity.x = 0f; velocity.z = 0f;                          // no control while stunned
            if (!_sm.IsGrounded) velocity.y -= _sm.Gravity * dt;       // still fall/settle
            else if (velocity.y < 0f) velocity.y = 0f;
        }

        public void UpdateRotation(ref Quaternion rotation, float dt) { }
    }
}
