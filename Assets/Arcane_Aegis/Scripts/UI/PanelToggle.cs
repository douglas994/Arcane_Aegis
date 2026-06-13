using UnityEngine;

namespace Arcane_Aegis.UI
{
    /// <summary>
    /// Tiny reusable UI toggle: flips a panel's active state. Put it on an ALWAYS-ACTIVE object (e.g. the button),
    /// assign the panel, and wire a Button's OnClick → <see cref="Toggle"/>. (A button can't call a method on an
    /// inactive panel, so the toggle must live on something that stays active.)
    /// </summary>
    public class PanelToggle : MonoBehaviour
    {
        [SerializeField] private GameObject panel;

        /// <summary>Open ↔ close the panel. Hook this to the Button's OnClick.</summary>
        public void Toggle()
        {
            if (panel != null) panel.SetActive(!panel.activeSelf);
        }

        /// <summary>Force open (e.g. for a hotkey/other UI).</summary>
        public void Open() { if (panel != null) panel.SetActive(true); }

        /// <summary>Force close (e.g. an X button inside the panel).</summary>
        public void Close() { if (panel != null) panel.SetActive(false); }
    }
}
