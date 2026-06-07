using UnityEngine;
using ArcaneShared.Enums;

namespace Arcane_Aegis.Controllers.Locomotion.States
{
    /// <summary>Jumping / falling (not grounded).</summary>
    public sealed class AirborneState : ILocomotionState
    {
        private readonly LocomotionStateMachine _sm;
        public AirborneState(LocomotionStateMachine sm) => _sm = sm;

        public MovementState NetState => MovementState.Airborne;

        public void Enter() { }
        public void Exit() { }

        public void Tick(float dt)
        {
            if (!_sm.IsGrounded) return;
            if (_sm.MoveDirection().sqrMagnitude > 0.0001f) _sm.ChangeState(_sm.Loco);
            else _sm.ChangeState(_sm.Idle);
        }

        public void UpdateVelocity(ref Vector3 velocity, float dt)
        {
            if (_sm.TakeJump()) velocity.y = _sm.JumpSpeed;     // jump impulse (once)
            velocity.y -= _sm.Gravity * dt;                      // gravity

            // light air control on the horizontal plane
            Vector3 horizontal = new Vector3(velocity.x, 0f, velocity.z);
            Vector3 target = _sm.MoveDirection() * _sm.CurrentSpeed;
            horizontal = Vector3.Lerp(horizontal, target, dt * _sm.AirControl);
            velocity = new Vector3(horizontal.x, velocity.y, horizontal.z);
        }

        public void UpdateRotation(ref Quaternion rotation, float dt)
        {
            Vector3 dir = _sm.MoveDirection();
            if (dir.sqrMagnitude > 0.0001f)
                rotation = Quaternion.Slerp(rotation, Quaternion.LookRotation(dir, Vector3.up), dt * _sm.RotationSharpness);
        }
    }
}
