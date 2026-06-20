using UnityEngine;
using ArcaneShared.Enums;

namespace Arcane_Aegis.Controllers.Locomotion.States
{
    /// <summary>
    /// Dead: the server killed this player (HP hit 0). Movement, jump and rotation are locked — only gravity keeps
    /// the body resting on the ground. Reports <see cref="MovementState.Dead"/> so remotes animate the death pose.
    /// Exits back to Idle/Airborne when the server revives the player (vitals &gt; 0 → SetDead(false)).
    /// </summary>
    public sealed class DeadState : ILocomotionState
    {
        private readonly LocomotionStateMachine _sm;
        public DeadState(LocomotionStateMachine sm) => _sm = sm;

        public MovementState NetState => MovementState.Dead;

        public void Enter() { }
        public void Exit() { }

        public void Tick(float dt)
        {
            if (!_sm.IsDead) _sm.ChangeState(_sm.IsGrounded ? _sm.Idle : _sm.Air); // revived → resume normal locomotion
        }

        public void UpdateVelocity(ref Vector3 velocity, float dt)
        {
            velocity.x = 0f; velocity.z = 0f;                          // no horizontal control while dead
            if (!_sm.IsGrounded) velocity.y -= _sm.Gravity * dt;       // settle onto the ground
            else if (velocity.y < 0f) velocity.y = 0f;
        }

        public void UpdateRotation(ref Quaternion rotation, float dt) { }
    }
}
