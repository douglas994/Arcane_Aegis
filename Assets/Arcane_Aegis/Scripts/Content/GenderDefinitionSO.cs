using UnityEngine;

namespace Arcane_Aegis.Content
{
    /// <summary>A gender as a ScriptableObject: gameplay (id/name, synced) + client art (model/icon).
    /// Create via Assets ▸ Create ▸ ArcaneMMO ▸ Gender Definition.</summary>
    [CreateAssetMenu(fileName = "Gender_", menuName = "ArcaneMMO/Gender Definition")]
    public class GenderDefinitionSO : ScriptableObject
    {
        [Header("Gameplay — synced to the server")]
        public string id;
        public string displayName;

        [Header("Client art — NOT synced (model lives on the CharacterTemplate per gender)")]
        public Sprite icon;
        [TextArea] public string description;
    }
}
