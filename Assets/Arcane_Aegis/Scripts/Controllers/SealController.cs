using UnityEngine;
using UnityEngine.InputSystem;
using Arcane_Aegis.Entities;
using Arcane_Aegis.Network;
using Arcane_Aegis.UI;

namespace Arcane_Aegis.Controllers
{
    /// <summary>"Press G to break a seal": finds the nearest sealed-boss altar within range of the local player and asks
    /// the server to break it (server validates level + key items, consumes them, awakens the boss). Drop this on a
    /// scene object once.</summary>
    public sealed class SealController : MonoBehaviour
    {
        [SerializeField] private float range = 5f; // matches the server's seal interact range
        [SerializeField] private Key interactKey = Key.G;

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
                var seal = _entities.NearestSeal(local.transform.position, range);
                if (seal != null && NetClient.Instance != null) NetClient.Instance.SendInteractSeal(seal.Id);
            }
        }
    }
}
