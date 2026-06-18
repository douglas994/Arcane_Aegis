using UnityEngine;
using UnityEngine.InputSystem;
using Arcane_Aegis.Entities;
using Arcane_Aegis.Network;
using Arcane_Aegis.UI;

namespace Arcane_Aegis.Controllers
{
    /// <summary>"Press F to gather": finds the nearest resource node within range of the local player and asks the
    /// server to harvest it (server validates tool/charges/range + resolves). Drop this on a scene object once.</summary>
    public sealed class GatherController : MonoBehaviour
    {
        [SerializeField] private float range = 3.5f; // matches the server's GatherRange
        [SerializeField] private Key gatherKey = Key.F;

        private EntityManager _entities;

        private void Awake() => _entities = FindAnyObjectByType<EntityManager>();

        private void Update()
        {
            if (_entities == null) { _entities = FindAnyObjectByType<EntityManager>(); return; }
            var local = _entities.Local;
            var kb = Keyboard.current;
            if (local == null || kb == null) return;

            if (kb[gatherKey].wasPressedThisFrame && !UiFocus.IsTyping)
            {
                var node = _entities.NearestResourceNode(local.transform.position, range);
                if (node != null && NetClient.Instance != null) NetClient.Instance.SendGather(node.Id);
            }
        }
    }
}
