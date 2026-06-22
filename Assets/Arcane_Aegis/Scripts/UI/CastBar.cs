using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Arcane_Aegis.UI
{
    /// <summary>
    /// HUD cast bar for the LOCAL player's own ability casts (driven by S2C_AbilityCast.CastMs). Fills over the cast
    /// duration and hides when it completes (or is cancelled). Build the bar by hand and wire the serialized refs;
    /// it starts hidden. Pure presentation — the server stays authoritative over whether the cast actually lands.
    /// </summary>
    public sealed class CastBar : MonoBehaviour
    {
        public static CastBar Instance { get; private set; }

        [Header("Refs (wire by hand)")]
        [SerializeField] private GameObject panelRoot;  // the bar container (starts hidden)
        [SerializeField] private Image fill;            // Image type=Filled, Horizontal
        [SerializeField] private TMP_Text label;        // ability name
        [SerializeField] private Image icon;            // optional ability icon

        private float _start, _end;
        private bool _active;

        private void Awake() { if (Instance == null) Instance = this; if (panelRoot != null) panelRoot.SetActive(false); }
        private void OnDestroy() { if (Instance == this) Instance = null; }

        /// <summary>Show the bar for a cast of <paramref name="seconds"/>. No-op for instant casts (≤0).</summary>
        public void Show(string abilityName, float seconds, Sprite iconSprite = null)
        {
            if (seconds <= 0f) return;
            _start = Time.unscaledTime; _end = _start + seconds; _active = true; // unscaled so a hit-stop doesn't stall the bar
            if (label != null) label.text = string.IsNullOrEmpty(abilityName) ? "Conjurando…" : abilityName;
            if (icon != null) { icon.sprite = iconSprite; icon.enabled = iconSprite != null; }
            if (fill != null) fill.fillAmount = 0f;
            if (panelRoot != null) panelRoot.SetActive(true);
        }

        /// <summary>Hide the bar now (cast finished early / interrupted).</summary>
        public void Cancel() { _active = false; if (panelRoot != null) panelRoot.SetActive(false); }

        private void Update()
        {
            if (!_active) return;
            float t = Mathf.InverseLerp(_start, _end, Time.unscaledTime);
            if (fill != null) fill.fillAmount = t;
            if (Time.unscaledTime >= _end) Cancel();
        }
    }
}
