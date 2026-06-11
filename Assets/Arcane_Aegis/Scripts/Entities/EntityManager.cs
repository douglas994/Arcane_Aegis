using System.Collections.Generic;
using UnityEngine;
using ArcaneShared.Enums;
using ArcaneShared.Models;
using ArcaneShared.Protocol.ServerToClient;
using Arcane_Aegis.Network;
using Arcane_Aegis.Content;

namespace Arcane_Aegis.Entities
{
    /// <summary>
    /// Spawns / updates / destroys entity views by id. ONE character prefab is shared by the local player
    /// and remotes — <see cref="PlayerView.Initialize"/> decides who drives the transform (KCC vs
    /// interpolation). (Later: a prefab per EntityType.)
    /// </summary>
    public class EntityManager : MonoBehaviour
    {
        /// <summary>Optional per-type prefab override (e.g. a goblin prefab with MonsterView on its root).</summary>
        [System.Serializable]
        public struct TypePrefab { public EntityType type; public GameObject prefab; }

        [Header("Prefabs")]
        [SerializeField] private GameObject characterPrefab;   // default / player (PlayerView on root, with a "ModelMount" child)
        [SerializeField] private TypePrefab[] typePrefabs;     // per-EntityType overrides (fallback = characterPrefab)
        [SerializeField] private ContentLibrary library;       // resolves a player's CharacterTemplate model (race+class+gender)

        private readonly Dictionary<ushort, EntityView> _views = new();

        /// <summary>Our own player view (set on login). The HUD / action-bar read vitals + combat through this.</summary>
        public PlayerView Local { get; private set; }

        // Bind this scene's manager to the persistent connection so entity packets route here (World/Dungeon).
        private void Start() => NetClient.Instance?.SetEntityManager(this);

        /// <summary>Spawns another player/entity (driven by snapshots).</summary>
        public void SpawnRemote(S2C_SpawnEntity data)
        {
            if (_views.ContainsKey(data.EntityId)) return;

            EntityView view = CreateView(data.EntityId, data.Name, data.Type, data.RaceId, data.ClassId, data.GenderId);
            view.Id = data.EntityId;
            view.Type = data.Type;
            view.Spawn(isLocal: false);
            view.Teleport(new Vector3(data.Position.X, data.Position.Y, data.Position.Z), data.Yaw);

            _views[data.EntityId] = view;
            Debug.Log($"[Entities] spawn #{data.EntityId} ({data.Name}) [{data.Type}]");
        }

        /// <summary>Spawns OUR own player at the SERVER-given spawn point (NetClient calls this on login).</summary>
        public PlayerView SpawnLocal(ushort id, string name, Vector3 serverSpawn, string raceId, string classId, string genderId)
        {
            EntityView view = CreateView(id, name, EntityType.Player, raceId, classId, genderId);
            view.Id = id;
            view.Type = EntityType.Player;
            view.Spawn(isLocal: true);

            var pv = view as PlayerView;
            if (pv == null) Debug.LogError("[Entities] local prefab root has no PlayerView — local control/interpolation won't be configured. Put PlayerView on the prefab ROOT.");

            // The server dictates the spawn (positive, inside the zone — coords are quantized to [0,8192]).
            // Small offset so two test clients don't overlap; +2 y so the KCC drops onto the ground.
            Vector2 off = Random.insideUnitCircle * 2f;
            Vector3 spawn = new Vector3(serverSpawn.x + off.x, serverSpawn.y + 2f, serverSpawn.z + off.y);
            if (pv != null && pv.Motor != null) pv.Motor.SetPosition(spawn);
            else view.transform.position = spawn;

            Local = pv;
            return pv;
        }

        public void Despawn(ushort id)
        {
            if (!_views.TryGetValue(id, out var view)) return;
            if (Local != null && Local.Id == id) Local = null;
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

        private EntityView CreateView(ushort id, string name, EntityType type, string raceId, string classId, string genderId)
        {
            GameObject prefab = PrefabFor(type);
            GameObject model = (library != null && !string.IsNullOrEmpty(raceId)) ? library.ResolveModel(raceId, classId, genderId) : null;

            GameObject go;
            if (prefab != null)
            {
                go = Instantiate(prefab);
                MountModel(go, model); // mount the data-driven model under "ModelMount"; gameplay stays on the prefab root
            }
            else if (model != null)
            {
                go = Instantiate(model); // no gameplay prefab assigned → the ContentLibrary model IS the entity (no capsule)
            }
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Capsule); // last resort only (no prefab AND no model)
            }
            go.name = $"Entity_{id}_{name}";

            EntityView view = go.GetComponent<EntityView>();
            if (view == null) view = go.AddComponent<PlayerView>(); // fallback so it still works
            return view;
        }

        /// <summary>Instantiates the resolved CharacterTemplate model under the prefab's "ModelMount" child (or the
        /// root if absent), visual-only — the prefab root keeps the gameplay (PlayerView/KCC/Animator).</summary>
        private void MountModel(GameObject root, GameObject model)
        {
            if (model == null) return;
            Transform mount = FindDeepChild(root.transform, "ModelMount") ?? root.transform;
            GameObject vis = Instantiate(model, mount);
            vis.transform.localPosition = Vector3.zero;
            vis.transform.localRotation = Quaternion.identity;

            // Visual only: drop colliders/rigidbodies so they don't fight the root's physics (keep meshes + Animator).
            foreach (var col in vis.GetComponentsInChildren<Collider>()) Destroy(col);
            foreach (var rb in vis.GetComponentsInChildren<Rigidbody>()) Destroy(rb);
        }

        private static Transform FindDeepChild(Transform parent, string childName)
        {
            if (parent.name == childName) return parent;
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform found = FindDeepChild(parent.GetChild(i), childName);
                if (found != null) return found;
            }
            return null;
        }

        /// <summary>The prefab for an entity type — a per-type override if assigned, else the default character prefab.</summary>
        private GameObject PrefabFor(EntityType type)
        {
            if (typePrefabs != null)
                for (int i = 0; i < typePrefabs.Length; i++)
                    if (typePrefabs[i].type == type && typePrefabs[i].prefab != null)
                        return typePrefabs[i].prefab;
            return characterPrefab;
        }
    }
}
