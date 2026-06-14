using System.Collections.Generic;
using UnityEngine;
using ArcaneShared.Enums;

namespace Arcane_Aegis.Content
{
    /// <summary>A monster as a ScriptableObject: gameplay (synced to content.db) + client art. Mirrors the server's
    /// MonsterDefinition / MonsterRecord. Stats + behavior + XP + a loot table (items dropped on death).
    /// Create via Assets ▸ Create ▸ ArcaneMMO ▸ Monster Definition.</summary>
    [CreateAssetMenu(fileName = "Monster_", menuName = "ArcaneMMO/Monster Definition")]
    public class MonsterDefinitionSO : ScriptableObject
    {
        [System.Serializable]
        public struct LootEntry
        {
            [Tooltip("Item que pode dropar.")] public ItemDefinitionSO item;
            [Tooltip("Chance de dropar (0–100%). LUK do matador dá um bônus pequeno.")] public int chancePct;
            [Tooltip("Quantidade mínima/máxima (inclusivo).")] public int min, max;
        }

        [Header("Gameplay — synced to the server")]
        public string id;
        public string displayName;
        public int level = 1;
        [Tooltip("Hostil = agride sozinho · Neutro = só revida se apanhar · Passivo = não luta.")]
        public CreatureDisposition disposition = CreatureDisposition.Hostile;
        [Tooltip("Família (rótulo p/ loot/resistência/UI no futuro).")]
        public CreatureKind kind = CreatureKind.Humanoid;
        [Tooltip("Vida exata. 0 = calcula pela fórmula (100 + VIT×10 + Nível×5).")]
        public int maxHp = 0;
        [Space] public int str = 4, dex = 4, intel = 1, vit = 5, spi = 3, luk = 3, armor = 10;
        public ElementType element = ElementType.None;
        [Space]
        [Tooltip("Nota inimigos neste raio (m).")] public float aggroRadius = 10f;
        [Tooltip("Desiste e volta pra casa se arrastado a este raio (m).")] public float leashRadius = 22f;
        [Tooltip("Alcance pra atacar (m).")] public float attackRange = 2.5f;
        public float moveSpeed = 3.5f;
        [Tooltip("Skill usada pra atacar (id 1..255).")] public int attackAbilityId = 1;
        [Tooltip("XP concedido a quem matar.")] public int xpReward = 25;

        [Header("Loot")]
        public List<LootEntry> loot = new();

        [Header("Client art — NOT synced")]
        public Sprite icon;
        [TextArea] public string description;
        [Tooltip("Modelo 3D (futuro: aparência replicada).")] public GameObject model3D;
    }
}
