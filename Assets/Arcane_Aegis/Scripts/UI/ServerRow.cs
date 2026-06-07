using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ArcaneShared.Enums;
using ArcaneShared.Models;

namespace Arcane_Aegis.UI
{
    /// <summary>
    /// One row in the server list — put this on the row PREFAB. Clicking it marks it as the pending selection
    /// (the screen highlights it); a separate "Select" button confirms. <see cref="highlight"/> is an optional
    /// object toggled when this row is the selected one.
    /// </summary>
    public class ServerRow : MonoBehaviour
    {
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text infoLabel;   // status + population (optional)
        [SerializeField] private Button button;
        [SerializeField] private GameObject highlight; // optional "selected" frame/background

        public byte Id { get; private set; }
        public string ServerName { get; private set; } = string.Empty;

        public void Bind(ServerInfo info, Action<ServerRow> onClick)
        {
            Id = info.Id;
            ServerName = info.Name;
            var status = (ServerStatus)info.Status;

            if (nameLabel != null) nameLabel.text = info.Name;
            if (infoLabel != null) infoLabel.text = $"{StatusText(status)} · {info.PopPct}%";
            SetSelected(false);

            if (button != null)
            {
                button.interactable = status is ServerStatus.Online or ServerStatus.Full;
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => onClick(this));
            }
        }

        public void SetSelected(bool on)
        {
            if (highlight != null) highlight.SetActive(on);
        }

        private static string StatusText(ServerStatus s) => s switch
        {
            ServerStatus.Online      => "Online",
            ServerStatus.Full        => "Cheio",
            ServerStatus.Maintenance => "Manutenção",
            _                        => "Offline",
        };
    }
}
