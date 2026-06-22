using UnityEngine;
using Arcane_Aegis.Controllers.Locomotion;
using Arcane_Aegis.Network;
using ArcaneShared.Enums;

namespace Arcane_Aegis.Controllers
{
    /// <summary>
    /// Networking layer of the local player: reports transform + current MovementState to the server
    /// ~15 Hz. Reads the FSM's current state; the server validates the position (Rules §0.2). The SAME
    /// component on a mount rig reports the mount instead — leave <see cref="fsm"/> empty and assign
    /// <see cref="mount"/> (the player's sender is disabled while mounted; the mount's drives the position).
    /// </summary>
    public class MovementSender : MonoBehaviour
    {
        [SerializeField] private LocomotionStateMachine fsm;
        [SerializeField] private MountController mount; // optional: mount rigs use this instead of the player FSM
        [SerializeField] private NetClient net;
        [SerializeField] private float sendInterval = 1f / 15f;

        private float _timer;

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

            MovementState state = fsm != null ? fsm.Current.NetState
                                : mount != null ? mount.NetState
                                : MovementState.Idle;
            net.SendMovement(transform.position, transform.eulerAngles.y, state);
        }
    }
}
