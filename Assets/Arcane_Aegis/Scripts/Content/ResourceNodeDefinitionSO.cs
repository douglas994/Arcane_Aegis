using System.Collections.Generic;
using UnityEngine;
using ArcaneShared.Enums;

namespace Arcane_Aegis.Content
{
    /// <summary>A harvestable resource node (tree/rock/herb) as a ScriptableObject: gameplay (synced to content.db) +
    /// client art. Mirrors the server's ResourceNodeDefinition. Players gather it with the right tool for materials +
    /// profession XP. Create via Assets ▸ Create ▸ ArcaneMMO ▸ Resource Node.</summary>
    [CreateAssetMenu(fileName = "Node_", menuName = "ArcaneMMO/Resource Node")]
    public class ResourceNodeDefinitionSO : ScriptableObject
    {
        [System.Serializable]
        public struct Yield
        {
            [Tooltip("Material que pode sair ao coletar.")] public ItemDefinitionSO item;
            [Tooltip("Chance (0–100%). LUK dá um bônus pequeno.")] public int chancePct;
            [Tooltip("Quantidade mín/máx (inclusivo).")] public int min, max;
        }

        [Header("Gameplay — synced to the server")]
        public string id;
        public string displayName;
        [Tooltip("Profissão que este nó treina.")] public Profession profession = Profession.Woodcutting;
        [Tooltip("Nível mínimo da profissão pra coletar (1 = qualquer um).")] public ushort requiredLevel = 1;
        [Tooltip("Categoria de ferramenta exigida (ex.: 'axe'). Vazio = mãos.")] public string requiredTool = "";
        [Tooltip("Segundos por coleta.")] public float gatherSeconds = 3f;
        [Tooltip("Quantas coletas antes de esgotar.")] public int charges = 5;
        [Tooltip("Segundos pra reaparecer depois de esgotar.")] public float respawnSeconds = 30f;
        [Tooltip("XP de profissão por coleta.")] public int xpReward = 15;

        [Header("Materiais (loot)")]
        public List<Yield> yields = new();

        [Header("Client art — NOT synced")]
        public Sprite icon;
        [TextArea] public string description;
        [Tooltip("Modelo 3D do nó (árvore/pedra).")] public GameObject model3D;
    }
}
