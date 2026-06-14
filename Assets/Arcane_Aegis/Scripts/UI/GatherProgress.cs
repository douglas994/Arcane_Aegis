using UnityEngine;
using UnityEngine.UI;
using Arcane_Aegis.Controllers.Locomotion;

namespace Arcane_Aegis.UI
{
    /// <summary>
    /// Local "gathering…" progress bar. Driven by S2C_GatherStart (the GatherStartHandler calls <see cref="Begin"/> for
    /// the local player). Fills over the gather time, locks movement while active, hides + unlocks when done. You build
    /// the bar: put this on a panel, assign <see cref="panel"/> (the root, toggled) + <see cref="fill"/> (Filled image).
    /// </summary>
    public sealed class GatherProgress : MonoBehaviour
    {
        public static GatherProgress Instance { get; private set; }

        [SerializeField] private GameObject panel; // bar root (toggled)
        [SerializeField] private Image fill;       // Image Type = Filled, Horizontal

        private float _end, _dur;
        private bool _active;
        private LocomotionStateMachine _fsm;

        private void Awake() { Instance = this; if (panel != null) panel.SetActive(false); }
        private void OnDestroy() { if (Instance == this) Instance = null; }

        /// <summary>Start the bar for <paramref name="seconds"/> and lock the player's movement. The server decides when it
        /// truly ends (S2C_GatherEnd → <see cref="End"/>); the timer here only fills the bar + a safety timeout so a lost
        /// end packet can't lock movement forever.</summary>
        public void Begin(float seconds, LocomotionStateMachine fsm)
        {
            _fsm = fsm; _dur = Mathf.Max(0.01f, seconds); _end = Time.time + _dur; _active = true;
            if (panel != null) panel.SetActive(true);
            if (_fsm != null) _fsm.SetGathering(true);
        }

        private void Update()
        {
            if (!_active) return;
            float left = _end - Time.time;
            if (fill != null) fill.fillAmount = Mathf.Clamp01(1f - left / _dur);
            if (left <= -2f) End(); // safety net only — normally the authoritative S2C_GatherEnd ends it first
        }

        /// <summary>Ends the bar + unlocks movement. Called by the GatherEndHandler on the server's authoritative end.</summary>
        public void End()
        {
            _active = false;
            if (panel != null) panel.SetActive(false);
            if (_fsm != null) { _fsm.SetGathering(false); _fsm = null; }
        }
    }
}
