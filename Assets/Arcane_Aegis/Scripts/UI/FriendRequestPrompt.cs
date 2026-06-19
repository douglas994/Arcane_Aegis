using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Arcane_Aegis.Network;

namespace Arcane_Aegis.UI
{
    /// <summary>Pop-up shown when someone sends a friend request (driven by <see cref="FriendRequestState"/>). Accept/decline
    /// send C2S_FriendResponse and clear the prompt. Build the visuals by hand and wire the refs.</summary>
    public sealed class FriendRequestPrompt : MonoBehaviour
    {
        [Tooltip("The pop-up root toggled on while a request is pending.")]
        [SerializeField] private GameObject panel;
        [Tooltip("Optional: the message label (shows who requested).")]
        [SerializeField] private TMP_Text message;
        [SerializeField] private Button acceptButton;
        [SerializeField] private Button declineButton;

        private void OnEnable() { WireButtons(); FriendRequestState.OnChanged += Refresh; Refresh(); }
        private void OnDisable() => FriendRequestState.OnChanged -= Refresh;

        private void WireButtons()
        {
            if (acceptButton != null) { acceptButton.onClick.RemoveAllListeners(); acceptButton.onClick.AddListener(() => Respond(true)); }
            if (declineButton != null) { declineButton.onClick.RemoveAllListeners(); declineButton.onClick.AddListener(() => Respond(false)); }
        }

        private void Respond(bool accept)
        {
            NetClient.Instance?.SendFriendResponse(accept);
            FriendRequestState.Clear();
        }

        private void Refresh()
        {
            bool has = FriendRequestState.HasPending;
            if (panel != null) panel.SetActive(has);
            if (has && message != null) message.text = $"{FriendRequestState.RequesterName} quer te adicionar como amigo.";
        }
    }
}
