using UnityEngine;

namespace Arcane_Aegis.Content
{
    /// <summary>A currency type as a ScriptableObject (synced to content.db). The wallet holds an amount per currency id;
    /// item prices reference one by id. Create via Assets ▸ Create ▸ ArcaneMMO ▸ Currency.</summary>
    [CreateAssetMenu(fileName = "Currency_", menuName = "ArcaneMMO/Currency")]
    public class CurrencyDefinitionSO : ScriptableObject
    {
        [Header("Gameplay — synced to the server")]
        [Tooltip("Id estável referenciado pelo preço dos itens e pela carteira (ex.: 'gold').")] public string id;
        public string displayName;
        [Tooltip("Ordem de exibição na HUD (menor = primeiro).")] public int sortOrder;

        [Header("Client art — NOT synced (icon id only is synced)")]
        public Sprite icon;
    }
}
