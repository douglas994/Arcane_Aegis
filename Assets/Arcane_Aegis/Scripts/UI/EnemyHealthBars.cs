using System.Collections.Generic;
using UnityEngine;
using Arcane_Aegis.Entities;

namespace Arcane_Aegis.UI
{
    /// <summary>
    /// Pool of floating enemy health bars on the MAIN (screen-space overlay) canvas — the cheap, scalable pattern: one
    /// canvas, a handful of pooled bars that follow damaged monsters via WorldToScreenPoint and auto-hide after a few
    /// seconds. A monster taking damage calls <see cref="Show"/> (from the CombatEventHandler). You build the bar prefab
    /// (an <see cref="EnemyBar"/>) and drop this on the HUD canvas; it does the rest. AoE → many bars at once.
    /// </summary>
    public sealed class EnemyHealthBars : MonoBehaviour
    {
        public static EnemyHealthBars Instance { get; private set; }

        [SerializeField] private EnemyBar barTemplate;   // your bar prefab (lives under the HUD canvas)
        [SerializeField] private Camera cam;             // defaults to Camera.main
        [SerializeField] private int poolSize = 16;      // max bars on screen at once
        [SerializeField] private float showSeconds = 5f; // hide a bar this long after the last hit
        [SerializeField] private float headOffset = 2.2f;// metres above the entity origin

        private sealed class Active { public EnemyBar bar; public EntityView target; public float until; }
        private readonly Dictionary<ushort, Active> _active = new();
        private readonly Stack<EnemyBar> _pool = new();
        private readonly List<ushort> _expired = new();

        private void Awake()
        {
            Instance = this;
            if (cam == null) cam = Camera.main;
            if (barTemplate != null)
            {
                barTemplate.SetVisible(false);
                for (int i = 0; i < poolSize; i++)
                {
                    var b = Instantiate(barTemplate, transform);
                    b.SetVisible(false);
                    _pool.Push(b);
                }
            }
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        /// <summary>Show (or refresh) the bar for a monster that just took damage.</summary>
        public void Show(EntityView view)
        {
            if (view == null) return;
            if (_active.TryGetValue(view.Id, out var a)) { a.until = Time.time + showSeconds; return; }
            if (_pool.Count == 0) return; // pool exhausted → skip (cap reached)
            var bar = _pool.Pop();
            bar.SetVisible(true);
            bar.SetLabel(string.IsNullOrEmpty(view.DisplayName) ? view.name : view.DisplayName);
            _active[view.Id] = new Active { bar = bar, target = view, until = Time.time + showSeconds };
        }

        private void LateUpdate()
        {
            if (_active.Count == 0) return;
            if (cam == null) { cam = Camera.main; if (cam == null) return; }
            float now = Time.time;

            _expired.Clear();
            foreach (var kv in _active)
            {
                Active a = kv.Value;
                if (a.target == null || now >= a.until) { _expired.Add(kv.Key); continue; }

                Vector3 sp = cam.WorldToScreenPoint(a.target.transform.position + Vector3.up * headOffset);
                if (sp.z <= 0f) { a.bar.SetVisible(false); continue; } // behind the camera → hide (keep tracking)
                a.bar.SetVisible(true);
                a.bar.Rt.position = sp;           // screen-space overlay canvas → screen px = position
                a.bar.SetHp(a.target.SnapHp01);
            }
            for (int i = 0; i < _expired.Count; i++) Recycle(_expired[i]);
        }

        private void Recycle(ushort id)
        {
            if (!_active.TryGetValue(id, out var a)) return;
            a.bar.SetVisible(false);
            _pool.Push(a.bar);
            _active.Remove(id);
        }
    }
}
