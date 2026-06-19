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
    /// <see cref="Spawn"/> wires the control layer:
    ///   local  → KCC + FSM + input drive the transform (interpolation OFF, anim from the FSM)
    ///   remote → snapshot interpolation drives it (control OFF, anim from networked speed)
    /// Holds the OWN player's exact vitals/XP (from S2C_StateUpdate / S2C_Experience) for the HUD — remotes only
    /// carry the quantized <see cref="EntityView.SnapHp01"/>.
    /// </summary>
    public class PlayerView : HumanoidView
    {
        // Exact vitals — filled for the OWN player from S2C_StateUpdate (the HUD reads these).
        public int Hp { get; private set; }
        public int MaxHp { get; private set; }
        public int Mana { get; private set; }
        public int MaxMana { get; private set; }
        public float HpFraction => MaxHp > 0 ? (float)Hp / MaxHp : 0f;
        public float ManaFraction => MaxMana > 0 ? (float)Mana / MaxMana : 0f;

        // XP/level (own player only, from S2C_Experience) — drives the HUD XP bar.
        public ushort Level { get; private set; } = 1;
        public uint Xp { get; private set; }
        public uint XpToNext { get; private set; } = 1;
        public float XpFraction => XpToNext > 0 ? Mathf.Clamp01((float)Xp / XpToNext) : 0f;

        public void SetVitals(int hp, int maxHp, int mana, int maxMana)
        {
            Hp = hp; MaxHp = maxHp; Mana = mana; MaxMana = maxMana;
        }

        public void SetExperience(ushort level, uint xp, uint xpToNext)
        {
            Level = level; Xp = xp; XpToNext = xpToNext;
        }

        /// <summary>The KCC motor (local only) — used for server position corrections.</summary>
        public KinematicCharacterMotor Motor { get; private set; }
        public bool IsLocal { get; private set; }
        /// <summary>This player's combat (cooldown owner) — the action-bar casts through it.</summary>
        public PlayerCombat Combat { get; private set; }
        /// <summary>This player's locomotion FSM (local only) — CC replication locks its input.</summary>
        public LocomotionStateMachine Locomotion { get; private set; }

        public override void Spawn(bool isLocal)
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
            Combat = combat;
            Locomotion = fsm;

            SetInterpolated(!isLocal);                 // remote follows snapshots; local drives via KCC

            if (anim != null)
            {
                if (isLocal) anim.UseFsm(fsm);         // accurate speed/state from the FSM
                else anim.UseNetworkSource();          // speed/state from the network (EntityView)
            }

            GetComponent<CombatantVitals>()?.ShowWorldBar(!isLocal); // own HP is on the HUD, not above the head

            // Show the equipped weapon model on EVERY player (local from inventory, remotes from the replicated id).
            if (GetComponent<Arcane_Aegis.Combat.WeaponVisual>() == null)
                gameObject.AddComponent<Arcane_Aegis.Combat.WeaponVisual>();
        }
    }
}
