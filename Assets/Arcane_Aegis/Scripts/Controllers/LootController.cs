using System.Collections.Generic;
using UnityEngine;
using Arcane_Aegis.Entities;
using Arcane_Aegis.Network;

namespace Arcane_Aegis.Controllers
{
    /// <summary>Auto-pickup of ground loot: when the local player walks within range of a drop, asks the server to pick
    /// it up (server validates range + loot-lock and ignores what isn't yours yet). Per-drop throttle so a loot-locked
    /// item isn't spammed — it just retries every <see cref="retrySeconds"/> until the lock frees or it's collected.
    /// Drop this on a scene object once. (Groundwork for a pet loot-vacuum: same call, on the owner's behalf.)</summary>
    public sealed class LootController : MonoBehaviour
    {
        [SerializeField] private float range = 3f;          // a touch under the server's 3.5m so the server check passes
        [SerializeField] private float retrySeconds = 1.5f; // re-attempt interval for a drop still in range (e.g. loot-locked)

        private EntityManager _entities;
        private readonly Dictionary<ushort, float> _nextAttempt = new();

        private void Awake() => _entities = FindAnyObjectByType<EntityManager>();

        private void Update()
        {
            if (_entities == null) { _entities = FindAnyObjectByType<EntityManager>(); return; }
            var local = _entities.Local;
            if (local == null || NetClient.Instance == null) return;

            var drop = _entities.NearestItemDrop(local.transform.position, range);
            if (drop == null) return;

            float now = Time.time;
            if (_nextAttempt.TryGetValue(drop.Id, out var next) && now < next) return;
            _nextAttempt[drop.Id] = now + retrySeconds;
            NetClient.Instance.SendPickup(drop.Id);
        }
    }
}
