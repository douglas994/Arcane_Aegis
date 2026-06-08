using UnityEngine;
using Arcane_Aegis.Controllers.Locomotion;
using Arcane_Aegis.Network;
using ArcaneShared.Enums;

namespace Arcane_Aegis.Controllers
{
    /// <summary>
    /// Networking layer of the local player: reports transform + current MovementState to the server
    /// ~15 Hz. Reads the FSM's current state; the server validates the position (Rules §0.2).
    /// </summary>
    public class MovementSender : MonoBehaviour
    {
        [SerializeField] private LocomotionStateMachine fsm;
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

            MovementState state = fsm != null ? fsm.Current.NetState : MovementState.Idle;
            net.SendMovement(transform.position, transform.eulerAngles.y, state);
        }
    }
}
