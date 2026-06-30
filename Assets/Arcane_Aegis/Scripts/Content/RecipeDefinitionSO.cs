using System.Collections.Generic;
using UnityEngine;
using ArcaneShared.Enums;

namespace Arcane_Aegis.Content
{
    /// <summary>A crafting recipe as a ScriptableObject: gameplay (synced to content.db) + client art. Mirrors the server's
    /// RecipeDefinition. The player crafts it with the right profession mastery: consumes the ingredients, produces the
    /// output + profession XP. Create via Assets ▸ Create ▸ ArcaneMMO ▸ Recipe.</summary>
    [CreateAssetMenu(fileName = "Recipe_", menuName = "ArcaneMMO/Recipe")]
    public class RecipeDefinitionSO : ScriptableObject
    {
        [System.Serializable]
        public struct Ingredient
        {
            [Tooltip("Material consumido.")] public ItemDefinitionSO item;
            [Tooltip("Quantidade consumida.")] public int qty;
        }

        [Header("Gameplay — synced to the server")]
        public string id;
        public string displayName;
        [Tooltip("Profissão de criação que esta receita treina.")] public Profession profession = Profession.Carpentry;
        [Tooltip("Nível mínimo da profissão pra criar (1 = qualquer um).")] public ushort requiredLevel = 1;
        [Tooltip("Segundos por criação (barra de progresso).")] public float craftSeconds = 2f;
        [Tooltip("XP de profissão por criação.")] public int xpReward = 10;
        [Tooltip("Cozinha: exige uma FOGUEIRA por perto pra criar (estação). Marque pra comidas cozidas.")] public bool requiresCampfire;

        [Header("Resultado")]
        [Tooltip("Item produzido.")] public ItemDefinitionSO output;
        [Tooltip("Quantos itens saem.")] public int outputQty = 1;

        [Header("Ingredientes (consumidos)")]
        public List<Ingredient> ingredients = new();

        [Header("Client art — NOT synced")]
        public Sprite icon;
        [TextArea] public string description;
    }
}
