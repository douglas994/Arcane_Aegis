using UnityEngine;
using ArcaneShared.Enums;
using ArcaneShared.Models;

namespace Arcane_Aegis.Entities
{
    /// <summary>
    /// A living entity view (mirrors the server's Humanoid): adds the world-space health bar (driven by the
    /// snapshot HpPercent) and a death cue. Players, monsters and NPCs all derive from this.
    /// </summary>
    public class HumanoidView : EntityView
    {
        [SerializeField] private EntityVitals vitals; // world-space health bar (above the head)
        [SerializeField] private float deathHideDelay = 2f; // let the death anim play before the body hides

        // Exact vitals — mirrors the server's Humanoid. Filled for the OWN player from S2C_StateUpdate; remotes
        // only receive the quantized HP% in the snapshot (used by the world bar above their head).
        public int Hp { get; private set; }
        public int MaxHp { get; private set; }
        public int Mana { get; private set; }
        public int MaxMana { get; private set; }
        public float HpFraction => MaxHp > 0 ? (float)Hp / MaxHp : 0f;
        public float ManaFraction => MaxMana > 0 ? (float)Mana / MaxMana : 0f;

        // HP fraction usable for ANY entity: exact for the local player (Hp/MaxHp), or the snapshot's quantized
        // HpPercent for remotes/monsters (which never get SetVitals). Drives the floating enemy health bars.
        private float _snapHp01 = 1f;
        public float BarHpFraction => MaxHp > 0 ? HpFraction : _snapHp01;

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

        private Renderer[] _renderers;
        private bool _wasDead;

        protected override void Awake()
        {
            base.Awake();
            if (vitals == null) vitals = GetComponentInChildren<EntityVitals>();
            _renderers = GetComponentsInChildren<Renderer>(includeInactive: true);
        }

        public override void ApplySnapshot(in SnapshotEntry e)
        {
            base.ApplySnapshot(e);
            _snapHp01 = e.HpPercent / 255f;
            if (vitals != null) vitals.SetHp01(_snapHp01);

            bool dead = e.State == MovementState.Dead;
            if (dead != _wasDead)
            {
                _wasDead = dead;
                OnDeathChanged(dead);
            }
        }

        /// <summary>The local player hides its world bar — its HP is shown on the HUD instead.</summary>
        public void ShowWorldVitals(bool show)
        {
            if (vitals != null) vitals.SetVisible(show);
        }

        /// <summary>Death: play the death anim (Dead bool), then hide the body after a delay. Respawn: show now.</summary>
        private void OnDeathChanged(bool dead)
        {
            if (characterAnimator != null) characterAnimator.SetDead(dead);

            CancelInvoke(nameof(HideModel));
            if (dead) Invoke(nameof(HideModel), deathHideDelay); // let the death anim play, then hide
            else { SnapToTarget(); SetRenderers(true); }         // respawn → snap to spawn (no slide) + show
        }

        private void HideModel() => SetRenderers(false);

        private void SetRenderers(bool on)
        {
            if (_renderers == null) return;
            for (int i = 0; i < _renderers.Length; i++)
                if (_renderers[i] != null) _renderers[i].enabled = on;
        }
    }
}
