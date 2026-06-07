using UnityEngine;
using ArcaneShared.Enums;

namespace Arcane_Aegis.Controllers.Locomotion.States
{
    /// <summary>Standing still on the ground.</summary>
    public sealed class IdleState : ILocomotionState
    {
        private readonly LocomotionStateMachine _sm;
        public IdleState(LocomotionStateMachine sm) => _sm = sm;

        public MovementState NetState => MovementState.Idle;

        public void Enter() { }
        public void Exit() { }

        public void Tick(float dt)
        {
            if (!_sm.IsGrounded) { _sm.ChangeState(_sm.Air); return; }
            if (_sm.Input != null && _sm.Input.ConsumeJump()) { _sm.RequestJump(); _sm.ChangeState(_sm.Air); return; }
            if (_sm.MoveDirection().sqrMagnitude > 0.0001f) { _sm.ChangeState(_sm.Loco); return; }
        }

        public void UpdateVelocity(ref Vector3 velocity, float dt)
            => velocity = Vector3.Lerp(velocity, Vector3.zero, dt * 15f);

        public void UpdateRotation(ref Quaternion rotation, float dt) { }
    }
}
