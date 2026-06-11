using UnityEngine;

namespace Arcane_Aegis.Content
{
    /// <summary>A race as a ScriptableObject: gameplay data (synced) + client art (model/icon/desc).
    /// Create via Assets ▸ Create ▸ ArcaneMMO ▸ Race Definition.</summary>
    [CreateAssetMenu(fileName = "Race_", menuName = "ArcaneMMO/Race Definition")]
    public class RaceDefinitionSO : ScriptableObject
    {
        [Header("Gameplay — synced to the server")]
        public string id;
        public string displayName;
        [Tooltip("Water / Fire / Wind / Earth / Light / Dark")]
        public string element = "Light";
        [Space] public int str = 5, dex = 5, intel = 5, vit = 5, spi = 5, luk = 5, armor;
        [Tooltip("Continente onde esta raça nasce — id do zones.json (1=Veridia, 2=Aethermoor, …)")]
        public int homeZoneId = 1;

        [Header("Client art — NOT synced (model lives on the CharacterTemplate)")]
        public Sprite icon;
        [TextArea] public string description;
    }
}
