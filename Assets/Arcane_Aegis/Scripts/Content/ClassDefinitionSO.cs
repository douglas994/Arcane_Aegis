using UnityEngine;

namespace Arcane_Aegis.Content
{
    /// <summary>A class as a ScriptableObject: gameplay data (synced to the server) + client art (model/icon/desc,
    /// stays on the client). Create via Assets ▸ Create ▸ ArcaneMMO ▸ Class Definition.</summary>
    [CreateAssetMenu(fileName = "Class_", menuName = "ArcaneMMO/Class Definition")]
    public class ClassDefinitionSO : ScriptableObject
    {
        [Header("Gameplay — synced to the server")]
        public string id;
        public string displayName;
        [Space] public int str = 5, dex = 5, intel = 5, vit = 5, spi = 5, luk = 5;
        [Space] public int strPerLevel = 1, dexPerLevel, intPerLevel, vitPerLevel = 1, spiPerLevel, lukPerLevel;

        [Header("Client art — NOT synced (model lives on the CharacterTemplate)")]
        public Sprite icon;
        [TextArea] public string description;
    }
}
