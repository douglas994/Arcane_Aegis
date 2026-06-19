using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Arcane_Aegis.Network;

namespace Arcane_Aegis.UI
{
    /// <summary>Pop-up shown when someone invites you to a guild (driven by <see cref="GuildInviteState"/>). Accept/decline
    /// send C2S_GuildResponse and clear the prompt. Build the visuals by hand and wire the refs.</summary>
    public sealed class GuildInvitePrompt : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private TMP_Text message;
        [SerializeField] private Button acceptButton;
        [SerializeField] private Button declineButton;

        private void OnEnable() { WireButtons(); GuildInviteState.OnChanged += Refresh; Refresh(); }
        private void OnDisable() => GuildInviteState.OnChanged -= Refresh;

        private void WireButtons()
        {
            if (acceptButton != null) { acceptButton.onClick.RemoveAllListeners(); acceptButton.onClick.AddListener(() => Respond(true)); }
            if (declineButton != null) { declineButton.onClick.RemoveAllListeners(); declineButton.onClick.AddListener(() => Respond(false)); }
        }

        private void Respond(bool accept)
        {
            NetClient.Instance?.SendGuildResponse(accept);
            GuildInviteState.Clear();
        }

        private void Refresh()
        {
            bool has = GuildInviteState.HasPending;
            if (panel != null) panel.SetActive(has);
            if (has && message != null) message.text = $"{GuildInviteState.InviterName} convidou você para a guilda {GuildInviteState.GuildName}.";
        }
    }
}
