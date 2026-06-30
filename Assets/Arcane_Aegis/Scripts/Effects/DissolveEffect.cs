using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Arcane_Aegis.Effects
{
    /// <summary>
    /// Plays a dissolve on a model's renderers using the <c>Arcane/DissolveURP</c> shader: it swaps each renderer's
    /// materials for dissolve instances (copying the base map + colour), animates <c>_DissolveAmount</c>, then restores
    /// (spawn) or fires a callback so the caller can despawn (despawn). The noise is procedural — no texture asset
    /// needed. Use the static <see cref="PlayIn"/> / <see cref="PlayOut"/> entry points; the component self-attaches.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DissolveEffect : MonoBehaviour
    {
        private static Shader _shader;
        private static Shader DissolveShader => _shader != null ? _shader : (_shader = Shader.Find("Arcane/DissolveURP"));
        private static Texture2D _noise;
        private static readonly Color DefaultEdge = new Color(1f, 0.55f, 0.15f, 1f) * 3f; // HDR so bloom catches the edge

        private struct Captured { public Renderer Renderer; public Material[] Original; public Material[] Dissolve; }
        private readonly List<Captured> _caps = new();
        private float _amount; // current dissolve amount (0 = solid, 1 = gone) — animations lerp FROM this
        private Action _pendingComplete; // out-effect callback (destroy) — fired even if the effect is interrupted

        /// <summary>Materialize in (spawn) — or revive a dissolved-away body: animate to solid, then restore the real
        /// materials. Reuses a prior death's captured state if present. Safe no-op if the object/shader is missing.</summary>
        public static void PlayIn(GameObject go, float duration = 0.6f, Color? edge = null)
            => Begin(go, fadeIn: true, duration, edge, null, keepStateOnOut: false);

        /// <summary>Dissolve out (despawn): burn away, then <paramref name="onComplete"/> runs so the caller can
        /// destroy/recycle the object. If the shader/object is missing, <paramref name="onComplete"/> still runs.</summary>
        public static void PlayOut(GameObject go, float duration = 0.6f, Color? edge = null, Action onComplete = null)
            => Begin(go, fadeIn: false, duration, edge, onComplete, keepStateOnOut: false);

        /// <summary>Dissolve out for DEATH on a reused object (mob respawns in place): burn away but KEEP the captured
        /// state so a later <see cref="PlayIn"/> can revive it. The body ends invisible (fully clipped), not destroyed.</summary>
        public static void PlayDeath(GameObject go, float duration = 0.6f, Color? edge = null)
            => Begin(go, fadeIn: false, duration, edge, null, keepStateOnOut: true);

        private static void Begin(GameObject go, bool fadeIn, float duration, Color? edge, Action onComplete, bool keepStateOnOut)
        {
            if (go == null) { onComplete?.Invoke(); return; }
            if (DissolveShader == null)
            {
                Debug.LogWarning("[Dissolve] shader 'Arcane/DissolveURP' não encontrado — pulando efeito.");
                onComplete?.Invoke();
                return;
            }
            var fx = go.GetComponent<DissolveEffect>() ?? go.AddComponent<DissolveEffect>();
            fx.Run(fadeIn, Mathf.Max(0.05f, duration), edge ?? DefaultEdge, onComplete, keepStateOnOut);
        }

        private void Run(bool fadeIn, float duration, Color edge, Action onComplete, bool keepStateOnOut)
        {
            StopAllCoroutines();
            bool fresh = _caps.Count == 0;
            if (fresh) Capture(edge);                    // capture once; reused across death ↔ revive
            if (_caps.Count == 0) { onComplete?.Invoke(); return; } // nothing to dissolve
            if (fresh) SetAmount(fadeIn ? 1f : 0f);      // a fresh effect starts at the extreme (reuse keeps current _amount)
            _pendingComplete = onComplete;               // remembered so an interrupted out-effect still destroys (no leak)
            StartCoroutine(Animate(fadeIn, duration, keepStateOnOut));
        }

        private void Capture(Color edge)
        {
            _caps.Clear();
            var renderers = GetComponentsInChildren<Renderer>(true);
            for (int r = 0; r < renderers.Length; r++)
            {
                var rend = renderers[r];
                if (rend is ParticleSystemRenderer || rend is SpriteRenderer) continue; // VFX / billboard icons
                if (rend.TryGetComponent<TMPro.TMP_Text>(out _)) continue;               // world-space nameplate/text
                if (rend.GetComponentInParent<Canvas>() != null) continue;               // anything under a UI canvas (HP bar)
                var orig = rend.sharedMaterials;
                var diss = new Material[orig.Length];
                for (int i = 0; i < orig.Length; i++)
                {
                    var m = new Material(DissolveShader);
                    var o = orig[i];
                    if (o != null)
                    {
                        Texture baseTex = o.HasProperty("_BaseMap") ? o.GetTexture("_BaseMap")
                                        : o.HasProperty("_MainTex") ? o.GetTexture("_MainTex") : null;
                        if (baseTex != null) m.SetTexture("_BaseMap", baseTex);
                        Color col = o.HasProperty("_BaseColor") ? o.GetColor("_BaseColor")
                                  : o.HasProperty("_Color") ? o.GetColor("_Color") : Color.white;
                        m.SetColor("_BaseColor", col);
                    }
                    m.SetTexture("_NoiseTex", Noise());
                    m.SetColor("_EdgeColor", edge);
                    m.SetFloat("_NoiseScale", 2f);
                    m.SetFloat("_DissolveAmount", 0f);
                    diss[i] = m;
                }
                rend.sharedMaterials = diss;
                _caps.Add(new Captured { Renderer = rend, Original = orig, Dissolve = diss });
            }
        }

        private IEnumerator Animate(bool fadeIn, float duration, bool keepStateOnOut)
        {
            float from = _amount;
            float to = fadeIn ? 0f : 1f;        // in → solid · out → gone
            float t = Mathf.Approximately(from, to) ? duration : 0f; // already there → skip
            while (t < duration)
            {
                t += Time.deltaTime;
                SetAmount(Mathf.Lerp(from, to, Mathf.Clamp01(t / duration)));
                yield return null;
            }
            SetAmount(to);

            Action cb = _pendingComplete; _pendingComplete = null; // consumed → OnDisable won't re-fire it
            if (fadeIn) { Restore(); cb?.Invoke(); Destroy(this); } // solid again → real materials back, done
            else cb?.Invoke(); // despawn → caller destroys (OnDestroy frees mats) · death → null, state kept for revive
        }

        // If the effect is interrupted (GameObject deactivated mid-dissolve), still run the pending destroy so the
        // object + its material instances aren't leaked. (OnDestroy frees the mats either way.)
        private void OnDisable()
        {
            if (_pendingComplete == null) return;
            Action cb = _pendingComplete; _pendingComplete = null;
            cb();
        }

        private void SetAmount(float a)
        {
            _amount = a;
            for (int i = 0; i < _caps.Count; i++)
            {
                var d = _caps[i].Dissolve;
                for (int j = 0; j < d.Length; j++) if (d[j] != null) d[j].SetFloat("_DissolveAmount", a);
            }
        }

        private void Restore()
        {
            for (int i = 0; i < _caps.Count; i++)
            {
                var c = _caps[i];
                if (c.Renderer != null) c.Renderer.sharedMaterials = c.Original;
                DestroyMats(c.Dissolve);
            }
            _caps.Clear();
            _amount = 0f;
        }

        private void OnDestroy() { for (int i = 0; i < _caps.Count; i++) DestroyMats(_caps[i].Dissolve); }

        private static void DestroyMats(Material[] mats)
        {
            if (mats == null) return;
            for (int j = 0; j < mats.Length; j++) if (mats[j] != null) Destroy(mats[j]);
        }

        // Shared procedural dissolve noise so no texture asset is needed (2-octave Perlin, repeat-wrapped).
        private static Texture2D Noise()
        {
            if (_noise != null) return _noise;
            const int S = 128;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
            var px = new Color32[S * S];
            const float scale = 6f;
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float n = Mathf.PerlinNoise(x / (float)S * scale, y / (float)S * scale) * 0.7f
                            + Mathf.PerlinNoise(x / (float)S * scale * 2f, y / (float)S * scale * 2f) * 0.3f;
                    byte b = (byte)(Mathf.Clamp01(n) * 255f);
                    px[y * S + x] = new Color32(b, b, b, 255);
                }
            tex.SetPixels32(px);
            tex.Apply();
            _noise = tex;
            return tex;
        }
    }
}
