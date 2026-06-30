using System.Collections.Generic;
using UnityEngine;

namespace Arcane_Aegis.Content
{
    /// <summary>A building piece (open-world construction) as a ScriptableObject: gameplay (synced to content.db) + client
    /// art. The player places it in build mode; it costs <see cref="ingredients"/> and the server spawns a persisted
    /// structure rendered from <see cref="prefab"/>. Create via Assets ▸ Create ▸ ArcaneMMO ▸ Building Piece.</summary>
    [CreateAssetMenu(fileName = "Building_", menuName = "ArcaneMMO/Building Piece")]
    public class BuildingDefinitionSO : ScriptableObject
    {
        [System.Serializable]
        public struct Ingredient
        {
            [Tooltip("Material consumido pra construir.")] public ItemDefinitionSO item;
            [Tooltip("Quantidade consumida.")] public int qty;
        }

        [Header("Gameplay — synced to the server")]
        public string id;
        public string displayName;
        [Tooltip("Integridade máxima (reservado pro decay; 0 = padrão). Não usado no MVP.")] public ushort maxIntegrity = 100;

        [Header("Custo (consumido ao construir)")]
        public List<Ingredient> ingredients = new();

        [Header("Client art")]
        [Tooltip("Modelo/prefab 3D da peça (o servidor replica como EntityType.Building; o cliente instancia isto). Também é o fantasma do modo construção.")]
        public GameObject prefab;
        public Sprite icon;
        [TextArea] public string description;
    }
}
