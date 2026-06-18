using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Arcane_Aegis.Network;

namespace Arcane_Aegis.UI
{
    /// <summary>The chat window: renders <see cref="ChatLog"/> into a single growing text and sends typed lines to the
    /// party channel (Enter or the send button). Build the visuals by hand / via the UIBuilder and wire the refs.
    /// (MVP: every line goes to party chat; channel selection lands when more channels exist.)</summary>
    public sealed class ChatPanel : MonoBehaviour
    {
        [Tooltip("The growing log text (inside a scroll view's Content). Word-wrap on, vertical ContentSizeFitter.")]
        [SerializeField] private TMP_Text logText;
        [Tooltip("Optional: the scroll view, kept pinned to the bottom on new lines.")]
        [SerializeField] private ScrollRect scroll;
        [SerializeField] private TMP_InputField input;
        [SerializeField] private Button sendButton;

        private void OnEnable() { WireInput(); ChatLog.OnChanged += Refresh; Refresh(); }
        private void OnDisable() => ChatLog.OnChanged -= Refresh;

        private void WireInput()
        {
            if (sendButton != null) { sendButton.onClick.RemoveAllListeners(); sendButton.onClick.AddListener(Submit); }
            if (input != null) { input.onSubmit.RemoveAllListeners(); input.onSubmit.AddListener(_ => Submit()); }
        }

        private void Submit()
        {
            if (input == null || NetClient.Instance == null) return;
            string t = input.text?.Trim();
            if (!string.IsNullOrEmpty(t)) NetClient.Instance.SendPartyChat(t);
            input.text = "";
            input.ActivateInputField(); // keep focus to type the next line
        }

        private void Refresh()
        {
            if (logText != null) logText.text = string.Join("\n", ChatLog.Lines);
            if (scroll != null) { Canvas.ForceUpdateCanvases(); scroll.verticalNormalizedPosition = 0f; } // pin to newest
        }
    }
}
