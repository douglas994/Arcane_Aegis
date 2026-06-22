using System.Collections.Generic;
using UnityEngine;
using ArcaneShared.Protocol.ServerToClient;
using Arcane_Aegis.Content;

namespace Arcane_Aegis.UI
{
    /// <summary>
    /// The LOCAL player's buff/debuff icon bar, driven by S2C_StatusEffects (sent owner-only on change). Mirrors the
    /// server's active-status set into pooled <see cref="StatusSlot"/> icons; the radial fill counts the remaining time
    /// down locally between packets, and effects drop when they expire (the server also re-sends on change). Icon /
    /// total duration come from the StatusDefinitionSO (resolved by id). Build the bar by hand and wire the two refs.
    /// </summary>
    public sealed class StatusBar : MonoBehaviour
    {
        public static StatusBar Instance { get; private set; }

        [Header("Refs (wire by hand)")]
        [SerializeField] private Transform container;     // holds the icons (give it a HorizontalLayoutGroup)
        [SerializeField] private StatusSlot slotTemplate; // one slot to clone (kept inactive)

        private struct Active { public float endTime; public float duration; public int stacks; public Sprite icon; }
        private readonly List<Active> _active = new();
        private readonly List<StatusSlot> _pool = new();

        private void Awake() { if (Instance == null) Instance = this; if (slotTemplate != null) slotTemplate.gameObject.SetActive(false); }
        private void OnDestroy() { if (Instance == this) Instance = null; }

        /// <summary>Replace the whole set from a server update.</summary>
        public void Set(StatusEntry[] entries)
        {
            _active.Clear();
            var lib = ContentLibrary.Active;
            if (entries != null)
                foreach (var e in entries)
                {
                    var def = lib != null ? lib.GetStatus(e.StatusId) : null;
                    float remain = e.RemainingMs / 1000f;
                    float dur = (def != null && def.duration > 0f) ? def.duration : Mathf.Max(remain, 0.01f);
                    _active.Add(new Active { endTime = Time.unscaledTime + remain, duration = dur, stacks = e.Stacks, icon = def != null ? def.icon : null });
                }
            Refresh();
        }

        private void Update()
        {
            bool changed = false;
            for (int i = _active.Count - 1; i >= 0; i--)
                if (Time.unscaledTime >= _active[i].endTime) { _active.RemoveAt(i); changed = true; }
            if (changed) Refresh();
            else for (int i = 0; i < _active.Count && i < _pool.Count; i++) _pool[i].Bind(_active[i].icon, Fill01(_active[i]), _active[i].stacks);
        }

        private void Refresh()
        {
            if (slotTemplate == null || container == null) return;
            while (_pool.Count < _active.Count) _pool.Add(Instantiate(slotTemplate, container));
            for (int i = 0; i < _pool.Count; i++)
            {
                bool on = i < _active.Count;
                _pool[i].gameObject.SetActive(on);
                if (on) _pool[i].Bind(_active[i].icon, Fill01(_active[i]), _active[i].stacks);
            }
        }

        private static float Fill01(Active a) => a.duration > 0f ? Mathf.Clamp01((a.endTime - Time.unscaledTime) / a.duration) : 0f;
    }
}
