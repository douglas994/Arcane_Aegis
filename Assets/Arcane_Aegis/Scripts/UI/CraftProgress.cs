using UnityEngine;
using UnityEngine.UI;

namespace Arcane_Aegis.UI
{
    /// <summary>Local "crafting…" progress bar. Driven by S2C_CraftStart (begin) and S2C_CraftEnd (the server's
    /// authoritative end). Unlike gathering, crafting does NOT lock movement. You build the bar: put this on a panel,
    /// assign <see cref="panel"/> (toggled root) + <see cref="fill"/> (Image Type = Filled, Horizontal).</summary>
    public sealed class CraftProgress : MonoBehaviour
    {
        public static CraftProgress Instance { get; private set; }

        [SerializeField] private GameObject panel; // bar root (toggled)
        [SerializeField] private Image fill;       // Image Type = Filled, Horizontal

        private float _end, _dur;
        private bool _active;

        private void Awake() { Instance = this; if (panel != null) panel.SetActive(false); }
        private void OnDestroy() { if (Instance == this) Instance = null; }

        /// <summary>Show the bar for <paramref name="seconds"/>. The server ends it (S2C_CraftEnd → <see cref="End"/>); the
        /// timer here only fills the bar + a safety timeout so a lost end packet can't leave it stuck.</summary>
        public void Begin(float seconds)
        {
            _dur = Mathf.Max(0.01f, seconds); _end = Time.time + _dur; _active = true;
            if (panel != null) panel.SetActive(true);
        }

        private void Update()
        {
            if (!_active) return;
            float left = _end - Time.time;
            if (fill != null) fill.fillAmount = Mathf.Clamp01(1f - left / _dur);
            if (left <= -2f) End(); // safety net only — the authoritative S2C_CraftEnd normally ends it
        }

        /// <summary>Ends the bar. Called by CraftEndHandler on the server's authoritative end.</summary>
        public void End()
        {
            _active = false;
            if (panel != null) panel.SetActive(false);
        }
    }
}
