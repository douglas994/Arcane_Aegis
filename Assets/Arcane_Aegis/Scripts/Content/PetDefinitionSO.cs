using System.Collections.Generic;
using UnityEngine;
using ArcaneShared.Enums;

namespace Arcane_Aegis.Content
{
    /// <summary>A PET (combat companion) as a ScriptableObject: gameplay (synced to content.db) + client art (model/icon,
    /// NOT synced). SEPARATE from MonsterDefinition. Has rarity + element (element is functional in combat). Create via
    /// Assets ▸ Create ▸ ArcaneMMO ▸ Pet Definition.</summary>
    [CreateAssetMenu(fileName = "Pet_", menuName = "ArcaneMMO/Pet Definition")]
    public class PetDefinitionSO : ScriptableObject
    {
        [Header("Gameplay — synced to the server")]
        [Tooltip("Id estável (ex.: 'wolf_pup'). Referenciado pela posse do personagem.")] public string id;
        public string displayName;
        public ItemRarity rarity = ItemRarity.Common;
        [Tooltip("Elemento do pet (funcional: entra na matriz elemental do combate).")] public ElementType element = ElementType.None;

        [Header("Stats")]
        public int level = 1;
        public int str = 4, dex = 4, intel = 1, vit = 5, spi = 3, luk = 3, armor = 8;
        [Tooltip("HP exato (0 = derivar de Vit + Level).")] public int maxHp = 0;

        [Header("Combate / comportamento")]
        public AiArchetype archetype = AiArchetype.Melee;
        [Tooltip("Skills que o pet usa (a IA escolhe entre elas).")] public List<SkillDefinitionSO> abilities = new();
        public float attackRange = 2.5f;
        public float moveSpeed = 4f;
        [Tooltip("Quão perto ele fica do dono ao seguir (m).")] public float followRange = 4f;

        [Header("Utilitário (enquanto o pet está fora)")]
        [Tooltip("Aura passiva: stat que o pet concede ao DONO (None = sem aura).")] public StatId auraStat = StatId.None;
        [Tooltip("Quanto do auraStat a aura concede.")] public int auraAmount = 0;
        [Tooltip("Bônus de % na chance de drop dos abates do dono (loot-find).")] public float lootFindPct = 0f;
        [Tooltip("Raio (m) em que o pet recolhe loot do chão automaticamente pro dono. 0 = sem vacuum.")] public float vacuumRange = 0f;

        [Header("Client art — NOT synced (resolvido por id no client)")]
        public Sprite icon;
        public GameObject model3D;
        [TextArea] public string description;
    }
}
