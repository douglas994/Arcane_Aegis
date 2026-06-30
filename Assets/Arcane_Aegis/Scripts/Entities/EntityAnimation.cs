using System.Collections;
using UnityEngine;
using Arcane_Aegis.Controllers;
using Arcane_Aegis.Combat;

namespace Arcane_Aegis.Entities
{
    /// <summary>
    /// The single hub for an entity's TRIGGER animations — attack, skill cast, death, gather. Centralizes what used
    /// to be scattered across EntityView, PlayerCombat and the vitals, and the duplicated CombatFx-vs-Animator cast
    /// fallback now lives in ONE place. Wraps the low-level <see cref="CharacterAnimator"/>; the continuous locomotion
    /// speed/state still flows through CharacterAnimator directly from the view (that path is already centralized).
    /// One per entity GameObject — use <see cref="Of"/> so callers don't depend on Awake ordering.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EntityAnimation : MonoBehaviour
    {
        private CharacterAnimator _anim;
        // Lazy so it resolves correctly whether the animator is on the prefab or a model mounted after Awake.
        private CharacterAnimator Anim => _anim != null ? _anim : (_anim = GetComponentInChildren<CharacterAnimator>(true));
        private Coroutine _gather;

        /// <summary>Get-or-add the hub on a GameObject (order-independent — no dependency on Awake timing).</summary>
        public static EntityAnimation Of(GameObject go)
            => go.GetComponent<EntityAnimation>() ?? go.AddComponent<EntityAnimation>();

        private Arcane_Aegis.Combat.WeaponTrail _trail;

        /// <summary>Flash the equipped weapon's swing trail (re-found each time — weapons swap). A skill (abilityId > 0)
        /// can override the trail's style (material/colour) so each skill trails differently on the same blade; a generic
        /// swing (abilityId 0) uses the weapon's own look. No-op if the weapon has no WeaponTrail.</summary>
        private void FlashTrail(int abilityId = 0)
        {
            if (_trail == null) _trail = GetComponentInChildren<Arcane_Aegis.Combat.WeaponTrail>(true);
            if (_trail == null) return;
            var so = abilityId > 0 && Arcane_Aegis.Content.ContentLibrary.Active != null
                ? Arcane_Aegis.Content.ContentLibrary.Active.GetSkill(abilityId) : null;
            if (so != null && so.overrideTrail) _trail.Flash(-1f, so.trailMaterial, so.trailColor);
            else _trail.Flash();
        }

        /// <summary>Generic attack swing.</summary>
        public void PlayAttack()
        {
            if (Anim != null) Anim.TriggerAttack();
            FlashTrail();
        }

        /// <summary>A brief flinch when this entity takes damage (no-op if the controller has no "Hit" trigger).</summary>
        public void PlayHit()
        {
            if (Anim != null) Anim.TriggerHit();
        }

        /// <summary>A skill's cast presentation (its own anim + cast VFX via CombatFx); falls back to the generic attack.</summary>
        public void PlayCast(int abilityId)
        {
            if (CombatFx.Instance != null) CombatFx.Instance.PlayCast(transform, Anim, abilityId);
            else if (Anim != null) Anim.TriggerAttack();
            FlashTrail(abilityId);
            ScheduleSlash(abilityId);
        }

        private int _pendingSkill;
        private bool _slashConsumed;

        /// <summary>Times the skill's slash arc: a frame-exact Animation Event ("Release" → <see cref="FireSlashNow"/>)
        /// when the skill has <c>useAnimEvent</c>, else a fallback at its <c>releaseTime</c>. Fires once either way.</summary>
        private void ScheduleSlash(int abilityId)
        {
            _pendingSkill = abilityId;
            _slashConsumed = false;
            var so = Arcane_Aegis.Content.ContentLibrary.Active != null
                ? Arcane_Aegis.Content.ContentLibrary.Active.GetSkill(abilityId) : null;
            if (so == null || so.slashVfx == null) return; // nothing to fire
            if (so.useAnimEvent) return;                   // the clip's Animation Event fires it (frame-accurate)
            if (so.releaseTime > 0f) StartCoroutine(FireAfter(so.releaseTime));
            else FireSlashNow();
        }

        /// <summary>Spawn the pending skill's slash arc now (called by the Animation Event or the releaseTime fallback).
        /// Guarded so the arc spawns exactly once per cast.</summary>
        public void FireSlashNow()
        {
            if (_slashConsumed) return;
            _slashConsumed = true;
            if (CombatFx.Instance != null) CombatFx.Instance.SpawnSlash(transform, _pendingSkill);
        }

        private IEnumerator FireAfter(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            FireSlashNow();
        }

        /// <summary>Death / respawn pose.</summary>
        public void SetDead(bool dead)
        {
            if (Anim != null) Anim.SetDead(dead);
        }

        /// <summary>Sitting pose while riding a mount (set on mount, cleared on dismount — local + remote riders).</summary>
        public void SetMounted(bool mounted)
        {
            if (Anim != null) Anim.SetMounted(mounted);
        }

        /// <summary>Hold the gather loop for a profession (0 chop / 1 mine / …). The server ends it authoritatively;
        /// the <paramref name="seconds"/> timer is only a safety net if that packet is lost.</summary>
        public void PlayGather(byte profession, float seconds)
        {
            if (Anim == null) return;
            if (_gather != null) StopCoroutine(_gather);
            Anim.SetGather(true, profession);
            _gather = seconds > 0f ? StartCoroutine(EndAfter(seconds + 2f)) : null;
        }

        /// <summary>Stop the gather loop now (the server's authoritative end, or a cancel/interrupt).</summary>
        public void StopGather()
        {
            if (_gather != null) { StopCoroutine(_gather); _gather = null; }
            if (Anim != null) Anim.SetGather(false);
        }

        private IEnumerator EndAfter(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            if (Anim != null) Anim.SetGather(false);
            _gather = null;
        }
    }
}
