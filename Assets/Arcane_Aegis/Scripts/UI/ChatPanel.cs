using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ArcaneShared.Enums;
using Arcane_Aegis.Network;

namespace Arcane_Aegis.UI
{
    /// <summary>The chat window: renders <see cref="ChatLog"/> and sends typed lines on the selected channel. A channel
    /// button cycles Global → Zona → Grupo; slash commands override per-message: <c>/g</c> global, <c>/z</c> zone,
    /// <c>/p</c> party, <c>/w Nome msg</c> whisper. Build the visuals by hand / via the UIBuilder and wire the refs.</summary>
    public sealed class ChatPanel : MonoBehaviour
    {
        [Tooltip("The growing log text (inside a scroll view's Content). Word-wrap on, vertical ContentSizeFitter.")]
        [SerializeField] private TMP_Text logText;
        [Tooltip("Optional: the scroll view, kept pinned to the bottom on new lines.")]
        [SerializeField] private ScrollRect scroll;
        [SerializeField] private TMP_InputField input;
        [SerializeField] private Button sendButton;
        [Tooltip("Optional: cycles the active channel (Global/Zona/Grupo).")]
        [SerializeField] private Button channelButton;
        [Tooltip("Optional: shows the active channel name (on the channel button).")]
        [SerializeField] private TMP_Text channelLabel;

        private ChatChannel _channel = ChatChannel.Global; // default outgoing channel (whisper only via /w)

        /// <summary>So other UI (e.g. the friends panel) can start a whisper without retyping "/w Nome".</summary>
        public static ChatPanel Instance { get; private set; }

        private void OnEnable() { Instance = this; WireInput(); ChatLog.OnChanged += Refresh; UpdateChannelLabel(); Refresh(); }
        private void OnDisable() { if (Instance == this) Instance = null; ChatLog.OnChanged -= Refresh; }

        /// <summary>Prefill the input with "/w {name} " and focus it — one click to whisper, then just type + Enter.</summary>
        public void BeginWhisper(string targetName)
        {
            if (input == null || string.IsNullOrEmpty(targetName)) return;
            input.text = $"/w {targetName} ";
            input.ActivateInputField();
            input.caretPosition = input.text.Length;
            input.stringPosition = input.text.Length;
        }

        private void WireInput()
        {
            if (sendButton != null) { sendButton.onClick.RemoveAllListeners(); sendButton.onClick.AddListener(Submit); }
            if (input != null) { input.onSubmit.RemoveAllListeners(); input.onSubmit.AddListener(_ => Submit()); }
            if (channelButton != null) { channelButton.onClick.RemoveAllListeners(); channelButton.onClick.AddListener(CycleChannel); }
        }

        private void Submit()
        {
            if (input == null || NetClient.Instance == null) return;
            string raw = input.text?.Trim();
            input.text = "";
            input.ActivateInputField(); // keep focus to type the next line
            if (string.IsNullOrEmpty(raw)) return;

            // /w Nome mensagem  → whisper
            if (raw.StartsWith("/w ") || raw.StartsWith("/sussurro "))
            {
                string rest = raw.Substring(raw.IndexOf(' ') + 1).TrimStart();
                int sp = rest.IndexOf(' ');
                if (sp <= 0) return; // need "Nome mensagem"
                string target = rest.Substring(0, sp);
                string msg = rest.Substring(sp + 1).Trim();
                if (msg.Length > 0) NetClient.Instance.SendChat(ChatChannel.Whisper, target, msg);
                return;
            }
            if (TryPrefix(raw, "/g ", out var g)) { NetClient.Instance.SendChat(ChatChannel.Global, "", g); return; }
            if (TryPrefix(raw, "/z ", out var z)) { NetClient.Instance.SendChat(ChatChannel.Zone, "", z); return; }
            if (TryPrefix(raw, "/p ", out var p)) { NetClient.Instance.SendChat(ChatChannel.Party, "", p); return; }
            if (TryPrefix(raw, "/gu ", out var gu)) { NetClient.Instance.SendChat(ChatChannel.Guild, "", gu); return; }

            NetClient.Instance.SendChat(_channel, "", raw); // selected channel
        }

        private static bool TryPrefix(string s, string prefix, out string rest)
        {
            if (s.StartsWith(prefix)) { rest = s.Substring(prefix.Length).Trim(); return rest.Length > 0; }
            rest = null;
            return false;
        }

        private void CycleChannel()
        {
            _channel = _channel switch
            {
                ChatChannel.Global => ChatChannel.Zone,
                ChatChannel.Zone => ChatChannel.Party,
                ChatChannel.Party => ChatChannel.Guild,
                _ => ChatChannel.Global,
            };
            UpdateChannelLabel();
        }

        private void UpdateChannelLabel()
        {
            if (channelLabel == null) return;
            channelLabel.text = _channel switch
            {
                ChatChannel.Zone => "Zona",
                ChatChannel.Party => "Grupo",
                ChatChannel.Guild => "Guilda",
                _ => "Global",
            };
        }

        private void Refresh()
        {
            if (logText != null) logText.text = string.Join("\n", ChatLog.Lines);
            if (scroll != null) { Canvas.ForceUpdateCanvases(); scroll.verticalNormalizedPosition = 0f; } // pin to newest
        }
    }
}
