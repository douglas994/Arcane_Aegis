using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ArcaneShared.Models;
using Arcane_Aegis.Network;

namespace Arcane_Aegis.UI
{
    /// <summary>One member line in the <see cref="PartyPanel"/> (name + level + HP bar, a leader marker, and a kick button
    /// the leader sees on everyone else). Build the row prefab by hand and wire these refs; the panel instantiates one per
    /// member and rebinds on every change.</summary>
    public sealed class PartyMemberRow : MonoBehaviour
    {
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text levelLabel;
        [Tooltip("HP bar (Image Type = Filled). Fills 0..1 from the member's HpPercent (live vitals land in Phase B).")]
        [SerializeField] private Image hpFill;
        [Tooltip("Optional: shown only on the party leader's row.")]
        [SerializeField] private GameObject leaderMarker;
        [Tooltip("Optional: kick button — shown only when YOU are the leader and this row isn't you.")]
        [SerializeField] private Button kickButton;

        private uint _characterId;

        public void Bind(in PartyMember m, bool isLeader, bool viewerIsLeader, bool isSelf)
        {
            _characterId = m.CharacterId;

            if (nameLabel != null)
            {
                nameLabel.text = isSelf ? $"{m.Name} (você)" : m.Name;
                nameLabel.color = m.Online ? Color.white : new Color(0.55f, 0.55f, 0.55f); // grey out when offline
            }
            if (levelLabel != null) levelLabel.text = $"Nv {m.Level}";
            if (hpFill != null) hpFill.fillAmount = Mathf.Clamp01(m.HpPercent / 100f);
            if (leaderMarker != null) leaderMarker.SetActive(isLeader);

            if (kickButton != null)
            {
                bool canKick = viewerIsLeader && !isSelf;
                kickButton.gameObject.SetActive(canKick);
                kickButton.onClick.RemoveAllListeners();
                if (canKick) kickButton.onClick.AddListener(() => { if (NetClient.Instance != null) NetClient.Instance.SendPartyKick(_characterId); });
            }
        }
    }
}
