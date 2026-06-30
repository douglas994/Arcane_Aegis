using System.Collections.Generic;
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
        [Tooltip("Ability ids esta classe pode castar. Vazio = sem restrição (qualquer skill).")]
        public List<int> skillIds = new();

        /// <summary>One line of the class's starting loadout: an item id + quantity (granted at character creation).</summary>
        [System.Serializable] public struct StartItem { public string itemId; public int qty; }
        [Tooltip("Itens iniciais (loadout) dados a um personagem NOVO desta classe — concedidos na criação, estilo Atavism. Vão pra bag.")]
        public List<StartItem> startItems = new();

        [Header("Client art — NOT synced (model lives on the CharacterTemplate)")]
        public Sprite icon;
        [TextArea] public string description;
    }
}
