using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using Arcane_Aegis.Network;
using PlayerInput = Arcane_Aegis.Controllers.Inputs.PlayerInput; // disambiguate from UnityEngine.InputSystem.PlayerInput

namespace Arcane_Aegis.Controllers.Combat
{
    /// <summary>
    /// Local player's combat input. Reads the Attack action and asks the SERVER to cast (server-authoritative
    /// — the client never decides damage). Separate from the locomotion FSM, so you can move while attacking.
    /// Plays a predicted attack animation immediately; the server confirms via S2C_CombatEvent.
    /// </summary>
    public class PlayerCombat : MonoBehaviour
    {
        [SerializeField] private byte basicAbilityId = 1;
        [SerializeField] private float localCooldown = 0.4f; // mirrors the ability's cooldown for responsiveness
        [SerializeField] private PlayerInput input;
        [SerializeField] private CharacterAnimator animator;

        private NetClient _net;
        private float _nextCast;

        private void Start()
        {
            if (input == null) input = GetComponent<PlayerInput>();
            if (animator == null) animator = GetComponentInChildren<CharacterAnimator>();
            _net = FindAnyObjectByType<NetClient>();
        }

        private void Update()
        {
            if (_net == null) return;

            // Left mouse (Attack) = basic ability — but ignore the click if it landed on the UI
            // (a UI button already handled it), otherwise it would cast twice.
            if (input != null && input.ConsumeAttack() && !IsPointerOverUI()) Cast(basicAbilityId);

            // Ability slots 1..4 (raw keys for now; proper bindings/action-bar later).
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.digit1Key.wasPressedThisFrame) Cast(1);
                if (kb.digit2Key.wasPressedThisFrame) Cast(2);
                if (kb.digit3Key.wasPressedThisFrame) Cast(3);
                if (kb.digit4Key.wasPressedThisFrame) Cast(4);
            }
        }

        private static bool IsPointerOverUI()
            => EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        private void Cast(byte abilityId)
        {
            if (Time.time < _nextCast) return;               // light anti-spam; server enforces real cooldowns
            _nextCast = Time.time + localCooldown;
            _net.SendCast(abilityId, 0);                     // 0 = action aim; server uses our facing
            if (animator != null) animator.TriggerAttack();  // predicted
        }
    }
}
