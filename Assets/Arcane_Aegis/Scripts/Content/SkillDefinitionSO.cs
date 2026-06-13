using System.Collections.Generic;
using UnityEngine;
using ArcaneShared.Enums;

namespace Arcane_Aegis.Content
{
    /// <summary>A skill/ability as a ScriptableObject: gameplay (synced to content.db) + client art (icon/description,
    /// NOT synced). Mirrors the server's AbilityRecord (Rules §7). A skill = TARGETING + a list of EFFECTS. The id is a
    /// byte (1..255) used by the cast packet. Create via Assets ▸ Create ▸ ArcaneMMO ▸ Skill Definition.</summary>
    [CreateAssetMenu(fileName = "Skill_", menuName = "ArcaneMMO/Skill Definition")]
    public class SkillDefinitionSO : ScriptableObject
    {
        [System.Serializable]
        public struct Effect
        {
            public AbilityEffectType type;
            [Tooltip("Flat amount: damage/heal base, knockback/dash meters, shield points, etc.")] public int amount;
            [Tooltip("For Damage: which attack power scales it.")] public ScalingAttribute scalesWith;
            [Tooltip("For ApplyStatus: the status id to apply (or the summon id).")] public string statusId;
        }

        [Header("Gameplay — synced to the server")]
        [Tooltip("Unique id 1..255 (used by the cast packet). Keep stable.")] public int id = 1;
        public string displayName;
        [Tooltip("Seconds of wind-up before the effect fires (0 = instant). >0 telegraphs the attack.")] public float castTime;
        public float cooldown = 1f;
        [Tooltip("Mana cost.")] public int cost;
        public TargetingMode targeting = TargetingMode.Single;
        [Tooltip("Cone/Line length, Circle radius, Single max distance (m).")] public float range = 3.5f;
        [Tooltip("Cone full width (degrees).")] public float coneAngle = 90f;
        [Tooltip("Line width (m).")] public float width = 2f;
        public ElementType element = ElementType.None;
        [Tooltip("Arma exigida pra castar: vazio = nenhuma, 'any' = qualquer arma, ou a Category de arma (ex.: 'swords').")]
        public string requiredWeapon = "";

        [Space]
        public List<Effect> effects = new();

        [Header("Client art — NOT synced")]
        public Sprite icon;
        [TextArea] public string description;
        [Tooltip("Optional Animator trigger to play on cast (e.g. 'CastSpell', 'Shoot'). Empty = default attack.")]
        public string animTrigger = "";
        [Tooltip("VFX prefab that flies for a Projectile skill (else a default sphere is shown).")]
        public GameObject projectileVfx;
        [Tooltip("VFX prefab spawned where a hit lands (projectile impact OR melee/AoE hit on the target).")]
        public GameObject impactVfx;
        [Tooltip("VFX da ÁREA do AoE (ex.: explosão no círculo, cone de fogo) — nasce no conjurador, virado pra frente, no momento do efeito. Vazio = nenhum.")]
        public GameObject areaVfx;
        [Tooltip("VFX prefab spawned on the CASTER when the skill is cast (glow, swirl…). Vazio = nenhum.")]
        public GameObject castVfx;
        [Tooltip("Se ligado, o VFX de cast segue o conjurador (filho); senão fica parado onde nasceu.")]
        public bool castVfxFollows = true;
        [Tooltip("Segundos até os VFX (cast/impacto) se autodestruírem.")]
        public float vfxLifetime = 2f;
    }
}
