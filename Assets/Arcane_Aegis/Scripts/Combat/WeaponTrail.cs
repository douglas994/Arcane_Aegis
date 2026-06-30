using System.Collections;
using UnityEngine;

namespace Arcane_Aegis.Combat
{
    /// <summary>
    /// A weapon SWING trail (the "anime" arc). Put this on a weapon prefab — ideally on a child positioned at the blade
    /// EDGE/TIP — with a TrailRenderer (it auto-adds one). It stays OFF and is flashed for a short window only during an
    /// attack swing (EntityAnimation flashes the equipped weapon's trail when an attack/skill plays), so you get a clean
    /// arc during the active frames instead of a permanent smear — the pro approach. Configure the TrailRenderer's
    /// material/width-curve/time on the prefab to taste.
    /// </summary>
    [RequireComponent(typeof(TrailRenderer))]
    public sealed class WeaponTrail : MonoBehaviour
    {
        [SerializeField] private TrailRenderer trail;
        [Tooltip("Quanto tempo (s) o trail fica visível por golpe. ~igual à parte ativa do swing.")]
        [SerializeField] private float defaultSeconds = 0.35f;

        private Coroutine _co;
        private Material _defaultMat;   // the weapon's own look — restored after a skill overrides it
        private Gradient _defaultGrad;

        private void Awake()
        {
            if (trail == null) trail = GetComponent<TrailRenderer>();
            if (trail != null)
            {
                _defaultMat = trail.sharedMaterial;
                _defaultGrad = trail.colorGradient;
                trail.emitting = false; trail.Clear(); // start clean — no rest-pose smear
            }
        }

        /// <summary>Show the trail for the swing window. Optional per-SKILL style override (material / colour gradient) so
        /// the SAME blade can trail red for a fire slash, blue for an ice slash, etc. — restored to the weapon's own look
        /// after the window. Clears first so there's no line from the rest pose. Re-flashing restarts the window (combos).</summary>
        public void Flash(float seconds = -1f, Material material = null, Gradient color = null)
        {
            if (trail == null) return;
            if (_co != null) StopCoroutine(_co);
            _co = StartCoroutine(Run(seconds > 0f ? seconds : defaultSeconds, material, color));
        }

        private IEnumerator Run(float seconds, Material material, Gradient color)
        {
            if (material != null) trail.material = material; // per-skill override
            if (color != null) trail.colorGradient = color;
            trail.Clear();
            trail.emitting = true;
            yield return new WaitForSeconds(seconds);
            trail.emitting = false;
            if (material != null) trail.material = _defaultMat;     // back to the weapon's own look
            if (color != null) trail.colorGradient = _defaultGrad;
            _co = null;
        }
    }
}
