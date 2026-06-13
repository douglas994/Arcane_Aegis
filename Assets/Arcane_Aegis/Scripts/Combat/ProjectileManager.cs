using System.Collections.Generic;
using UnityEngine;
using Arcane_Aegis.Content;

namespace Arcane_Aegis.Combat
{
    /// <summary>
    /// Renders server projectiles (skillshots): on S2C_ProjectileSpawn it flies a visual along the given direction at
    /// the server's speed; on S2C_ProjectileDespawn it removes it (and shows the impact VFX on a hit). The server is
    /// authoritative on the actual hit — this is purely cosmetic. Put it on an active scene object and assign the
    /// ContentLibrary so it can resolve each skill's projectile/impact VFX (falls back to a small sphere).
    /// </summary>
    public sealed class ProjectileManager : MonoBehaviour
    {
        public static ProjectileManager Instance { get; private set; }

        [SerializeField] private ContentLibrary library; // optional; falls back to ContentLibrary.Active

        private sealed class Live { public GameObject go; public int abilityId; public Vector3 dir; public float speed; public float life; }
        private readonly Dictionary<uint, Live> _live = new();
        private readonly List<uint> _expired = new();

        private ContentLibrary Lib => library != null ? library : ContentLibrary.Active;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            var go = new GameObject("ProjectileManager");
            DontDestroyOnLoad(go);
            go.AddComponent<ProjectileManager>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }
        private void OnDestroy() { if (Instance == this) Instance = null; }

        public void Spawn(uint id, int abilityId, Vector3 start, Vector3 dir, float speed, float range)
        {
            SkillDefinitionSO so = Lib != null ? Lib.GetSkill(abilityId) : null;

            GameObject go;
            if (so != null && so.projectileVfx != null) go = Instantiate(so.projectileVfx);
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Sphere); // fallback so it's visible
                go.transform.localScale = Vector3.one * 0.4f;
                var col = go.GetComponent<Collider>(); if (col != null) Destroy(col);
            }
            go.transform.position = start;
            if (dir.sqrMagnitude > 0.0001f) go.transform.rotation = Quaternion.LookRotation(new Vector3(dir.x, 0f, dir.z));

            _live[id] = new Live { go = go, abilityId = abilityId, dir = dir.normalized, speed = speed, life = speed > 0f ? range / speed + 0.5f : 1f };
        }

        public void Despawn(uint id, bool hit)
        {
            if (!_live.TryGetValue(id, out var l)) return;
            if (hit && l.go != null)
            {
                var so = Lib != null ? Lib.GetSkill(l.abilityId) : null;
                if (so != null && so.impactVfx != null) Instantiate(so.impactVfx, l.go.transform.position, Quaternion.identity);
                else FxFlash.Spawn(l.go.transform.position, new Color(1f, 0.85f, 0.4f, 0.9f), 1.1f, 0.3f); // default impact
            }
            if (l.go != null) Destroy(l.go);
            _live.Remove(id);
        }

        private void Update()
        {
            if (_live.Count == 0) return;
            float dt = Time.deltaTime;
            _expired.Clear();
            foreach (var kv in _live)
            {
                var l = kv.Value;
                if (l.go == null) { _expired.Add(kv.Key); continue; }
                l.go.transform.position += l.dir * (l.speed * dt);
                l.life -= dt;
                if (l.life <= 0f) _expired.Add(kv.Key);
            }
            for (int i = 0; i < _expired.Count; i++)
            {
                if (_live.TryGetValue(_expired[i], out var l) && l.go != null) Destroy(l.go);
                _live.Remove(_expired[i]);
            }
        }
    }
}
