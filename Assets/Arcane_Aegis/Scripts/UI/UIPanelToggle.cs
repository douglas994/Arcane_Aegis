using UnityEngine;
using UnityEngine.InputSystem;

namespace Arcane_Aegis.UI
{
    /// <summary>Opens/closes a UI panel — by a hotkey and/or a Button. Drag the panel in, optionally set a key (new Input
    /// System), and/or wire a Button's OnClick to <see cref="Toggle"/>. Reusable for the bag, professions, etc.</summary>
    public sealed class UIPanelToggle : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [Tooltip("Optional hotkey to toggle the panel (e.g. P for professions, I for the bag). None = button only.")]
        [SerializeField] private Key hotkey = Key.None;
        [Tooltip("Hide the panel on start so it opens closed.")]
        [SerializeField] private bool startHidden = true;

        private void Awake() { if (panel != null && startHidden) panel.SetActive(false); }

        private void Update()
        {
            if (hotkey == Key.None) return;
            var kb = Keyboard.current;
            if (kb != null && kb[hotkey].wasPressedThisFrame) Toggle();
        }

        /// <summary>Wire this to a Button's OnClick (and/or it fires on the hotkey).</summary>
        public void Toggle() { if (panel != null) panel.SetActive(!panel.activeSelf); }
        public void Show()   { if (panel != null) panel.SetActive(true); }
        public void Hide()   { if (panel != null) panel.SetActive(false); }
    }
}
