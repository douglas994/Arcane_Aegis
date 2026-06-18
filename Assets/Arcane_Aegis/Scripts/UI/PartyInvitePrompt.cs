using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Arcane_Aegis.Network;

namespace Arcane_Aegis.UI
{
    /// <summary>Pop-up shown when someone invites you to a party (driven by <see cref="PartyInviteState"/>). Accept/decline
    /// send C2S_PartyResponse and clear the prompt. Build the visuals by hand and wire the refs.</summary>
    public sealed class PartyInvitePrompt : MonoBehaviour
    {
        [Tooltip("The pop-up root toggled on while an invite is pending.")]
        [SerializeField] private GameObject panel;
        [Tooltip("Optional: the message label (shows who invited you).")]
        [SerializeField] private TMP_Text message;
        [SerializeField] private Button acceptButton;
        [SerializeField] private Button declineButton;

        private void OnEnable() { WireButtons(); PartyInviteState.OnChanged += Refresh; Refresh(); }
        private void OnDisable() => PartyInviteState.OnChanged -= Refresh;

        private void WireButtons()
        {
            if (acceptButton != null) { acceptButton.onClick.RemoveAllListeners(); acceptButton.onClick.AddListener(() => Respond(true)); }
            if (declineButton != null) { declineButton.onClick.RemoveAllListeners(); declineButton.onClick.AddListener(() => Respond(false)); }
        }

        private void Respond(bool accept)
        {
            NetClient.Instance?.SendPartyResponse(accept);
            PartyInviteState.Clear();
        }

        private void Refresh()
        {
            bool has = PartyInviteState.HasPending;
            if (panel != null) panel.SetActive(has);
            if (has && message != null) message.text = $"{PartyInviteState.InviterName} convidou você para um grupo.";
        }
    }
}
