using System.Collections.Generic;
using UnityEngine;
using KinematicCharacterController;
using MMO.Scripts.Controllers;
using Arcane_Aegis.Content;
using Arcane_Aegis.Controllers.Inputs;
using Arcane_Aegis.Controllers.Locomotion;
using Arcane_Aegis.Entities;

namespace Arcane_Aegis.Controllers
{
    /// <summary>
    /// LOCAL rider glue: turns S2C_MountState into "get on / off the mount". On mount it spawns the mount's controllable
    /// rig (its own KCC + <see cref="MountController"/>), DISABLES the player's control stack, parents the player onto the
    /// mount's seat, and re-points the camera + a movement sender at the rig. On dismount it reverses everything and
    /// destroys the rig. Remotes are handled elsewhere (EntityManager seats them on the snapshot mount). Scene singleton.
    /// </summary>
    public class MountSession : MonoBehaviour
    {
        public static MountSession Instance { get; private set; }

        [SerializeField] private EntityManager entities;

        private GameObject _rig;                 // the spawned mount rig (null = on foot)
        private PlayerView _rider;               // the player currently mounted
        private readonly List<Behaviour> _disabled = new();   // player components we turned off (to restore)
        private readonly List<Collider> _riderCols = new();   // player colliders we turned off
        private Transform _playerCamTarget;      // the player's own camera target (to restore)

        public bool IsMounted => _rig != null;

        private void Awake()
        {
            Instance = this;
            if (entities == null) entities = FindAnyObjectByType<EntityManager>();
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        /// <summary>Get on the mount (local player). No-op if already mounted or no local player.</summary>
        public void Mount(string mountDefId)
        {
            if (_rig != null) return;
            var rider = entities != null ? entities.Local : null;
            if (rider == null || string.IsNullOrEmpty(mountDefId)) return;

            var def = ContentLibrary.Active != null ? ContentLibrary.Active.GetMount(mountDefId) : null;
            GameObject prefab = def != null ? def.mountPrefab : null;
            if (prefab == null)
            {
                // No rig authored → degrade to a speed-only boost on the FSM (still rideable, just no model).
                if (def != null) rider.Locomotion?.SetMount(def.speedMult);
                return;
            }

            _rider = rider;
            Transform rt = rider.transform;

            // Spawn the rig at the rider's ground position + facing.
            _rig = Instantiate(prefab, rt.position, Quaternion.Euler(0f, rt.eulerAngles.y, 0f));
            var mc = _rig.GetComponentInChildren<MountController>();
            if (mc == null) { Debug.LogError("[MountSession] mountPrefab has no MountController — destroying rig."); Destroy(_rig); _rig = null; _rider = null; return; }

            // Turn OFF the player's control stack (keep PlayerInput — the mount reads it).
            _disabled.Clear();
            Disable(rt.GetComponent<KccMotor>());
            Disable(rt.GetComponent<LocomotionStateMachine>());
            Disable(rt.GetComponent<MovementSender>());     // the rig's own sender reports position now
            var motor = rt.GetComponent<KinematicCharacterMotor>();
            if (motor != null) motor.enabled = false;       // re-enabled explicitly on dismount (not via _disabled)

            // Disable the rider's colliders so the mount's KCC doesn't fight them (local physics only).
            _riderCols.Clear();
            foreach (var c in rt.GetComponents<Collider>()) if (c.enabled) { c.enabled = false; _riderCols.Add(c); }

            // Seat the rider on the mount.
            rt.SetParent(mc.RiderSeat, worldPositionStays: false);
            rt.localPosition = Vector3.zero;
            rt.localRotation = Quaternion.identity;

            // Hand control to the mount + start reporting the mount's position to the server.
            var input = rider.GetComponent<PlayerInput>();
            mc.ActivateLocal(input);
            var rigSender = _rig.GetComponentInChildren<MovementSender>(true);
            if (rigSender != null) rigSender.enabled = true;

            // Re-point the camera at the mount's target.

            var cam = FindAnyObjectByType<MMOCamera>();
            if (cam != null)
            {
                _playerCamTarget = cam.FollowTransform; // remember the player's target
                cam.SetFollowTransform(mc.CameraTarget);
            }
        }

        /// <summary>Get off the mount (local player). No-op if not mounted.</summary>
        public void Dismount()
        {
            if (_rider != null) _rider.Locomotion?.SetMount(1f); // clear any fallback speed boost
            if (_rig == null) { _rider = null; return; }

            Transform rt = _rider != null ? _rider.transform : null;
            Vector3 ground = _rig.transform.position;
            float yaw = _rig.transform.eulerAngles.y;

            if (rt != null)
            {
                rt.SetParent(null, worldPositionStays: false);
                rt.SetPositionAndRotation(ground, Quaternion.Euler(0f, yaw, 0f));

                // Restore the rider's colliders + control stack.
                foreach (var c in _riderCols) if (c != null) c.enabled = true;
                _riderCols.Clear();

                var motor = rt.GetComponent<KinematicCharacterMotor>();
                if (motor != null) { motor.enabled = true; motor.SetPositionAndRotation(ground, Quaternion.Euler(0f, yaw, 0f)); }
                foreach (var b in _disabled) if (b != null) b.enabled = true;
                _disabled.Clear();
            }

            var cam = FindAnyObjectByType<MMOCamera>();
            if (cam != null && _playerCamTarget != null) cam.SetFollowTransform(_playerCamTarget);
            _playerCamTarget = null;

            Destroy(_rig);
            _rig = null;
            _rider = null;
        }

        private void Disable(Behaviour b)
        {
            if (b != null && b.enabled) { b.enabled = false; _disabled.Add(b); }
        }
    }
}
