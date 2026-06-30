using UnityEngine;
using UnityEngine.InputSystem;
using Arcane_Aegis.Entities;
using Arcane_Aegis.Network;
using Arcane_Aegis.UI;

namespace Arcane_Aegis.Controllers
{
    /// <summary>Put this on the EXIT PORTAL object you place in the Dungeon scene. When the local player stands near it and
    /// presses E, it asks the server to leave the instance and return to the open world (the scene swaps back). Because
    /// the dungeon is a hand-built scene, the exit portal is just a scene object here (no server entity / coord-matching) —
    /// it triggers <see cref="NetClient.SendLeaveDungeon"/>, which the server only honors while you're actually in a dungeon.</summary>
    public sealed class DungeonExitPortal : MonoBehaviour
    {
        [SerializeField] private float range = 4f;
        [SerializeField] private Key interactKey = Key.E;

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
                NetClient.Instance?.SendLeaveDungeon();
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.85f, 0.3f, 1f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, range);
        }
    }
}
