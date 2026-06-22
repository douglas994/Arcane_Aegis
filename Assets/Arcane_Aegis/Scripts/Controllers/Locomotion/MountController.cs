using UnityEngine;
using KinematicCharacterController;
using Arcane_Aegis.Controllers.Inputs;
using ArcaneShared.Enums;

namespace Arcane_Aegis.Controllers.Locomotion
{
    /// <summary>
    /// Drives a MOUNT's own Kinematic Character Motor — SEPARATE from the player's locomotion FSM (mount types vary:
    /// ground walkers, flyers, …). Lives on the mount prefab ROOT. When the LOCAL player mounts, <see cref="ActivateLocal"/>
    /// hands it the player's input and it takes over movement; remotes leave it disabled (the MountView interpolates the
    /// snapshot instead). Modular: <see cref="canFly"/> switches between ground (gravity) and free-flight (Space ↑ / Ctrl ↓)
    /// motion; tune the speeds per-prefab so each mount feels different. The rider is parented to <see cref="riderSeat"/>.
    /// </summary>
    [RequireComponent(typeof(KinematicCharacterMotor))]
    public class MountController : MonoBehaviour, ICharacterController
    {
        [Header("Refs")]
        [Tooltip("Onde o player senta (filho vazio na garupa/sela). Se vazio, procura um filho chamado 'RiderSeat'.")]
        public Transform riderSeat;
        [Tooltip("Alvo da câmera enquanto montado (filho vazio). Se vazio, procura 'Target', senão usa a raiz.")]
        public Transform cameraTarget;
        public KinematicCharacterMotor Motor;

        [Header("Movimento")]
        [Tooltip("Velocidade no chão (m/s). Mantenha coerente com speedMult do SO p/ não bater no cap do server.")]
        public float groundSpeed = 6f;
        public float gravity = 25f;
        public float rotationSharpness = 12f;

        [Header("Voo")]
        [Tooltip("Se marcado, a montaria voa: sem gravidade, Espaço sobe / Ctrl desce.")]
        public bool canFly = false;
        [Tooltip("Velocidade horizontal voando (m/s).")] public float flySpeed = 8f;
        [Tooltip("Velocidade vertical (subir/descer) voando (m/s).")] public float ascendSpeed = 5f;

        public bool CanFly => canFly;

        private PlayerInput _input;
        private Transform _cam;
        private Vector3 _planarVel;     // last horizontal velocity (for the NetState / anim)
        private float _verticalVel;     // gravity (ground) or ascend/descend (fly)
        private bool _active;           // true only on the LOCAL rider's mount

        private void Awake()
        {
            if (Motor == null) Motor = GetComponent<KinematicCharacterMotor>();
            if (riderSeat == null) riderSeat = FindChild(transform, "RiderSeat");
            if (cameraTarget == null) cameraTarget = FindChild(transform, "Target") ?? transform;
        }

        /// <summary>The transform the rider is parented to (the seat), or the root as a fallback.</summary>
        public Transform RiderSeat => riderSeat != null ? riderSeat : transform;
        /// <summary>The transform the camera should orbit while mounted.</summary>
        public Transform CameraTarget => cameraTarget != null ? cameraTarget : transform;

        /// <summary>Hand control to the LOCAL player's input and start driving the motor. Call on mount.</summary>
        public void ActivateLocal(PlayerInput input)
        {
            _input = input;
            _active = true;
            if (Motor != null) { Motor.CharacterController = this; Motor.enabled = true; }
            enabled = true;
        }

        /// <summary>Movement state for the network sender (so the mount's anim/snapshot reads moving vs idle).</summary>
        public MovementState NetState =>
            (_planarVel.sqrMagnitude > 0.04f || Mathf.Abs(_verticalVel) > 0.1f) ? MovementState.Locomotion : MovementState.Idle;

        // ── ICharacterController ──
        public void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime)
        {
            if (!_active || _input == null) { currentVelocity = Vector3.zero; return; }

            Vector3 dir = MoveDirection();          // camera-relative, XZ, magnitude 0..1
            if (canFly)
            {
                _planarVel = dir * flySpeed;
                float up = (_input.AscendHeld ? 1f : 0f) - (_input.DescendHeld ? 1f : 0f);
                _verticalVel = up * ascendSpeed;
            }
            else
            {
                _planarVel = dir * groundSpeed;
                if (Motor.GroundingStatus.IsStableOnGround) _verticalVel = 0f;
                else _verticalVel -= gravity * deltaTime; // fall
            }
            currentVelocity = new Vector3(_planarVel.x, _verticalVel, _planarVel.z);
        }

        public void UpdateRotation(ref Quaternion currentRotation, float deltaTime)
        {
            Vector3 face = _planarVel; face.y = 0f;
            if (face.sqrMagnitude < 1e-4f) return; // no horizontal move → keep facing
            Quaternion target = Quaternion.LookRotation(face.normalized, Vector3.up);
            currentRotation = Quaternion.Slerp(currentRotation, target, 1f - Mathf.Exp(-rotationSharpness * deltaTime));
        }

        /// <summary>Move direction from input, relative to the camera's facing (XZ plane).</summary>
        private Vector3 MoveDirection()
        {
            Vector2 m = _input.Move;
            if (m.sqrMagnitude < 1e-6f) return Vector3.zero;
            if (_cam == null && Camera.main != null) _cam = Camera.main.transform;

            Vector3 dir;
            if (_cam != null)
            {
                Vector3 fwd = _cam.forward; fwd.y = 0f;
                Vector3 right = _cam.right; right.y = 0f;
                dir = fwd.normalized * m.y + right.normalized * m.x;
            }
            else dir = new Vector3(m.x, 0f, m.y);
            return dir.sqrMagnitude > 1f ? dir.normalized : dir;
        }

        private static Transform FindChild(Transform parent, string name)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform c = parent.GetChild(i);
                if (c.name == name) return c;
                Transform r = FindChild(c, name);
                if (r != null) return r;
            }
            return null;
        }

        // ── Unused KCC callbacks ──
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
