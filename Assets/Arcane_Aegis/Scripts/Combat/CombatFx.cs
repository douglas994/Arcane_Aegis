using System.Collections;
using UnityEngine;
using ArcaneShared.Enums;
using Arcane_Aegis.Content;
using Arcane_Aegis.Controllers;

namespace Arcane_Aegis.Combat
{
    /// <summary>
    /// Plays a skill's presentation: the right animation on cast (the SkillDefinitionSO's animTrigger, falling back to
    /// the generic attack), a cast VFX on the caster, and an impact VFX on a hit. Auto-creates itself (no scene setup)
    /// and resolves skills via ContentLibrary.Active. If a skill has no authored VFX prefab, a built-in flash plays so
    /// there's ALWAYS visible feedback. Server-authoritative gameplay is untouched — this is purely cosmetic.
    /// </summary>
    public sealed class CombatFx : MonoBehaviour
    {
        public static CombatFx Instance { get; private set; }

        [SerializeField] private ContentLibrary library; // optional; falls back to ContentLibrary.Active

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            var go = new GameObject("CombatFx");
            DontDestroyOnLoad(go);
            go.AddComponent<CombatFx>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }
        private void OnDestroy() { if (Instance == this) Instance = null; }

        private ContentLibrary Lib => library != null ? library : ContentLibrary.Active;

        /// <summary>On cast: play the skill's animation on the caster and spawn its cast VFX (or a default flash).</summary>
        public void PlayCast(Transform caster, CharacterAnimator anim, int abilityId)
        {
            SkillDefinitionSO so = Lib != null ? Lib.GetSkill(abilityId) : null;
            if (anim != null) anim.TriggerNamed(so != null ? so.animTrigger : null);
            if (caster == null) return;

            // Cast origin is INDEPENDENT of the projectile: its own castOffset + castVfxEuler (a body aura sits on the
            // caster while the projectile leaves the staff tip — tuning one must not move the other).
            Vector3 castPos = SkillOrigin.Resolve(caster, so != null ? so.castOffset : Vector3.zero);

            if (so != null) Audio.AudioManager.Instance?.PlaySfx3D(so.castSfx, castPos, so.sfxVolume);

            if (so != null && so.castVfx != null)
            {
                var go = Instantiate(so.castVfx, castPos, caster.rotation * Quaternion.Euler(so.castVfxEuler));
                if (so.castVfxFollows) go.transform.SetParent(caster, worldPositionStays: true);
                Destroy(go, so.vfxLifetime > 0f ? so.vfxLifetime : 2f);
            }
            else if (so != null && so.castTime > 0f) // built-in fallback for spells (skip instant basic attacks → no per-swing noise)
            {
                FxFlash.Spawn(castPos, ElementColor(so.element), 0.9f, 0.3f);
            }

            // NOTE: the SLASH arc is fired at the swing's APEX by EntityAnimation — either from an Animation Event on the
            // attack clip (frame-accurate, the pro way) or, as a fallback, after the skill's releaseTime. Both route to
            // <see cref="SpawnSlash"/> below, guarded so it fires once.
        }

        /// <summary>Spawns the skill's authored slash arc in FRONT of the caster, facing the swing (+ the Euler knob).
        /// Called at the swing's apex (anim event / releaseTime). Strips a pack prefab's physics so it doesn't fight us.</summary>
        public void SpawnSlash(Transform caster, int abilityId)
        {
            SkillDefinitionSO so = Lib != null ? Lib.GetSkill(abilityId) : null;
            if (so == null || caster == null) return;
            Vector3 fwd = caster.forward; fwd.y = 0f; fwd = fwd.sqrMagnitude > 1e-4f ? fwd.normalized : caster.forward;
            Vector3 pos = SkillOrigin.Resolve(caster, so.spawnOffset);
            Audio.AudioManager.Instance?.PlaySfx3D(so.slashSfx, pos, so.sfxVolume); // swing whoosh, even if there's no slash prefab
            if (so.slashVfx == null) return;
            var slash = Instantiate(so.slashVfx, pos, Quaternion.LookRotation(fwd) * Quaternion.Euler(so.slashVfxEuler));
            foreach (var col in slash.GetComponentsInChildren<Collider>(true)) Destroy(col);
            foreach (var rb in slash.GetComponentsInChildren<Rigidbody>(true)) Destroy(rb);
            Destroy(slash, so.vfxLifetime > 0f ? so.vfxLifetime : 1.5f);
        }

        /// <summary>The AoE's area VFX (explosion/cone), once, at the caster facing forward — fired at the moment the
        /// effect resolves (after the wind-up). Only for skills that authored an areaVfx.</summary>
        public void PlayArea(Transform caster, int abilityId, float delaySeconds)
        {
            SkillDefinitionSO so = Lib != null ? Lib.GetSkill(abilityId) : null;
            if (so == null || so.areaVfx == null || caster == null) return;
            StartCoroutine(AreaRoutine(caster, so, delaySeconds));
        }

        private IEnumerator AreaRoutine(Transform caster, SkillDefinitionSO so, float delay)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            if (caster == null) yield break;
            var go = Instantiate(so.areaVfx, caster.position, caster.rotation);
            Destroy(go, so.vfxLifetime > 0f ? so.vfxLifetime : 2f);
        }

        /// <summary>On a hit: spawn the skill's impact VFX at the target (or a default flash). Skipped for projectile
        /// skills — the ProjectileManager already shows the impact at the projectile's exact hit point.</summary>
        public void SpawnImpact(int abilityId, Vector3 pos)
        {
            SkillDefinitionSO so = Lib != null ? Lib.GetSkill(abilityId) : null;
            if (so != null) Audio.AudioManager.Instance?.PlaySfx3D(so.impactSfx, pos, so.sfxVolume);
            // Projectile skills ALSO show their impact here now — at the REAL target position (from the combat event),
            // which is accurate. The ProjectileManager no longer spawns a hit impact (it was laggy → landed near the
            // caster), so this is the single source of the hit VFX for both melee and projectiles.
            if (so != null && so.impactVfx != null)
            {
                var go = Instantiate(so.impactVfx, pos, Quaternion.identity);
                Destroy(go, so.vfxLifetime > 0f ? so.vfxLifetime : 2f);
            }
            else // built-in fallback hit flash (works even with no authored VFX / unknown ability)
            {
                FxFlash.Spawn(pos, so != null ? ElementColor(so.element) : new Color(1f, 0.85f, 0.4f, 0.9f), 1.1f, 0.3f);
            }
        }

        private static Color ElementColor(ElementType e) => e switch
        {
            ElementType.Fire  => new Color(1f, 0.45f, 0.2f, 0.9f),
            ElementType.Water => new Color(0.3f, 0.6f, 1f, 0.9f),
            ElementType.Wind  => new Color(0.6f, 1f, 0.6f, 0.9f),
            ElementType.Earth => new Color(0.8f, 0.6f, 0.3f, 0.9f),
            ElementType.Light => new Color(1f, 0.95f, 0.6f, 0.9f),
            ElementType.Dark  => new Color(0.7f, 0.4f, 0.9f, 0.9f),
            _                 => new Color(1f, 0.85f, 0.4f, 0.9f),
        };
    }
}
