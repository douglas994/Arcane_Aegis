using System.Collections.Generic;
using UnityEngine;
using ArcaneShared.Enums;

namespace Arcane_Aegis.Content
{
    /// <summary>An item template as a ScriptableObject: gameplay data (synced to the server) + client art
    /// (icon/model/description, NOT synced). Same pattern as RaceDefinitionSO. Mirrors the server's ItemTemplate
    /// (Rules §12.1). The classification/stat fields use the shared enums → the inspector renders them as DROPDOWNS
    /// (fast, no typos). Create via Assets ▸ Create ▸ ArcaneMMO ▸ Item Definition.</summary>
    [CreateAssetMenu(fileName = "Item_", menuName = "ArcaneMMO/Item Definition")]
    public class ItemDefinitionSO : ScriptableObject
    {
        /// <summary>Where an equipped model attaches on the character rig (bone + local offset). Client art.</summary>
        [System.Serializable] public struct AttachPoint
        {
            [Tooltip("Nome do osso/transform no modelo do personagem (ex.: 'RightHand', 'Spine2'). Vazio = não anexa.")]
            public string bone;
            public Vector3 position;
            public Vector3 euler;
            [Tooltip("(0,0,0) = escala 1,1,1.")] public Vector3 scale;
        }

        [System.Serializable] public struct Stat { public StatId statId; public int value; }
        [System.Serializable] public struct Roll { public StatId statId; public int min, max; }
        [System.Serializable] public struct Effect
        {
            public ConsumableEffectKind kind;
            [Tooltip("Only for BuffStat — which stat to boost.")] public StatId statId;
            [Tooltip("Flat amount, or a percent depending on the kind.")] public int amount;
            [Tooltip("Seconds the buff lasts. 0 = instant (restores).")] public int durationSeconds;
        }

        [Header("Gameplay — synced to the server")]
        public string id;
        public string displayName;
        public ItemType type = ItemType.Weapon;
        [Tooltip("Bag filter group, e.g. 'swords', 'ore'")]
        public string category = "";
        public EquipSlot slot = EquipSlot.None;
        [Tooltip("Two-handed weapon (MainHand) — also occupies the OffHand, blocking off-hand items.")]
        public bool twoHanded;
        public ItemRarity rarity = ItemRarity.Common;
        public ElementType element = ElementType.None;
        public int levelReq = 1;
        [Tooltip("empty = any class")] public string classReq = "";

        [Space]
        public List<Stat> statsBase = new();      // fixed bonuses (dropdown stat id)
        public List<Roll> rollsPossible = new();  // RNG roll pool (§10.2)
        public int maxRolls;

        [Tooltip("Consumable effects applied on use (heal / buff). Only used for Consumable items.")]
        public List<Effect> effects = new();

        [Space]
        public int tierMax = 5;
        public int enhanceMax = 12;
        public int socketsMax;
        public int durabilityMax = 100;
        public float weight;
        public bool sellable = true;
        public bool tradeable = true;
        public int npcPrice;
        public int stackMax = 1;

        [Header("Client art — NOT synced")]
        public Sprite icon;
        [TextArea] public string description;
        [Tooltip("World/equipped model (optional)")] public GameObject model3D;
        [Tooltip("Onde a arma fica EM USO (em combate) — normalmente a mão.")]
        public AttachPoint gripAttach;
        [Tooltip("Onde a arma fica GUARDADA fora de combate (ex.: costas). Bone vazio = sempre na mão.")]
        public AttachPoint sheathAttach;
    }
}
