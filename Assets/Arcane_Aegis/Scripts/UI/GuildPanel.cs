using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ArcaneShared.Enums;
using Arcane_Aegis.Network;

namespace Arcane_Aegis.UI
{
    /// <summary>The guild panel: two modes — when you're NOT in a guild it shows a create box; when you ARE, it shows the
    /// roster + invite/leave (+ disband for the leader). Driven by <see cref="GuildState"/>. Build the visuals by hand /
    /// via the UIBuilder and wire the refs. Requests a fresh roster when opened.</summary>
    public sealed class GuildPanel : MonoBehaviour
    {
        [Header("Create (shown when not in a guild)")]
        [SerializeField] private GameObject createRoot;
        [SerializeField] private TMP_InputField createName;
        [SerializeField] private Button createButton;

        [Header("Guild (shown when in a guild)")]
        [SerializeField] private GameObject guildRoot;
        [SerializeField] private TMP_Text guildNameLabel;
        [SerializeField] private GameObject rowPrefab;
        [SerializeField] private Transform container;
        [SerializeField] private TMP_InputField inviteName;
        [SerializeField] private Button inviteButton;
        [SerializeField] private Button leaveButton;
        [SerializeField] private Button disbandButton;

        private readonly List<GuildRow> _rows = new();

        private void OnEnable()
        {
            WireButtons();
            GuildState.OnChanged += Refresh;
            NetClient.Instance?.RequestGuild();
            Refresh();
        }

        private void OnDisable() => GuildState.OnChanged -= Refresh;

        private void WireButtons()
        {
            if (createButton != null) { createButton.onClick.RemoveAllListeners(); createButton.onClick.AddListener(Create); }
            if (inviteButton != null) { inviteButton.onClick.RemoveAllListeners(); inviteButton.onClick.AddListener(Invite); }
            if (leaveButton != null) { leaveButton.onClick.RemoveAllListeners(); leaveButton.onClick.AddListener(() => NetClient.Instance?.SendGuildLeave()); }
            if (disbandButton != null) { disbandButton.onClick.RemoveAllListeners(); disbandButton.onClick.AddListener(() => NetClient.Instance?.SendGuildDisband()); }
        }

        private void Create()
        {
            if (createName == null || NetClient.Instance == null) return;
            string n = createName.text?.Trim();
            if (!string.IsNullOrEmpty(n)) { NetClient.Instance.SendGuildCreate(n); createName.text = ""; }
        }

        private void Invite()
        {
            if (inviteName == null || NetClient.Instance == null) return;
            string n = inviteName.text?.Trim();
            if (!string.IsNullOrEmpty(n)) { NetClient.Instance.SendGuildInvite(n); inviteName.text = ""; }
        }

        private void Refresh()
        {
            bool inGuild = GuildState.InGuild;
            if (createRoot != null) createRoot.SetActive(!inGuild);
            if (guildRoot != null) guildRoot.SetActive(inGuild);
            if (disbandButton != null) disbandButton.gameObject.SetActive(GuildState.AmLeader);
            if (guildNameLabel != null) guildNameLabel.text = GuildState.GuildName;

            EnsureCapacity();
            var members = GuildState.Members;
            byte myRank = GuildState.MyRank;
            bool amLeader = GuildState.AmLeader;
            uint me = GuildState.MyCharacterId;

            for (int i = 0; i < _rows.Count; i++)
            {
                bool used = i < members.Length;
                _rows[i].gameObject.SetActive(used);
                if (!used) continue;
                uint id = members[i].CharacterId;
                bool isSelf = id == me;
                byte curRank = members[i].Rank;
                _rows[i].Bind(members[i], myRank, amLeader, isSelf,
                    () => NetClient.Instance?.SendGuildKick(id),
                    () => NetClient.Instance?.SendGuildSetRank(id, curRank == (byte)GuildRank.Officer ? (byte)GuildRank.Member : (byte)GuildRank.Officer));
            }
        }

        private void EnsureCapacity()
        {
            if (rowPrefab == null || container == null) return;
            int need = GuildState.Members.Length;
            while (_rows.Count < need)
            {
                GameObject go = Instantiate(rowPrefab, container);
                go.SetActive(false);
                _rows.Add(go.GetComponent<GuildRow>());
            }
        }
    }
}
