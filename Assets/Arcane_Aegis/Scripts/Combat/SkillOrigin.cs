using UnityEngine;
using Arcane_Aegis.Content;

namespace Arcane_Aegis.Combat
{
    /// <summary>
    /// THE single, standard spawn point for a skill's presentation (cast VFX + projectile). The base is the EQUIPPED
    /// WEAPON's tip — a child named "Muzzle" on each weapon prefab (so every staff/bow/wand spawns from its OWN tip,
    /// resolved live from the hierarchy as gear is swapped). Lookup order: weapon "Muzzle" → the hand socket
    /// "Socket_MainHand" → a fixed "CastOrigin" on the rig → a chest-height point in front. On top of the base, each
    /// skill may add a local offset (x = right, y = up, z = forward, in the caster's facing) via
    /// <see cref="SkillDefinitionSO.spawnOffset"/>. One place so cast + projectile always agree.
    /// </summary>
    public static class SkillOrigin
    {
        // Priority: the equipped weapon's tip ("Muzzle", varies per weapon) → a fixed rig point ("CastOrigin").
        // NOT the hand socket — the hand is off to the SIDE of the body, so spawning there reads as "comes out the
        // side". With neither present we fall back to a CENTERED chest-front point (looks natural for any weapon).
        private static readonly string[] MuzzleNames = { "Muzzle", "CastOrigin" };

        /// <summary>World spawn point for the given caster + explicit local offset.</summary>
        public static Vector3 Resolve(Transform caster, Vector3 localOffset)
        {
            if (caster == null) return Vector3.zero;
            Vector3 fwd = caster.forward; fwd.y = 0f;
            fwd = fwd.sqrMagnitude > 1e-4f ? fwd.normalized : caster.forward;

            Transform muzzle = null;
            for (int i = 0; i < MuzzleNames.Length && muzzle == null; i++) muzzle = FindDeep(caster, MuzzleNames[i]);
            Vector3 basePos = muzzle != null ? muzzle.position
                                             : caster.position + fwd * 0.6f + Vector3.up * 1.1f; // fallback: chest, in front

            if (localOffset == Vector3.zero) return basePos;
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            return basePos + right * localOffset.x + Vector3.up * localOffset.y + fwd * localOffset.z;
        }

        /// <summary>World spawn point resolving the per-skill offset from the ContentLibrary by ability id.</summary>
        public static Vector3 ResolveFor(Transform caster, int abilityId)
        {
            var so = ContentLibrary.Active != null ? ContentLibrary.Active.GetSkill(abilityId) : null;
            return Resolve(caster, so != null ? so.spawnOffset : Vector3.zero);
        }

        private static Transform FindDeep(Transform parent, string name)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform c = parent.GetChild(i);
                if (c.name == name) return c;
                Transform r = FindDeep(c, name);
                if (r != null) return r;
            }
            return null;
        }
    }
}
