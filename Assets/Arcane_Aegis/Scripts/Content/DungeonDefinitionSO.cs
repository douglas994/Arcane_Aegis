using UnityEngine;

namespace Arcane_Aegis.Content
{
    /// <summary>Defines ONE dungeon so the game can have many. Create via Assets ▸ Create ▸ ArcaneMMO ▸ Dungeon, then add it
    /// to the ContentLibrary (or click Collect→Library). The client uses it to pick which Unity SCENE to load for a given
    /// dungeon. Adding a new dungeon = make one of these + a scene + author its content (spawners tagged with this <see
    /// cref="id"/>) + drop a <c>DungeonEntrancePortal</c> in the world pointing here. No server change, no zones.json edit.</summary>
    [CreateAssetMenu(fileName = "Dungeon", menuName = "ArcaneMMO/Dungeon")]
    public class DungeonDefinitionSO : ScriptableObject
    {
        [Tooltip("Id do TEMPLATE da dungeon (use 100-255). É a TAG do conteúdo: autora os mobs como SpawnMarkers com Export zone = este id.")]
        public int id = 100;

        [Tooltip("Nome pra UI (opcional).")]
        public string displayName = "";

        [Tooltip("Cena Unity a carregar pra esta dungeon. PRECISA estar no Build Settings. Cada dungeon pode ter a sua (arte própria).")]
        public string sceneName = "Dungeon";
    }
}
