using System.Collections.Generic;
using UnityEngine;
using ArcaneShared.Enums;
using ArcaneShared.Models;
using ArcaneShared.Protocol.ServerToClient;

namespace Arcane_Aegis.Entities
{
    /// <summary>
    /// Spawns / updates / destroys entity views by id. ONE character prefab is shared by the local player
    /// and remotes — <see cref="PlayerView.Initialize"/> decides who drives the transform (KCC vs
    /// interpolation). (Later: a prefab per EntityType.)
    /// </summary>
    public class EntityManager : MonoBehaviour
    {
        [SerializeField] private GameObject characterPrefab; // unified player prefab (PlayerView on root)

        private readonly Dictionary<ushort, EntityView> _views = new();

        /// <summary>Spawns another player/entity (driven by snapshots).</summary>
        public void SpawnRemote(S2C_SpawnEntity data)
        {
            if (_views.ContainsKey(data.EntityId)) return;

            EntityView view = CreateView(data.EntityId, data.Name);
            view.Id = data.EntityId;
            view.Type = data.Type;
            if (view is PlayerView pv) pv.Initialize(isLocal: false);
            else Debug.LogError("[Entities] characterPrefab root has no PlayerView — remote control stack won't be disabled. Put PlayerView on the prefab ROOT.");
            view.Teleport(new Vector3(data.Position.X, data.Position.Y, data.Position.Z), data.Yaw);

            _views[data.EntityId] = view;
            Debug.Log($"[Entities] spawn #{data.EntityId} ({data.Name})");
        }

        /// <summary>Spawns OUR own player at the SERVER-given spawn point (NetClient calls this on login).</summary>
        public PlayerView SpawnLocal(ushort id, string name, Vector3 serverSpawn)
        {
            EntityView view = CreateView(id, name);
            view.Id = id;
            view.Type = EntityType.Player;

            var pv = view as PlayerView;
            if (pv != null) pv.Initialize(isLocal: true);
            else Debug.LogError("[Entities] characterPrefab root has no PlayerView — local control/interpolation won't be configured. Put PlayerView on the prefab ROOT.");

            // The server dictates the spawn (positive, inside the zone — coords are quantized to [0,8192]).
            // Small offset so two test clients don't overlap; +2 y so the KCC drops onto the ground.
            Vector2 off = Random.insideUnitCircle * 2f;
            Vector3 spawn = new Vector3(serverSpawn.x + off.x, serverSpawn.y + 2f, serverSpawn.z + off.y);
            if (pv != null && pv.Motor != null) pv.Motor.SetPosition(spawn);
            else view.transform.position = spawn;

            return pv;
        }

        public void Despawn(ushort id)
        {
            if (!_views.TryGetValue(id, out var view)) return;
            Destroy(view.gameObject);
            _views.Remove(id);
            Debug.Log($"[Entities] despawn #{id}");
        }

        public void ApplySnapshot(in SnapshotEntry e)
        {
            if (_views.TryGetValue(e.Id, out var view)) view.ApplySnapshot(e);
        }

        public bool TryGetView(ushort id, out EntityView view) => _views.TryGetValue(id, out view);

        /// <summary>Plays the attack animation on a remote entity (from S2C_AbilityCast).</summary>
        public void PlayAttack(ushort id)
        {
            if (_views.TryGetValue(id, out var view)) view.PlayAttack();
        }

        private EntityView CreateView(ushort id, string name)
        {
            GameObject go = characterPrefab != null
                ? Instantiate(characterPrefab)
                : GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = $"Entity_{id}_{name}";

            EntityView view = go.GetComponent<EntityView>();
            if (view == null) view = go.AddComponent<PlayerView>(); // fallback so the capsule still works
            return view;
        }
    }
}
