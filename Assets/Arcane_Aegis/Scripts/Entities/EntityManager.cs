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

        /// <summary>This continent's world offset (grid × worldSize). The server sends LOCAL coords; we render in
        /// GLOBAL (local + offset). Set on spawn/zone-change from S2C_LoginResult so a border crossing keeps the
        /// player's global position continuous → no teleport.</summary>
        public Vector3 ZoneOffset { get; private set; }
        public Vector3 ToWorld(Vector3 serverLocal) => serverLocal + ZoneOffset;
        public Vector3 ToServer(Vector3 world) => world - ZoneOffset;

        // Bind this scene's manager to the persistent connection so entity packets route here (World/Dungeon).
        private void Start() => NetClient.Instance?.SetEntityManager(this);

        /// <summary>Spawns another player/entity (driven by snapshots).</summary>
        public void SpawnRemote(S2C_SpawnEntity data)
        {
            if (_views.ContainsKey(data.EntityId)) return;

            EntityView view = CreateView(data.EntityId, data.Name, data.Type, data.RaceId, data.ClassId, data.GenderId, data.MonsterId);
            view.Id = data.EntityId;
            view.Type = data.Type;
            view.DisplayName = data.Name ?? "";
            view.WorldOffset = ZoneOffset; // render this continent's locals in global space
            view.EquippedMainHand = data.MainHandItemId ?? ""; // visible weapon (replicated)
            EnsureCombatant(view, data.Type); // HP bar + death cue (before Spawn, which reads the component)
            view.Spawn(isLocal: false);
            view.Teleport(new Vector3(data.Position.X, data.Position.Y, data.Position.Z), data.Yaw);
            if (IsTargetableType(data.Type)) AttachHitbox(view, data.Type, data.MonsterId); // controlled hitbox for mira/seleção

            _views[data.EntityId] = view;
        }

        /// <summary>Spawns OUR own player at the SERVER-given spawn point (NetClient calls this on login).</summary>
        public PlayerView SpawnLocal(ushort id, string name, Vector3 serverSpawn, string raceId, string classId, string genderId, Vector3 zoneOffset)
        {
            ZoneOffset = zoneOffset; // this continent's offset (server LOCAL → our GLOBAL)
            EntityView view = CreateView(id, name, EntityType.Player, raceId, classId, genderId, "");
            view.Id = id;
            view.Type = EntityType.Player;
            EnsureCombatant(view, EntityType.Player); // HP bar + death cue (before Spawn, which reads the component)
            view.Spawn(isLocal: true);

            var pv = view as PlayerView;
            if (pv == null) Debug.LogError("[Entities] local prefab root has no PlayerView — local control/interpolation won't be configured. Put PlayerView on the prefab ROOT.");

            // Server spawn is in LOCAL coords → render in global. Small xz offset so two test clients don't overlap;
            // +2 y so the KCC drops onto the ground.
            Vector3 ws = ToWorld(serverSpawn);
            Vector2 off = Random.insideUnitCircle * 2f;
            Vector3 spawn = new Vector3(ws.x + off.x, ws.y + 2f, ws.z + off.y);
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
        }

        /// <summary>Zone change (border handoff): re-home OUR player on the new continent — drop the old continent's
        /// entities, re-key the local view to the new server id, and teleport it to the new local spawn. The
        /// character (model/appearance) is unchanged — only the id + position change.</summary>
        public void RespawnLocal(ushort newId, Vector3 serverSpawn, Vector3 zoneOffset)
        {
            if (Local == null) return;
            ZoneOffset = zoneOffset; // the NEW continent's offset → the player's GLOBAL position stays continuous

            // The old continent's entities aren't in the new one → drop everything except our own player.
            var stale = new List<ushort>();
            foreach (var kv in _views) if (kv.Key != Local.Id) stale.Add(kv.Key);
            foreach (ushort id in stale) { Destroy(_views[id].gameObject); _views.Remove(id); }

            // Re-key the local view to the new server-assigned id (snapshots/corrections reference it).
            _views.Remove(Local.Id);
            Local.Id = newId;
            _views[newId] = Local;

            // Global coords are CONTINUOUS across the border, so our current transform is already (almost) right —
            // only off by the latency the handoff round-trip took. Hard-teleporting to the server's recorded
            // crossing point would snap us BACKWARD by that drift (the visible "tremidinha"). So keep the smooth
            // current position and let the normal StateUpdate correction nudge it; only snap if we're genuinely far
            // off (something went wrong).
            Vector3 target = ToWorld(serverSpawn);            // where the server thinks we are (global)
            float driftSqr = (Local.transform.position - target).sqrMagnitude;
            if (driftSqr > 25f) // > 5 m → snap to be safe
            {
                if (Local.Motor != null) Local.Motor.SetPosition(target);
                else Local.transform.position = target;
            }
            Debug.Log($"[Entities] zone change → local re-homed as #{newId} @ global ({Local.transform.position.x:0},{Local.transform.position.z:0}) drift {Mathf.Sqrt(driftSqr):0.0}m");
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

        private EntityView CreateView(ushort id, string name, EntityType type, string raceId, string classId, string genderId, string monsterId)
        {
            GameObject prefab = PrefabFor(type);
            GameObject model = (library != null && !string.IsNullOrEmpty(raceId)) ? library.ResolveModel(raceId, classId, genderId) : null;
            // Monsters / resource nodes: resolve the model from the content definition by the replicated id (no capsule).
            if (model == null && library != null && !string.IsNullOrEmpty(monsterId))
            {
                if (type == EntityType.ResourceNode) { var nd = library.GetResourceNode(monsterId); if (nd != null && nd.model3D != null) model = nd.model3D; }
                else if (type == EntityType.Vendor) { var vd = library.GetVendor(monsterId); if (vd != null && vd.model3D != null) model = vd.model3D; }
                else { var md = library.GetMonster(monsterId); if (md != null && md.model3D != null) model = md.model3D; }
            }

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
            if (view == null) view = type == EntityType.Player ? go.AddComponent<PlayerView>() : go.AddComponent<EntityView>(); // players need the control stack; nodes/mobs just need a view
            view.ContentId = monsterId ?? ""; // replicated def id (monster/node/vendor) — used to resolve vendor shop, etc.
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
            // Monsters/nodes fall back to "model IS the entity" (no player-prefab capsule) unless a dedicated prefab is set.
            return type is EntityType.Monster or EntityType.ResourceNode or EntityType.Vendor ? null : characterPrefab;
        }

        /// <summary>Nearest resource node within <paramref name="range"/> of a world position (for "press to gather").</summary>
        public EntityView NearestResourceNode(Vector3 worldPos, float range) => Nearest(worldPos, range, EntityType.ResourceNode);

        /// <summary>Nearest vendor within <paramref name="range"/> of a world position (for "press to shop").</summary>
        public EntityView NearestVendor(Vector3 worldPos, float range) => Nearest(worldPos, range, EntityType.Vendor);

        private EntityView Nearest(Vector3 worldPos, float range, EntityType type)
        {
            EntityView best = null;
            float bestSq = range * range;
            foreach (var v in _views.Values)
            {
                if (v == null || v.Type != type) continue;
                float d = (v.transform.position - worldPos).sqrMagnitude;
                if (d <= bestSq) { bestSq = d; best = v; }
            }
            return best;
        }

        // ── Combatant composition (F0c) ──

        /// <summary>Adds the <see cref="CombatantVitals"/> component (HP bar + death cue) to a combatant view, so HP +
        /// death are composed onto ANY view (player/npc/monster/boss/pet) instead of inherited from a base class.
        /// No-op for non-combatants (nodes/vendors/mounts) and if the prefab already carries one.</summary>
        private static void EnsureCombatant(EntityView view, EntityType type)
        {
            if (!IsTargetableType(type)) return;
            if (view.GetComponent<CombatantVitals>() == null) view.gameObject.AddComponent<CombatantVitals>();
        }

        // ── Targeting (F0b) ──

        /// <summary>Anything you can lock onto in combat (a living combatant), vs nodes/vendors (interact, not fight).</summary>
        public static bool IsTargetableType(EntityType t)
            => t is EntityType.Player or EntityType.Monster or EntityType.Npc or EntityType.Boss or EntityType.Pet;

        /// <summary>Enemies that Tab cycles through (excludes players/pets so you don't tab onto a friend in PvE).</summary>
        public static bool IsEnemyType(EntityType t)
            => t is EntityType.Monster or EntityType.Npc or EntityType.Boss;

        /// <summary>Living targetables within <paramref name="range"/> of <paramref name="center"/> (for Tab-cycling).
        /// Excludes our own player. <paramref name="enemiesOnly"/> restricts to mobs (the usual Tab behaviour).</summary>
        public void CollectTargetables(List<EntityView> into, Vector3 center, float range, bool enemiesOnly)
        {
            into.Clear();
            float r2 = range * range;
            foreach (var v in _views.Values)
            {
                if (v == null) continue;
                if (enemiesOnly ? !IsEnemyType(v.Type) : !IsTargetableType(v.Type)) continue;
                if (Local != null && v.Id == Local.Id) continue;
                if (v.SnapHp01 <= 0f) continue; // dead
                if ((v.transform.position - center).sqrMagnitude <= r2) into.Add(v);
            }
        }

        /// <summary>Attaches a controlled trigger capsule (on the <c>Targetable</c> layer) so the entity can be raycast
        /// for mira/seleção. Size comes from the MonsterDefinitionSO for mobs (scales with the model), else a humanoid
        /// default. A trigger → never blocks the player's movement.</summary>
        private void AttachHitbox(EntityView view, EntityType type, string monsterId)
        {
            float radius = 0.4f, height = 2f; // humanoid default
            if ((type == EntityType.Monster || type == EntityType.Boss) && library != null && !string.IsNullOrEmpty(monsterId))
            {
                var md = library.GetMonster(monsterId);
                if (md != null)
                {
                    if (md.hitboxRadius > 0f) radius = md.hitboxRadius;
                    if (md.hitboxHeight > 0f) height = md.hitboxHeight;
                }
            }

            var go = new GameObject("Hitbox");
            go.transform.SetParent(view.transform, false);
            int layer = LayerMask.NameToLayer("Targetable");
            if (layer >= 0) go.layer = layer;
            else Debug.LogWarning("[Entities] layer 'Targetable' não existe — adicione em Project Settings ▸ Tags and Layers.");

            var cap = go.AddComponent<CapsuleCollider>();
            cap.isTrigger = true;  // never blocks movement; raycast hits it via QueryTriggerInteraction.Collide
            cap.direction = 1;     // Y axis
            cap.radius = radius;
            cap.height = Mathf.Max(height, radius * 2f);
            cap.center = new Vector3(0f, cap.height * 0.5f, 0f);
            go.AddComponent<TargetableHitbox>().Bind(view);
        }
    }
}
