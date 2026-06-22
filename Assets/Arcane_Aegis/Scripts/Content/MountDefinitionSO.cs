using UnityEngine;
using ArcaneShared.Enums;

namespace Arcane_Aegis.Content
{
    /// <summary>A MOUNT (travel mount, speed only) as a ScriptableObject: gameplay (synced) + client art (model/icon, NOT
    /// synced). SEPARATE from MonsterDefinition. Create via Assets ▸ Create ▸ ArcaneMMO ▸ Mount Definition.</summary>
    [CreateAssetMenu(fileName = "Mount_", menuName = "ArcaneMMO/Mount Definition")]
    public class MountDefinitionSO : ScriptableObject
    {
        [Header("Gameplay — synced to the server")]
        [Tooltip("Id estável (ex.: 'brown_horse'). Referenciado pela posse do personagem.")] public string id;
        public string displayName;
        public ItemRarity rarity = ItemRarity.Common;
        [Tooltip("Tema/afinidade (cosmético por ora).")] public ElementType element = ElementType.None;
        [Tooltip("Multiplicador de velocidade ao montar (ex.: 1.6 = +60%).")] public float speedMult = 1.6f;
        [Tooltip("Tempo (s) pra montar (0 = instantâneo).")] public float mountTimeSeconds = 1f;

        [Header("Client art — NOT synced (resolvido por id no client)")]
        public Sprite icon;
        [Tooltip("Prefab COMPLETO da montaria (raiz com KinematicCharacterMotor + MountController + collider + filho 'RiderSeat' + 'Target' p/ câmera + o modelo). Usado tanto pelo dono (controlável) quanto por remotos (interpolado).")]
        public GameObject mountPrefab;
        [TextArea] public string description;
    }
}
