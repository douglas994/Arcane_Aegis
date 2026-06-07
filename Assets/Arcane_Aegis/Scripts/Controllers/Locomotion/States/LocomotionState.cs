using UnityEngine;
using ArcaneShared.Enums;

namespace Arcane_Aegis.Controllers.Locomotion.States
{
    /// <summary>Moving on the ground (walk/run).</summary>
    public sealed class LocomotionState : ILocomotionState
    {
        private readonly LocomotionStateMachine _sm;
        public LocomotionState(LocomotionStateMachine sm) => _sm = sm;

        public MovementState NetState => MovementState.Locomotion;

        public void Enter() { }
        public void Exit() { }

        public void Tick(float dt)
        {
            if (!_sm.IsGrounded) { _sm.ChangeState(_sm.Air); return; }
            if (_sm.Input != null && _sm.Input.ConsumeJump()) { _sm.RequestJump(); _sm.ChangeState(_sm.Air); return; }
            if (_sm.MoveDirection().sqrMagnitude <= 0.0001f) { _sm.ChangeState(_sm.Idle); return; }
        }

        public void UpdateVelocity(ref Vector3 velocity, float dt)
        {
            Vector3 target = _sm.MoveDirection() * _sm.CurrentSpeed;
            velocity = new Vector3(target.x, velocity.y, target.z);
        }

        public void UpdateRotation(ref Quaternion rotation, float dt)
        {
            Vector3 dir = _sm.MoveDirection();
            if (dir.sqrMagnitude > 0.0001f)
                rotation = Quaternion.Slerp(rotation, Quaternion.LookRotation(dir, Vector3.up), dt * _sm.RotationSharpness);
        }
    }
}
