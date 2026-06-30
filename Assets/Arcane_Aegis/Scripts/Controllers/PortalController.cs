using UnityEngine;
using UnityEngine.InputSystem;
using Arcane_Aegis.Entities;
using Arcane_Aegis.Network;
using Arcane_Aegis.UI;

namespace Arcane_Aegis.Controllers
{
    /// <summary>"Press E at a dungeon portal": finds the nearest portal within range of the local player and interacts
    /// with it. The SERVER decides what happens — an entrance portal (open world) sends you into an instanced dungeon
    /// (spawned on demand, a few seconds), an exit portal (inside a dungeon) returns you home. Drop this on a scene
    /// object once.</summary>
    public sealed class PortalController : MonoBehaviour
    {
        [SerializeField] private float range = 5f; // matches the server's portal interact range
        [SerializeField] private Key interactKey = Key.E;

        private EntityManager _entities;

        private void Awake() => _entities = FindAnyObjectByType<EntityManager>();

        private void Update()
        {
            if (_entities == null) { _entities = FindAnyObjectByType<EntityManager>(); return; }
            var local = _entities.Local;
            var kb = Keyboard.current;
            if (local == null || kb == null) return;

            if (kb[interactKey].wasPressedThisFrame && !UiFocus.IsTyping)
            {
                var portal = _entities.NearestPortal(local.transform.position, range);
                if (portal != null && NetClient.Instance != null) NetClient.Instance.SendInteractPortal(portal.Id);
            }
        }
    }
}
