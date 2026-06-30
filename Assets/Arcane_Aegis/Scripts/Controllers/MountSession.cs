using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using KinematicCharacterController;
using MMO.Scripts.Controllers;
using ArcaneShared.Models;
using Arcane_Aegis.Content;
using Arcane_Aegis.Controllers.Inputs;
using Arcane_Aegis.Controllers.Locomotion;
using Arcane_Aegis.Effects;
using Arcane_Aegis.Entities;
using Arcane_Aegis.Network;
using Arcane_Aegis.UI;

namespace Arcane_Aegis.Controllers
{
    /// <summary>
    /// LOCAL rider glue: turns S2C_MountState into "get on / off the mount". On mount it spawns the mount's controllable
    /// rig (its own KCC + <see cref="MountController"/>), DISABLES the player's control stack, parents the player onto the
    /// mount's seat, retargets the camera + the player's MovementSender at the rig. On dismount it reverses everything.
    /// Also owns the client-side MOUNT-TIME channel (a cast gate before the toggle is sent). Remotes are handled by the
    /// EntityManager (seats them on the snapshot mount). Scene singleton.
    /// </summary>
    public class MountSession : MonoBehaviour
    {
        public static MountSession Instance { get; private set; }

        // Hooks for VFX/SFX (wire your effects to these). Fired for the LOCAL player.
        public static event System.Action<GameObject> OnMounted;    // rig spawned (mount poof / sound)
        public static event System.Action OnDismounted;
        public static event System.Action<float> OnMountChannel;    // mount-time channel started (duration s) → show a bar
        public static event System.Action<bool> OnMountChannelEnd;  // channel ended (true = completed, false = cancelled)

        [SerializeField] private EntityManager entities;
        [Tooltip("Distância da câmera enquanto montado (modelo maior → afasta).")]
        [SerializeField] private float mountedCameraDistance = 9f;

        private GameObject _rig;                 // the spawned mount rig (null = on foot)
        private PlayerView _rider;               // the player currently mounted
        private readonly List<Behaviour> _disabled = new();   // player components we turned off (to restore)
        private readonly List<Collider> _riderCols = new();   // player colliders we turned off
        private Transform _playerCamTarget;      // the player's own camera target (to restore)
        private float _camDist, _camMax;         // camera distance/max to restore on dismount
        private Coroutine _channel;              // running mount-time channel (null = none)

        public bool IsMounted => _rig != null;

        private void Awake()
        {
            Instance = this;
            if (entities == null) entities = FindAnyObjectByType<EntityManager>();
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        // ── Button entry point (CompanionHud) — gates mounting behind the mount-time channel ──
        /// <summary>Press the mount button: dismount is instant; mounting first runs the mount-time channel (cancellable
        /// by moving). Re-pressing during the channel cancels it.</summary>
        public void RequestToggleMount()
        {
            if (IsMounted) { NetClient.Instance?.SendToggleMount(); return; } // dismount = instant
            if (_channel != null) { CancelChannel(); return; }               // pressed again → cancel the pending mount

            float t = ActiveMountTime();
            if (t <= 0f) { NetClient.Instance?.SendToggleMount(); return; }   // instant mount
            _channel = StartCoroutine(ChannelMount(t));
        }

        private IEnumerator ChannelMount(float duration)
        {
            OnMountChannel?.Invoke(duration);
            var input = entities != null && entities.Local != null ? entities.Local.GetComponent<PlayerInput>() : null;
            float t = 0f;
            while (t < duration)
            {
                if (input != null && input.Move.sqrMagnitude > 0.01f) { CancelChannel(); yield break; } // moved → cancel
                t += Time.deltaTime;
                yield return null;
            }
            _channel = null;
            OnMountChannelEnd?.Invoke(true);
            NetClient.Instance?.SendToggleMount();
        }

        private void CancelChannel()
        {
            if (_channel != null) StopCoroutine(_channel);
            _channel = null;
            OnMountChannelEnd?.Invoke(false);
        }

        /// <summary>The active mount's mount-up time (s), or 0 if none/instant.</summary>
        private float ActiveMountTime()
        {
            if (ContentLibrary.Active == null) return 0f;
            foreach (var c in CompanionState.Companions)
                if (c.Kind == OwnedCompanion.KindMount && c.Active)
                {
                    var d = ContentLibrary.Active.GetMount(c.DefId);
                    return d != null ? Mathf.Max(0f, d.mountTimeSeconds) : 0f;
                }
            return 0f;
        }

        // ── Mount / dismount (driven by the server's S2C_MountState) ──
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
            mc.canFly = def.canFly; // the MountDefinition is the single source of truth for flight (server + client)
            // Only the player's (retargeted) MovementSender reports the mount — kill any stray sender on the rig.
            foreach (var s in _rig.GetComponentsInChildren<MovementSender>(true)) s.enabled = false;

            DissolveEffect.PlayIn(_rig); // materialize the mount in — called BEFORE seating the rider so only the rig dissolves

            // Turn OFF the player's control stack (keep PlayerInput — the mount reads it; keep MovementSender — we
            // retarget it to report the MOUNT rig's transform so the server + remotes track the mount).
            _disabled.Clear();
            Disable(rt.GetComponent<KccMotor>());
            Disable(rt.GetComponent<LocomotionStateMachine>());
            var motor = rt.GetComponent<KinematicCharacterMotor>();
            if (motor != null) motor.enabled = false;       // re-enabled explicitly on dismount (not via _disabled)

            // Disable the rider's colliders so the mount's KCC doesn't fight them (local physics only).
            _riderCols.Clear();
            foreach (var c in rt.GetComponents<Collider>()) if (c.enabled) { c.enabled = false; _riderCols.Add(c); }

            // Seat the rider on the mount + put it in the sitting pose.
            rt.SetParent(mc.RiderSeat, worldPositionStays: false);
            rt.localPosition = Vector3.zero;
            rt.localRotation = Quaternion.identity;
            EntityAnimation.Of(rider.gameObject).SetMounted(true);

            // Hand control to the mount + retarget the player's sender to report the mount's transform.
            var input = rider.GetComponent<PlayerInput>();
            mc.ActivateLocal(input);
            rider.GetComponent<MovementSender>()?.SetMountSource(_rig.transform, mc);

            // Re-point the camera at the mount's target + pull back a bit (the mount is bigger).
            var cam = FindAnyObjectByType<MMOCamera>();
            if (cam != null)
            {
                _playerCamTarget = cam.FollowTransform;
                _camDist = cam.TargetDistance;
                _camMax = cam.MaxDistance;
                cam.SetFollowTransform(mc.CameraTarget);
                if (cam.MaxDistance < mountedCameraDistance) cam.MaxDistance = mountedCameraDistance;
                cam.TargetDistance = mountedCameraDistance;
            }

            OnMounted?.Invoke(_rig);
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
                EntityAnimation.Of(rt.gameObject).SetMounted(false); // back to normal locomotion pose
                rt.GetComponent<MovementSender>()?.ClearMountSource();  // report the player's own transform again
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
            if (cam != null)
            {
                if (_playerCamTarget != null) cam.SetFollowTransform(_playerCamTarget);
                cam.MaxDistance = _camMax;
                cam.TargetDistance = _camDist;
            }
            _playerCamTarget = null;

            // Dissolve the rig out, THEN destroy it (the rider is already un-parented above, so it won't dissolve).
            var rig = _rig;
            _rig = null;
            _rider = null;
            OnDismounted?.Invoke();
            if (rig != null)
            {
                foreach (var mc in rig.GetComponentsInChildren<MountController>(true)) mc.enabled = false; // freeze it
                foreach (var mo in rig.GetComponentsInChildren<KinematicCharacterMotor>(true)) mo.enabled = false;
                DissolveEffect.PlayOut(rig, 0.6f, null, () => { if (rig != null) Destroy(rig); });
            }
        }

        private void Disable(Behaviour b)
        {
            if (b != null && b.enabled) { b.enabled = false; _disabled.Add(b); }
        }
    }
}
