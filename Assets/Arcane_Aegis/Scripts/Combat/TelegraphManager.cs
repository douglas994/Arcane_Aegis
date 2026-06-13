using System.Collections.Generic;
using UnityEngine;
using ArcaneShared.Enums;
using Arcane_Aegis.Content;
using Arcane_Aegis.Entities;

namespace Arcane_Aegis.Combat
{
    /// <summary>
    /// Draws a ground telegraph for a telegraphed (wind-up &gt; 0) skill: a translucent shape (cone / circle / line)
    /// on the floor at the caster, matching the skill's targeting, that intensifies over the wind-up and vanishes when
    /// the effect resolves — so a target can step out. Purely cosmetic; the server still resolves the real hit.
    /// Parented to the caster so it tracks facing (the server resolves at the wind-up's END with current facing).
    /// Put it on an active scene object; it resolves skills via ContentLibrary.Active (no inspector ref needed).
    /// </summary>
    public sealed class TelegraphManager : MonoBehaviour
    {
        public static TelegraphManager Instance { get; private set; }

        [SerializeField] private Color color = new(1f, 0.25f, 0.2f, 0.5f);
        [SerializeField] private int segments = 28;     // smoothness of cone/circle arcs
        [SerializeField] private float groundOffset = 0.06f; // lift off the ground to avoid z-fighting

        private sealed class Live { public GameObject go; public Material mat; public float start; public float end; }
        private readonly Dictionary<ushort, Live> _live = new();
        private readonly List<ushort> _expired = new();
        private Material _template;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            var go = new GameObject("TelegraphManager");
            DontDestroyOnLoad(go);
            go.AddComponent<TelegraphManager>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }
        private void OnDestroy() { if (Instance == this) Instance = null; }

        /// <summary>Shows the telegraph for a caster's skill over <paramref name="castSeconds"/> (AoE shapes only).</summary>
        public void Show(EntityView caster, int abilityId, float castSeconds)
        {
            if (caster == null || castSeconds <= 0f) return;
            SkillDefinitionSO so = ContentLibrary.Active != null ? ContentLibrary.Active.GetSkill(abilityId) : null;
            if (so == null) return;

            Mesh mesh = BuildMesh(so);
            if (mesh == null) return; // Self/Single → nothing to dodge

            Despawn(caster.Id); // replace any previous telegraph from this caster

            var go = new GameObject($"Telegraph_{caster.Id}");
            go.transform.SetParent(caster.transform, worldPositionStays: false);
            go.transform.localPosition = new Vector3(0f, groundOffset, 0f);
            go.transform.localRotation = Quaternion.identity; // mesh +Z = caster forward

            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            var mat = new Material(Template());
            mr.sharedMaterial = mat;

            _live[caster.Id] = new Live { go = go, mat = mat, start = Time.time, end = Time.time + castSeconds };
        }

        public void Despawn(ushort casterId)
        {
            if (!_live.TryGetValue(casterId, out var l)) return;
            if (l.go != null) Destroy(l.go);
            if (l.mat != null) Destroy(l.mat);
            _live.Remove(casterId);
        }

        private void Update()
        {
            if (_live.Count == 0) return;
            float now = Time.time;
            _expired.Clear();
            foreach (var kv in _live)
            {
                var l = kv.Value;
                if (l.go == null) { _expired.Add(kv.Key); continue; }
                float t = Mathf.InverseLerp(l.start, l.end, now);          // 0..1 over the wind-up
                var c = color; c.a = Mathf.Lerp(color.a * 0.4f, color.a, t); // intensify toward the hit
                l.mat.color = c;
                if (now >= l.end) _expired.Add(kv.Key);
            }
            for (int i = 0; i < _expired.Count; i++) Despawn(_expired[i]);
        }

        // ── mesh builders (XZ plane, local +Z = caster forward) ──
        private Mesh BuildMesh(SkillDefinitionSO so) => so.targeting switch
        {
            TargetingMode.Circle => BuildFan(so.range, 0f, 360f),
            TargetingMode.Cone   => BuildFan(so.range, -so.coneAngle * 0.5f, so.coneAngle * 0.5f),
            TargetingMode.Line   => BuildQuad(Mathf.Max(0.1f, so.width), so.range),
            _ => null, // Self / Single: no area telegraph
        };

        /// <summary>A triangle fan from the origin spanning [a0,a1] degrees at <paramref name="radius"/> (circle or cone).</summary>
        private Mesh BuildFan(float radius, float a0Deg, float a1Deg)
        {
            if (radius <= 0.01f) return null;
            int seg = Mathf.Max(3, segments);
            var verts = new Vector3[seg + 2];
            var tris = new int[seg * 3];
            verts[0] = Vector3.zero;
            float a0 = a0Deg * Mathf.Deg2Rad, a1 = a1Deg * Mathf.Deg2Rad;
            for (int i = 0; i <= seg; i++)
            {
                float a = Mathf.Lerp(a0, a1, i / (float)seg);
                verts[i + 1] = new Vector3(Mathf.Sin(a) * radius, 0f, Mathf.Cos(a) * radius); // yaw 0 = +Z
            }
            for (int i = 0; i < seg; i++) { tris[i * 3] = 0; tris[i * 3 + 1] = i + 1; tris[i * 3 + 2] = i + 2; }
            var m = new Mesh { name = "telegraph_fan" };
            m.SetVertices(verts); m.SetTriangles(tris, 0); m.RecalculateBounds();
            return m;
        }

        /// <summary>A quad in front of the caster: width on X (centered), length on +Z (a line skill).</summary>
        private Mesh BuildQuad(float width, float length)
        {
            if (length <= 0.01f) return null;
            float hw = width * 0.5f;
            var verts = new[]
            {
                new Vector3(-hw, 0f, 0f), new Vector3(hw, 0f, 0f),
                new Vector3(hw, 0f, length), new Vector3(-hw, 0f, length),
            };
            var tris = new[] { 0, 1, 2, 0, 2, 3 };
            var m = new Mesh { name = "telegraph_line" };
            m.SetVertices(verts); m.SetTriangles(tris, 0); m.RecalculateBounds();
            return m;
        }

        /// <summary>A translucent, unlit, double-sided base material (Sprites/Default blends reliably under URP).</summary>
        private Material Template()
        {
            if (_template != null) return _template;
            Shader sh = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
            _template = new Material(sh) { color = color };
            return _template;
        }
    }
}
