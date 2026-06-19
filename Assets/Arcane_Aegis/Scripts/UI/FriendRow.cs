using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ArcaneShared.Models;

namespace Arcane_Aegis.UI
{
    /// <summary>One friend line in the <see cref="FriendPanel"/> (name + online status + a remove button). Build the row
    /// prefab by hand and wire these refs; the panel instantiates one per friend and rebinds on every change.</summary>
    public sealed class FriendRow : MonoBehaviour
    {
        [SerializeField] private TMP_Text nameLabel;
        [Tooltip("Optional: 'Online'/'Offline' status.")]
        [SerializeField] private TMP_Text statusLabel;
        [Tooltip("Optional: one-click whisper — prefills the chat with /w to this friend.")]
        [SerializeField] private Button whisperButton;
        [Tooltip("Optional: remove-friend button.")]
        [SerializeField] private Button removeButton;

        public void Bind(in FriendEntry f, Action onRemove)
        {
            if (nameLabel != null) nameLabel.text = f.Name;
            if (statusLabel != null)
            {
                statusLabel.text = f.Online ? "Online" : "Offline";
                statusLabel.color = f.Online ? new Color(0.45f, 0.85f, 0.45f) : new Color(0.55f, 0.55f, 0.55f);
            }
            if (whisperButton != null)
            {
                string targetName = f.Name;
                whisperButton.onClick.RemoveAllListeners();
                whisperButton.onClick.AddListener(() => { if (ChatPanel.Instance != null) ChatPanel.Instance.BeginWhisper(targetName); });
            }
            if (removeButton != null)
            {
                removeButton.onClick.RemoveAllListeners();
                removeButton.onClick.AddListener(() => onRemove?.Invoke());
            }
        }
    }
}
