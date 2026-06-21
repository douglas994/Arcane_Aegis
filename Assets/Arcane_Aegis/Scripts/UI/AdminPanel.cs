using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using Arcane_Aegis.Network;

namespace Arcane_Aegis.UI
{
    /// <summary>
    /// In-game GM/admin panel. Toggles with <see cref="toggleKey"/> (default F8) and ONLY for an admin session
    /// (<see cref="ClientSession.IsAdmin"/>, set by the server in S2C_LoginResult). Sends the typed command via
    /// <c>C2S_AdminCommand</c> — the SERVER authorizes + executes (the panel is just a convenient sender, so a
    /// non-admin or a hacked client gets nothing).
    /// Build the UI by hand: wire <see cref="panelRoot"/> + <see cref="input"/>, and point a "Send" button's OnClick
    /// at <see cref="Submit"/>. Examples to type: "give Ice_Sword 1", "spawn Dragon_Boss", "heal".
    /// (Movement is auto-suppressed while the field is focused — UiFocus reads the focused TMP_InputField.)
    /// </summary>
    public sealed class AdminPanel : MonoBehaviour
    {
        [SerializeField] private GameObject panelRoot;   // the panel to show/hide (hidden by default)
        [SerializeField] private TMP_InputField input;   // command field
        [SerializeField] private Key toggleKey = Key.F8;

        private void Start() { if (panelRoot != null) panelRoot.SetActive(false); }

        private void Update()
        {
            if (!ClientSession.IsAdmin) return; // non-admins never get the panel
            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb[toggleKey].wasPressedThisFrame) Toggle();

            // Enter while the field is focused = send.
            if (panelRoot != null && panelRoot.activeSelf && input != null && input.isFocused
                && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame))
                Submit();
        }

        private void Toggle()
        {
            if (panelRoot == null) return;
            bool show = !panelRoot.activeSelf;
            panelRoot.SetActive(show);
            if (show && input != null) { input.text = string.Empty; input.ActivateInputField(); }
        }

        /// <summary>Sends the current command. Wire a "Send" button's OnClick here too.</summary>
        public void Submit()
        {
            if (input == null || string.IsNullOrWhiteSpace(input.text)) return;
            NetClient.Instance?.SendAdminCommand(input.text);
            input.text = string.Empty;
            input.ActivateInputField(); // keep focus for the next command
        }

        // ── one-click quick commands (wire to buttons) ──
        public void QuickHeal() => Run("heal");
        public void QuickGod()  => Run("god");
        public void QuickKill() => Run("kill");
        private static void Run(string cmd) => NetClient.Instance?.SendAdminCommand(cmd);
    }
}
