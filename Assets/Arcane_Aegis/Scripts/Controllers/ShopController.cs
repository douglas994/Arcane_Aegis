using UnityEngine;
using UnityEngine.InputSystem;
using Arcane_Aegis.Entities;
using Arcane_Aegis.UI;

namespace Arcane_Aegis.Controllers
{
    /// <summary>"Press E to shop": near a vendor NPC, opens its shop (toggles closed if already open). Drop this on a scene
    /// object once. The buy/sell themselves are server-validated.</summary>
    public sealed class ShopController : MonoBehaviour
    {
        [SerializeField] private float range = 5f; // matches the server's VendorRange
        [SerializeField] private Key interactKey = Key.E;

        private EntityManager _entities;

        private void Awake() => _entities = FindAnyObjectByType<EntityManager>();

        private void Update()
        {
            if (_entities == null) { _entities = FindAnyObjectByType<EntityManager>(); return; }
            var local = _entities.Local;
            var kb = Keyboard.current;
            if (local == null || kb == null) return;

            if (kb[interactKey].wasPressedThisFrame)
            {
                if (ShopPanel.Instance != null && ShopPanel.Instance.IsOpen) { ShopPanel.Instance.Close(); return; }
                var vendor = _entities.NearestVendor(local.transform.position, range);
                if (vendor != null && ShopPanel.Instance != null) ShopPanel.Instance.Open(vendor.ContentId);
            }
        }
    }
}
