using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ArcaneShared.Enums;
using ArcaneShared.Models;

namespace Arcane_Aegis.UI
{
    /// <summary>One member line in the <see cref="GuildPanel"/> (name + rank + online + leader-only kick/promote). Build the
    /// row prefab by hand and wire these refs; the panel instantiates one per member and rebinds on every change.</summary>
    public sealed class GuildRow : MonoBehaviour
    {
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text rankLabel;
        [SerializeField] private TMP_Text statusLabel;
        [Tooltip("Promote/demote (Leader only). Cycles Member↔Officer.")]
        [SerializeField] private Button rankButton;
        [Tooltip("Kick (Officer+ over a lower rank).")]
        [SerializeField] private Button kickButton;

        public void Bind(in GuildMemberEntry m, byte viewerRank, bool viewerIsLeader, bool isSelf, Action onKick, Action onToggleRank)
        {
            if (nameLabel != null) { nameLabel.text = isSelf ? $"{m.Name} (você)" : m.Name; nameLabel.color = m.Online ? Color.white : new Color(0.55f, 0.55f, 0.55f); }
            if (rankLabel != null) rankLabel.text = RankName(m.Rank);
            if (statusLabel != null)
            {
                statusLabel.text = m.Online ? "Online" : "Offline";
                statusLabel.color = m.Online ? new Color(0.45f, 0.85f, 0.45f) : new Color(0.5f, 0.5f, 0.5f);
            }

            if (rankButton != null)
            {
                bool canRank = viewerIsLeader && !isSelf && m.Rank < (byte)GuildRank.Leader;
                rankButton.gameObject.SetActive(canRank);
                if (canRank)
                {
                    var label = rankButton.GetComponentInChildren<TMP_Text>();
                    if (label != null) label.text = m.Rank == (byte)GuildRank.Officer ? "Rebaixar" : "Promover";
                    rankButton.onClick.RemoveAllListeners();
                    rankButton.onClick.AddListener(() => onToggleRank?.Invoke());
                }
            }

            if (kickButton != null)
            {
                bool canKick = viewerRank >= (byte)GuildRank.Officer && !isSelf && m.Rank < viewerRank;
                kickButton.gameObject.SetActive(canKick);
                if (canKick)
                {
                    kickButton.onClick.RemoveAllListeners();
                    kickButton.onClick.AddListener(() => onKick?.Invoke());
                }
            }
        }

        private static string RankName(byte rank) => rank switch
        {
            (byte)GuildRank.Leader => "Líder",
            (byte)GuildRank.Officer => "Oficial",
            _ => "Membro",
        };
    }
}
