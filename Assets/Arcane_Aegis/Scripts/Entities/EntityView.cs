using System.Collections.Generic;
using UnityEngine;
using ArcaneShared.Enums;
using ArcaneShared.Models;
using Arcane_Aegis.Controllers;

namespace Arcane_Aegis.Entities
{
    /// <summary>
    /// Base view for ANY networked entity (mirrors the server's Entity). Owns transform sync: smoothly
    /// follows the latest snapshot (SmoothDamp) and feeds the CharacterAnimator the state + a stable
    /// networked speed. Locally-controlled entities turn interpolation OFF (their own controller drives).
    /// </summary>
    public class EntityView : MonoBehaviour
    {
        public ushort Id;
        public EntityType Type;
        public string DisplayName = ""; // clean name from the spawn packet (e.g. "Bear") — for UI labels
        public string ContentId = "";   // replicated def id (monster/node/vendor) — resolves shop, etc.
        public Vector3 WorldOffset; // continent offset (grid × worldSize) → render server LOCAL coords in GLOBAL space
        public string EquippedMainHand = ""; // replicated main-hand template id (remotes) → WeaponVisual shows the model

        /// <summary>Latest snapshot HP fraction (0..1) for ANY entity — the quantized HpPercent/255. Drives the
        /// target frame + tab-target "is alive" check + enemy health bars, so it works even for a plain view.</summary>
        public float SnapHp01 { get; private set; } = 1f;

        private CombatantVitals _combatant; // optional: HP bar + death cue (added by EntityManager to combatants)
        private EntityAnimation _animHub;   // single hub for trigger animations (attack/cast/death/gather)

        /// <summary>Wired by the <see cref="CombatantVitals"/> component on its Awake (composition over inheritance):
        /// any view can host the combatant behaviour without being a "Humanoid".</summary>
        public void AttachCombatant(CombatantVitals combatant) => _combatant = combatant;

        [SerializeField] private float positionSmoothTime = 0.1f;
        [SerializeField] private float rotationLerp = 12f;
        [SerializeField] private float speedWindow = 0.25f;
        [SerializeField] protected CharacterAnimator characterAnimator;

        private Vector3 _targetPos;
        private float _targetYaw;
        private Vector3 _vel;                 // SmoothDamp state
        private bool _interpolate;            // OFF by default; remote players turn it on in Initialize
                                             // (so a mis-set local player is never dragged to the origin)
        private readonly Queue<(double time, Vector3 pos)> _hist = new();

        protected virtual void Awake()
        {
            if (characterAnimator == null) characterAnimator = GetComponentInChildren<CharacterAnimator>();
            _animHub = EntityAnimation.Of(gameObject);
        }

        /// <summary>Applies a snapshot. Base = transform target + anim state + speed. Subclasses extend (HP, …).</summary>
        public virtual void ApplySnapshot(in SnapshotEntry e)
        {
            SnapHp01 = e.HpPercent / 255f; // any view tracks its last-known HP fraction (used by targeting/UI)
            if (_combatant != null) _combatant.OnSnapshot(SnapHp01, e.State == MovementState.Dead); // HP bar + death cue
            SetTarget(new Vector3(e.Position.X, e.Position.Y, e.Position.Z), e.Yaw, e.State);
        }

        /// <summary>Local players drive their own transform (KCC), so interpolation is turned off for them.</summary>
        public void SetInterpolated(bool on) => _interpolate = on;

        /// <summary>
        /// Configures the view right after spawn. Base = REMOTE setup (snapshot interpolation + animation driven
        /// by the network). <see cref="PlayerView"/> overrides this to wire the LOCAL control stack when isLocal.
        /// </summary>
        public virtual void Spawn(bool isLocal)
        {
            SetInterpolated(!isLocal);
            if (characterAnimator != null) characterAnimator.UseNetworkSource();
        }

        /// <summary>Updates the follow target, animation state, and windowed networked speed.</summary>
        public void SetTarget(Vector3 position, float yaw, MovementState state)
        {
            position += WorldOffset; // server sends LOCAL coords; this view renders in GLOBAL (continent offset)
            _targetPos = position;
            _targetYaw = yaw;

            // Stable speed = straight-line distance across the window / its duration (cancels the
            // snapshot-vs-movement-rate beat that would otherwise flicker the blend tree).
            double now = Time.timeAsDouble;
            _hist.Enqueue((now, position));
            while (_hist.Count > 2 && now - _hist.Peek().time > speedWindow) _hist.Dequeue();

            if (characterAnimator != null)
            {
                if (_hist.Count >= 2)
                {
                    var oldest = _hist.Peek();
                    double dt = now - oldest.time;
                    if (dt > 1e-3)
                    {
                        Vector3 d = position - oldest.pos;
                        d.y = 0f;
                        characterAnimator.SourceSpeed = d.magnitude / (float)dt;
                    }
                }
                characterAnimator.State = state;
            }
        }

        /// <summary>Snaps instantly and clears history (on spawn).</summary>
        public void Teleport(Vector3 position, float yaw)
        {
            position += WorldOffset; // local → global
            _targetPos = position;
            _targetYaw = yaw;
            _vel = Vector3.zero;
            _hist.Clear();
            transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
        }

        /// <summary>Hard-snaps to the latest snapshot target (no interpolation slide). Used on respawn so a creature
        /// reviving back at its spawn doesn't visibly slide there from where it died. No-op for the local player.</summary>
        public void SnapToTarget()
        {
            if (!_interpolate) return; // local player is driven by its own controller + server correction
            _vel = Vector3.zero;
            _hist.Clear();
            transform.SetPositionAndRotation(_targetPos, Quaternion.Euler(0f, _targetYaw, 0f));
        }

        // Trigger animations route through the single EntityAnimation hub (driven by S2C_AbilityCast / gather packets).
        /// <summary>Plays the attack animation on this entity.</summary>
        public void PlayAttack() => _animHub.PlayAttack();

        /// <summary>Plays a SKILL's cast presentation (its own anim + cast VFX, or the generic attack fallback).</summary>
        public void PlayCast(int abilityId) => _animHub.PlayCast(abilityId);

        /// <summary>Holds the gather animation for <paramref name="profession"/> (the server ends it authoritatively;
        /// <paramref name="seconds"/> is only a safety net if that packet is lost).</summary>
        public void PlayGather(byte profession, float seconds) => _animHub.PlayGather(profession, seconds);

        /// <summary>Stops the gather animation now (the server's authoritative end, or a cancel/interrupt).</summary>
        public void StopGather() => _animHub.StopGather();

        private void Update()
        {
            if (!_interpolate) return;
            transform.position = Vector3.SmoothDamp(transform.position, _targetPos, ref _vel, positionSmoothTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(0f, _targetYaw, 0f), Time.deltaTime * rotationLerp);
        }
    }
}
