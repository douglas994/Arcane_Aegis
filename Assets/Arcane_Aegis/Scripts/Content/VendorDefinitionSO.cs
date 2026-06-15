using System.Collections.Generic;
using UnityEngine;

namespace Arcane_Aegis.Content
{
    /// <summary>A vendor (shop NPC) as a ScriptableObject (synced to content.db). <see cref="stock"/> is what it SELLS
    /// (each priced by the item's npcPrice + priceCurrency). Place it in the world with a SpawnMarker (vendor). Create via
    /// Assets ▸ Create ▸ ArcaneMMO ▸ Vendor.</summary>
    [CreateAssetMenu(fileName = "Vendor_", menuName = "ArcaneMMO/Vendor")]
    public class VendorDefinitionSO : ScriptableObject
    {
        [Header("Gameplay — synced to the server")]
        public string id;
        public string displayName;
        [Tooltip("Itens que o vendedor VENDE (a venda PRA ele aceita qualquer item vendável).")]
        public List<ItemDefinitionSO> stock = new();

        [Header("Client art — NOT synced")]
        [Tooltip("Modelo 3D do vendedor no mundo.")] public GameObject model3D;
        [TextArea] public string description;
    }
}
