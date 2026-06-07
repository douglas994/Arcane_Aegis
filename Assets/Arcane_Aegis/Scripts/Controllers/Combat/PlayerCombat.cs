using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using Arcane_Aegis.Network;
using PlayerInput = Arcane_Aegis.Controllers.Inputs.PlayerInput; // disambiguate from UnityEngine.InputSystem.PlayerInput

namespace Arcane_Aegis.Controllers.Combat
{
    /// <summary>
    /// Local player's combat — the single owner of casting on the client. Keyboard, mouse AND UI buttons all
    /// route through <see cref="TryCast"/>, so the cooldown gate is consistent everywhere. The server is
    /// authoritative: we ask it to cast (never decide damage) and it tells us the real cooldown via
    /// S2C_AbilityCast → <see cref="OnServerCooldown"/>. We start an optimistic cooldown on cast for snappy
    /// UI, then snap to the server's value. The UI reads <see cref="GetCooldownRemaining01"/> for a radial fill.
    /// Separate from the locomotion FSM (you can move while attacking).
    /// </summary>
    public class PlayerCombat : MonoBehaviour
    {
        [SerializeField] private byte basicAbilityId = 1;
        [SerializeField] private float defaultCooldown = 0.3f; // assumed until the server tells us the real value
        [SerializeField] private PlayerInput input;
        [SerializeField] private CharacterAnimator animator;

        private NetClient _net;
        private readonly Dictionary<byte, float> _readyAt = new();    // ability id → Time.time when castable again
        private readonly Dictionary<byte, float> _cdDuration = new(); // ability id → last-known cooldown (seconds)
        private readonly Dictionary<byte, float> _castAt = new();     // ability id → Time.time of the last cast (anchor)

        private void Start()
        {
            if (input == null) input = GetComponent<PlayerInput>();
            if (animator == null) animator = GetComponentInChildren<CharacterAnimator>();
            _net = FindAnyObjectByType<NetClient>();
        }

        private void Update()
        {
            if (_net == null) return;

            // Left mouse (Attack) = basic ability — ignore clicks over the UI (a button handled them).
            if (input != null && input.ConsumeAttack() && !IsPointerOverUI()) TryCast(basicAbilityId);

            // Ability slots 1..4 (raw keys for now; proper bindings/action-bar later).
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.digit1Key.wasPressedThisFrame) TryCast(1);
                if (kb.digit2Key.wasPressedThisFrame) TryCast(2);
                if (kb.digit3Key.wasPressedThisFrame) TryCast(3);
                if (kb.digit4Key.wasPressedThisFrame) TryCast(4);
            }
        }

        /// <summary>Casts if off cooldown. Called by keys/mouse AND by UI buttons (via NetClient). Returns false if on cooldown.</summary>
        public bool TryCast(byte abilityId)
        {
            if (_net == null) return false;
            if (GetCooldownRemaining(abilityId) > 0f) return false;

            // optimistic local cooldown anchored to NOW (server corrects the duration in OnServerCooldown,
            // but keeps this same anchor so the bar is one continuous countdown — not a second cycle).
            float dur = _cdDuration.TryGetValue(abilityId, out var d) ? d : defaultCooldown;
            _castAt[abilityId] = Time.time;
            _readyAt[abilityId] = Time.time + dur;

            _net.SendCast(abilityId, 0);                     // 0 = action aim; server uses our facing
            if (animator != null) animator.TriggerAttack();  // predicted
            return true;
        }

        /// <summary>Server told us the real cooldown (on cast confirmation). Re-anchor to WHEN we cast, not now,
        /// so correcting the duration doesn't restart the bar (which looked like the cooldown playing twice).</summary>
        public void OnServerCooldown(byte abilityId, float seconds)
        {
            _cdDuration[abilityId] = seconds;
            float anchor = _castAt.TryGetValue(abilityId, out var ca) ? ca : Time.time;
            _readyAt[abilityId] = anchor + seconds;
        }

        /// <summary>Seconds left on this ability's cooldown (0 = ready).</summary>
        public float GetCooldownRemaining(byte abilityId)
            => _readyAt.TryGetValue(abilityId, out var t) ? Mathf.Max(0f, t - Time.time) : 0f;

        /// <summary>Cooldown progress 0..1 for a radial UI fill (1 = just cast, 0 = ready).</summary>
        public float GetCooldownRemaining01(byte abilityId)
        {
            float rem = GetCooldownRemaining(abilityId);
            if (rem <= 0f) return 0f;
            float dur = _cdDuration.TryGetValue(abilityId, out var d) && d > 0f ? d : defaultCooldown;
            return Mathf.Clamp01(rem / dur);
        }

        private static bool IsPointerOverUI()
            => EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }
}
