using UnityEngine;
using Arcane_Aegis.Controllers.Locomotion;
using Arcane_Aegis.Network;
using ArcaneShared.Enums;

namespace Arcane_Aegis.Controllers
{
    /// <summary>
    /// Networking layer of the local player: reports transform + current MovementState to the server
    /// ~15 Hz. Reads the FSM's current state; the server validates the position (Rules §0.2).
    /// While MOUNTED, <see cref="SetMountSource"/> retargets it to report the MOUNT rig's transform + state
    /// instead (so the server — and thus every remote viewer — tracks the mount), without a second sender.
    /// </summary>
    public class MovementSender : MonoBehaviour
    {
        [SerializeField] private LocomotionStateMachine fsm;
        [SerializeField] private NetClient net;
        [SerializeField] private float sendInterval = 1f / 15f;

        private float _timer;
        private Transform _mountSource;     // while mounted: the mount rig's transform (else null → use own)
        private MountController _mountState; // while mounted: the mount's NetState source

        /// <summary>Mounted: report the mount rig's transform + state instead of the player's.</summary>
        public void SetMountSource(Transform rig, MountController mount) { _mountSource = rig; _mountState = mount; }
        /// <summary>Dismounted: back to reporting the player's own transform + FSM.</summary>
        public void ClearMountSource() { _mountSource = null; _mountState = null; }

        private void Start()
        {
            if (fsm == null) fsm = GetComponent<LocomotionStateMachine>();
            if (net == null) net = NetClient.Instance ?? FindAnyObjectByType<NetClient>();
        }

        private void Update()
        {
            if (net == null) return;

            _timer += Time.deltaTime;
            if (_timer < sendInterval) return;
            _timer = 0f;

            Transform src = _mountSource != null ? _mountSource : transform;
            MovementState state = _mountState != null ? _mountState.NetState
                                : fsm != null ? fsm.Current.NetState
                                : MovementState.Idle;
            // Survival v2: sprinting = dashing (Shift) while actually moving on foot (not mounted) → server drains stamina.
            bool sprinting = _mountState == null && fsm != null && fsm.Dashing && fsm.MoveDirection().sqrMagnitude > 0.01f;
            net.SendMovement(src.position, src.eulerAngles.y, state, sprinting);
        }
    }
}
