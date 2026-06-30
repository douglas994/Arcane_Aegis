using UnityEngine;
using UnityEngine.InputSystem;
using Arcane_Aegis.Content;
using Arcane_Aegis.Entities;
using Arcane_Aegis.Network;
using Arcane_Aegis.UI;

namespace Arcane_Aegis.Controllers
{
    /// <summary>Put this on an ENTRANCE PORTAL object you place in the open-world (World) scene. When the local player
    /// stands near it and presses E, it asks the server to enter the dungeon TEMPLATE set in <see cref="dungeonDef"/>
    /// (the instance is spawned on demand and the scene swaps to the Dungeon scene). Symmetric with
    /// <see cref="DungeonExitPortal"/> — both portals are just scene objects you position by hand (no SpawnMarker, no
    /// server entity, no coord-matching). The server validates (alive, not already in a dungeon).</summary>
    public sealed class DungeonEntrancePortal : MonoBehaviour
    {
        [SerializeField] private float range = 4f;
        [SerializeField] private Key interactKey = Key.E;
        [Tooltip("Qual dungeon este portal abre — arraste o DungeonDefinitionSO aqui (preferido).")]
        [SerializeField] private DungeonDefinitionSO dungeon;
        [Tooltip("Fallback: id do template se nenhum DungeonDefinitionSO for atribuído acima. Autora os mobs como spawners da MESMA zona.")]
        [SerializeField] private int dungeonDefFallback = 100;

        private byte ResolveDef() => dungeon != null
            ? (byte)Mathf.Clamp(dungeon.id, 0, 255)
            : (byte)Mathf.Clamp(dungeonDefFallback, 0, 255);

        private EntityManager _entities;

        private void Awake() => _entities = FindAnyObjectByType<EntityManager>();

        private void Update()
        {
            if (_entities == null) { _entities = FindAnyObjectByType<EntityManager>(); return; }
            var local = _entities.Local;
            var kb = Keyboard.current;
            if (local == null || kb == null) return;
            if (!kb[interactKey].wasPressedThisFrame || UiFocus.IsTyping) return;

            if ((local.transform.position - transform.position).sqrMagnitude <= range * range)
                NetClient.Instance?.SendEnterDungeon(ResolveDef());
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.3f, 0.85f, 1f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, range);
        }
    }
}
