using UnityEngine;
using KinematicCharacterController;

namespace Arcane_Aegis.Controllers.Locomotion
{
    /// <summary>
    /// Bridge between the Kinematic Character Motor and the FSM: the motor calls these callbacks and we
    /// forward velocity/rotation to the current state. Keeps the FSM free of the KCC interface noise.
    /// </summary>
    [RequireComponent(typeof(KinematicCharacterMotor), typeof(LocomotionStateMachine))]
    public class KccMotor : MonoBehaviour, ICharacterController
    {
        [SerializeField] private KinematicCharacterMotor motor;
        [SerializeField] private LocomotionStateMachine fsm;

        private void Awake()
        {
            // Assign in Awake (NOT Start): the prefab is Instantiated at runtime, and the KCC system's
            // FixedUpdate can run before Start — leaving CharacterController null → NRE in UpdatePhase1.
            if (motor == null) motor = GetComponent<KinematicCharacterMotor>();
            if (fsm == null) fsm = GetComponent<LocomotionStateMachine>();
            motor.CharacterController = this;
        }

        public void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime)
            => fsm.Current.UpdateVelocity(ref currentVelocity, deltaTime);

        public void UpdateRotation(ref Quaternion currentRotation, float deltaTime)
            => fsm.Current.UpdateRotation(ref currentRotation, deltaTime);

        // ── Unused KCC callbacks (defaults) ──
        public void BeforeCharacterUpdate(float deltaTime) { }
        public void PostGroundingUpdate(float deltaTime) { }
        public void AfterCharacterUpdate(float deltaTime) { }
        public bool IsColliderValidForCollisions(Collider coll) => true;
        public void OnGroundHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport) { }
        public void OnMovementHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport) { }
        public void ProcessHitStabilityReport(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, Vector3 atCharacterPosition, Quaternion atCharacterRotation, ref HitStabilityReport hitStabilityReport) { }
        public void OnDiscreteCollisionDetected(Collider hitCollider) { }
    }
}
