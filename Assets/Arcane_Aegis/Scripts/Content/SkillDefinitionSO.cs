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
        [Tooltip("Lado do alvo: Enemy = dano/CC (mira inimigos) · Ally = cura/buff/escudo (mira aliados, pode incluir você).")]
        public TargetSide targetSide = TargetSide.Enemy;
        [Tooltip("Cone/Line length, Circle radius, Single max distance (m).")] public float range = 3.5f;
        [Tooltip("Cone full width (degrees).")] public float coneAngle = 90f;
        [Tooltip("Line width (m).")] public float width = 2f;
        public ElementType element = ElementType.None;
        [Tooltip("Arma exigida pra castar: vazio = nenhuma, 'any' = qualquer arma, ou a Category de arma (ex.: 'swords').")]
        public string requiredWeapon = "";
        [Tooltip("Se ligado, tomar dano durante o wind-up NÃO cancela o cast (ex.: skills 'channeling' fortes/instantâneas não se importam).")]
        public bool uninterruptible = false;

        [Space]
        public List<Effect> effects = new();

        [Header("Client art — NOT synced")]
        public Sprite icon;
        [TextArea] public string description;
        [Tooltip("Optional Animator trigger to play on cast (e.g. 'CastSpell', 'Shoot'). Empty = default attack.")]
        public string animTrigger = "";
        [Tooltip("VFX prefab that flies for a Projectile skill (else a default sphere is shown).")]
        public GameObject projectileVfx;
        [Tooltip("Muzzle/clarão CURTO que nasce no ponto de saída quando o projétil é lançado (ex.: fogo na ponta do " +
                 "cajado). Fica parado no muzzle enquanto a bola voa. Vazio = sem muzzle.")]
        public GameObject muzzleVfx;

        [Tooltip("Slash VFX (arco de espada autorado, ex.: do Vefects) que nasce na FRENTE do caster no golpe. " +
                 "Alternativa/complemento ao trail procedural. Vazio = sem arco.")]
        public GameObject slashVfx;
        [Tooltip("Atraso (s) até o slash/VFX de golpe sair, contado do início da animação — sincronize com o AUGE do " +
                 "swing (quando a lâmina passa). 0 = imediato. Ex.: ~0.15–0.25 num corte de espada. (Ignorado se 'Use Anim Event'.)")]
        public float releaseTime = 0.15f;
        [Tooltip("Frame-exato: dispara o slash por um Animation Event 'Release' no clip de ataque (em vez do releaseTime). " +
                 "Ligue depois de adicionar o evento no clip. Precisa do componente CombatAnimEvents no modelo.")]
        public bool useAnimEvent;
        [Tooltip("Rotação (graus) aplicada ao slash VFX — ajuste até o arco apontar/alinhar com o golpe (+Z). " +
                 "Packs montam em eixos variados: tente Y/X = 90 ou -90. (0,0,0) = sem ajuste.")]
        public Vector3 slashVfxEuler;

        [Header("Trail desta skill (sobrescreve o da arma)")]
        [Tooltip("Liga o trail próprio desta skill (ex.: corte de fogo = vermelho). Desligado = usa o trail padrão da arma.")]
        public bool overrideTrail;
        [Tooltip("Material do trail só pra esta skill (vazio = mantém o material da arma).")]
        public Material trailMaterial;
        [Tooltip("Cor/gradiente do trail desta skill (aplicado quando 'Override Trail' está ligado).")]
        public Gradient trailColor = new Gradient();
        [Tooltip("Rotação (graus) aplicada ao VFX do projétil — ajuste até a chama apontar pro sentido do voo (+Z). " +
                 "Muitos packs montam o efeito no eixo X: tente Y = 90 ou -90. (0,0,0) = sem ajuste.")]
        public Vector3 projectileVfxEuler;
        [Tooltip("Origem da skill (cast VFX + projétil), LOCAL ao caster: x=direita, y=cima, z=frente. Somado ao " +
                 "'Muzzle' da ARMA equipada (ou à mão/peito se não houver). (0,0,0) = sai direto do Muzzle da arma.")]
        public Vector3 spawnOffset;
        [Tooltip("VFX prefab spawned where a hit lands (projectile impact OR melee/AoE hit on the target).")]
        public GameObject impactVfx;
        [Tooltip("VFX da ÁREA do AoE (ex.: explosão no círculo, cone de fogo) — nasce no conjurador, virado pra frente, no momento do efeito. Vazio = nenhum.")]
        public GameObject areaVfx;
        [Tooltip("VFX prefab spawned on the CASTER when the skill is cast (glow, swirl…). Vazio = nenhum.")]
        public GameObject castVfx;
        [Tooltip("Origem do CAST VFX, LOCAL ao caster — SEPARADA do projétil: x=direita, y=cima, z=frente. " +
                 "(0,0,0) = mesmo ponto do Muzzle/peito.")]
        public Vector3 castOffset;
        [Tooltip("Rotação (graus) do CAST VFX, relativa à frente do caster — separada do projétil. (0,0,0) = sem ajuste.")]
        public Vector3 castVfxEuler;
        [Tooltip("Se ligado, o VFX de cast segue o conjurador (filho); senão fica parado onde nasceu.")]
        public bool castVfxFollows = true;
        [Tooltip("Segundos até os VFX (cast/impacto) se autodestruírem.")]
        public float vfxLifetime = 2f;

        [Header("Áudio (client art — NOT synced)")]
        [Tooltip("Som ao conjurar/lançar a skill (no conjurador). Vazio = sem som.")]
        public AudioClip castSfx;
        [Tooltip("Som do golpe/arco de espada (no auge do swing, junto do slash). Vazio = sem som.")]
        public AudioClip slashSfx;
        [Tooltip("Som de impacto onde o golpe acerta (melee/projétil). Vazio = sem som.")]
        public AudioClip impactSfx;
        [Tooltip("Volume relativo dos sons desta skill (0..1).")]
        [Range(0f, 1f)] public float sfxVolume = 1f;
    }
}
