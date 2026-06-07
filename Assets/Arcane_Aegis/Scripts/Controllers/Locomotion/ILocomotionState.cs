using UnityEngine;
using ArcaneShared.Enums;

namespace Arcane_Aegis.Controllers.Locomotion
{
    /// <summary>
    /// One locomotion state. Tick() decides transitions; UpdateVelocity/UpdateRotation feed the KCC
    /// (called via KccMotor). NetState is the value reported to the server in the snapshot.
    /// </summary>
    public interface ILocomotionState
    {
        MovementState NetState { get; }

        void Enter();
        void Exit();
        void Tick(float deltaTime);
        void UpdateVelocity(ref Vector3 velocity, float deltaTime);
        void UpdateRotation(ref Quaternion rotation, float deltaTime);
    }
}
