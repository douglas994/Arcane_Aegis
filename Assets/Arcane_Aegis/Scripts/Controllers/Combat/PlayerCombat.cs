using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using Arcane_Aegis.Network;
using Arcane_Aegis.UI;
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
        [SerializeField] private Arcane_Aegis.Combat.TargetingSystem targeting; // current target id (0 = action aim)

        private Arcane_Aegis.Entities.EntityAnimation _anim; // trigger-animation hub (predicted cast presentation)

        private NetClient _net;
        private Arcane_Aegis.Entities.PlayerView _self;               // own view → current mana + entity id
        private bool _stunned;                                        // server CC: blocks casting (no prediction)
        private readonly Dictionary<byte, float> _readyAt = new();    // ability id → Time.time when castable again
        private readonly Dictionary<byte, float> _cdDuration = new(); // ability id → last-known cooldown (seconds)
        private readonly Dictionary<byte, float> _castAt = new();     // ability id → Time.time of the last cast (anchor)

        private void Start()
        {
            if (input == null) input = GetComponent<PlayerInput>();
            _anim = Arcane_Aegis.Entities.EntityAnimation.Of(gameObject);
            if (targeting == null) targeting = FindAnyObjectByType<Arcane_Aegis.Combat.TargetingSystem>();
            _self = GetComponent<Arcane_Aegis.Entities.PlayerView>();
            _net = NetClient.Instance ?? FindAnyObjectByType<NetClient>();
        }

        private void Update()
        {
            if (_net == null || UiFocus.IsTyping) return; // typing in chat → no casting

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
            if (_stunned) return false; // server rejects casts while stunned — don't predict
            if (GetCooldownRemaining(abilityId) > 0f) return false;

            // Mana gate: only predict what the server will accept (else you swing with no effect AND the weapon
            // never draws, since the rejected cast never marks you in-combat). Cost comes from the authored skill.
            var so = Arcane_Aegis.Content.ContentLibrary.Active != null ? Arcane_Aegis.Content.ContentLibrary.Active.GetSkill(abilityId) : null;
            int cost = so != null ? so.cost : 0;
            if (cost > 0 && _self != null && _self.Mana < cost)
            {
                if (Arcane_Aegis.UI.Toast.Instance != null) Arcane_Aegis.UI.Toast.Instance.Show("Sem mana.");
                return false;
            }

            // optimistic local cooldown anchored to NOW (server corrects the duration in OnServerCooldown,
            // but keeps this same anchor so the bar is one continuous countdown — not a second cycle).
            float dur = _cdDuration.TryGetValue(abilityId, out var d) ? d : defaultCooldown;
            _castAt[abilityId] = Time.time;
            _readyAt[abilityId] = Time.time + dur;

            ushort targetId = targeting != null ? targeting.CurrentTargetId : (ushort)0;
            // GroundAoE skills carry a ground aim point (cursor → ground); other modes use facing/target.
            Vector3 aim = (so != null && so.targeting == ArcaneShared.Enums.TargetingMode.GroundAoE) ? GroundAimPoint() : default;
            _net.SendCast(abilityId, targetId, aim);         // locked/aimed target id (0 = action aim → server uses our facing)
            if (_self != null) Arcane_Aegis.Combat.CombatStance.Mark(_self.Id); // draw the weapon on the swing (instant, no round-trip wait)
            _anim.PlayCast(abilityId); // predicted presentation: skill anim + cast VFX (or generic attack fallback)
            return true;
        }

        /// <summary>Server CC replication: while stunned, casting is blocked (matches the server's rejection).</summary>
        public void SetStunned(bool stunned) => _stunned = stunned;

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

        /// <summary>Ground point under the cursor for a GroundAoE cast (raycast → physics hit, else the caster's ground
        /// plane). World coords — the server validates the placement distance. (A persistent reticle/preview is optional polish.)</summary>
        private Vector3 GroundAimPoint()
        {
            var cam = Camera.main; var mouse = Mouse.current;
            if (cam != null && mouse != null)
            {
                Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());
                if (Physics.Raycast(ray, out var hit, 200f)) return hit.point;
                float y = transform.position.y; // fallback: intersect the caster's ground plane
                if (Mathf.Abs(ray.direction.y) > 1e-3f)
                {
                    float t = (y - ray.origin.y) / ray.direction.y;
                    if (t > 0f) return ray.origin + ray.direction * t;
                }
            }
            return transform.position;
        }

        private static bool IsPointerOverUI()
            => EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }
}
