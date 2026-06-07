using UnityEngine;
using KinematicCharacterController;
using Arcane_Aegis.Controllers;
using Arcane_Aegis.Controllers.Inputs;
using Arcane_Aegis.Controllers.Locomotion;
using Arcane_Aegis.Controllers.Combat;

namespace Arcane_Aegis.Entities
{
    /// <summary>
    /// A player view (mirrors the server's Player). ONE prefab serves both the local and remote player —
    /// <see cref="Initialize"/> wires the control layer:
    ///   local  → KCC + FSM + input drive the transform (interpolation OFF, anim from the FSM)
    ///   remote → snapshot interpolation drives it (control OFF, anim from networked speed)
    /// </summary>
    public class PlayerView : HumanoidView
    {
        /// <summary>The KCC motor (local only) — used for server position corrections.</summary>
        public KinematicCharacterMotor Motor { get; private set; }
        public bool IsLocal { get; private set; }

        public void Initialize(bool isLocal)
        {
            IsLocal = isLocal;
            gameObject.tag = isLocal ? "Player" : "Untagged"; // the camera follows only the local "Player"

            Motor = GetComponent<KinematicCharacterMotor>();
            var kcc = GetComponent<KccMotor>();
            var fsm = GetComponent<LocomotionStateMachine>();
            var input = GetComponent<PlayerInput>();
            var sender = GetComponent<MovementSender>();
            var combat = GetComponent<PlayerCombat>();
            var anim = GetComponentInChildren<CharacterAnimator>();

            // Control stack ON for local, OFF for remote (the motor stops simulating when disabled).
            if (Motor != null) Motor.enabled = isLocal;
            if (kcc != null) kcc.enabled = isLocal;
            if (fsm != null) fsm.enabled = isLocal;
            if (input != null) input.enabled = isLocal;
            if (sender != null) sender.enabled = isLocal;
            if (combat != null) combat.enabled = isLocal;

            SetInterpolated(!isLocal);                 // remote follows snapshots; local drives via KCC

            if (anim != null)
            {
                if (isLocal) anim.UseFsm(fsm);         // accurate speed/state from the FSM
                else anim.UseNetworkSource();          // speed/state from the network (EntityView)
            }

            ShowWorldVitals(!isLocal);                 // own HP is on the HUD, not above the head
        }
    }
}
