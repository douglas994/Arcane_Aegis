using UnityEngine;

namespace Arcane_Aegis.Combat
{
    /// <summary>
    /// A tiny built-in VFX so skills always show SOMETHING before the artist authors a prefab: a glowing sphere that
    /// scales up and fades out, then self-destroys. Same "visible fallback" idea as the projectile's default sphere.
    /// </summary>
    public sealed class FxFlash : MonoBehaviour
    {
        private float _life, _age, _startScale, _endScale;
        private Material _mat;
        private Color _color;

        /// <summary>Spawns a flash at a world position. Returns the GameObject (auto-destroys after its life).</summary>
        public static GameObject Spawn(Vector3 pos, Color color, float endScale = 1.2f, float life = 0.35f)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            var col = go.GetComponent<Collider>(); if (col != null) Destroy(col);
            go.transform.position = pos;
            go.transform.localScale = Vector3.one * 0.15f;

            var mr = go.GetComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            var mat = new Material(Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color")) { color = color };
            mr.sharedMaterial = mat;

            var f = go.AddComponent<FxFlash>();
            f._life = Mathf.Max(0.05f, life);
            f._startScale = 0.15f;
            f._endScale = endScale;
            f._color = color;
            f._mat = mat;
            return go;
        }

        private void Update()
        {
            _age += Time.deltaTime;
            float t = Mathf.Clamp01(_age / _life);
            transform.localScale = Vector3.one * Mathf.Lerp(_startScale, _endScale, t);
            if (_mat != null) { var c = _color; c.a = _color.a * (1f - t); _mat.color = c; }
            if (_age >= _life)
            {
                if (_mat != null) Destroy(_mat);
                Destroy(gameObject);
            }
        }
    }
}
