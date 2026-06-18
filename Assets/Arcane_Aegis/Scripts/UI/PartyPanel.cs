using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Arcane_Aegis.Network;

namespace Arcane_Aegis.UI
{
    /// <summary>The party panel: renders the live roster (one <see cref="PartyMemberRow"/> per member from
    /// <see cref="PartyState"/>) and drives leave/disband + invite-by-name. Build the visuals by hand and wire the refs;
    /// rows are instantiated from a prefab (same pattern as the bag). The invite field/button work even when you're not in
    /// a party (inviting auto-creates one). The roster <see cref="rosterRoot"/> hides when you're solo (optional).</summary>
    public sealed class PartyPanel : MonoBehaviour
    {
        [Header("Roster")]
        [Tooltip("Optional: the roster container toggled off when you're not in a party (leave the invite controls outside it).")]
        [SerializeField] private GameObject rosterRoot;
        [Tooltip("Row prefab — must have a PartyMemberRow component.")]
        [SerializeField] private GameObject rowPrefab;
        [Tooltip("Parent the member rows are created under (e.g. a Vertical Layout Group).")]
        [SerializeField] private Transform container;

        [Header("Actions")]
        [Tooltip("Leave the party (visible whenever you're in one).")]
        [SerializeField] private Button leaveButton;
        [Tooltip("Disband the whole party — only interactable/visible when you're the leader.")]
        [SerializeField] private Button disbandButton;

        [Header("Invite")]
        [Tooltip("Name to invite (works even when solo — inviting creates a party).")]
        [SerializeField] private TMP_InputField inviteName;
        [SerializeField] private Button inviteButton;

        private readonly List<PartyMemberRow> _rows = new();

        private void OnEnable() { WireButtons(); PartyState.OnChanged += Refresh; Refresh(); }
        private void OnDisable() => PartyState.OnChanged -= Refresh;

        private void WireButtons()
        {
            if (leaveButton != null)
            {
                leaveButton.onClick.RemoveAllListeners();
                leaveButton.onClick.AddListener(() => NetClient.Instance?.SendPartyLeave());
            }
            if (disbandButton != null)
            {
                disbandButton.onClick.RemoveAllListeners();
                disbandButton.onClick.AddListener(() => NetClient.Instance?.SendPartyDisband());
            }
            if (inviteButton != null)
            {
                inviteButton.onClick.RemoveAllListeners();
                inviteButton.onClick.AddListener(SendInvite);
            }
        }

        private void SendInvite()
        {
            if (inviteName == null || NetClient.Instance == null) return;
            string n = inviteName.text?.Trim();
            if (string.IsNullOrEmpty(n)) return;
            NetClient.Instance.SendPartyInvite(n);
            inviteName.text = "";
        }

        private void Refresh()
        {
            EnsureCapacity();

            bool inParty = PartyState.InParty;
            if (rosterRoot != null) rosterRoot.SetActive(inParty);
            if (leaveButton != null) leaveButton.gameObject.SetActive(inParty);
            if (disbandButton != null) disbandButton.gameObject.SetActive(PartyState.AmLeader);

            var members = PartyState.Members;
            uint leader = PartyState.LeaderCharacterId;
            uint me = PartyState.MyCharacterId;
            bool amLeader = PartyState.AmLeader;

            for (int i = 0; i < _rows.Count; i++)
            {
                bool used = i < members.Length;
                _rows[i].gameObject.SetActive(used);
                if (used) _rows[i].Bind(members[i], members[i].CharacterId == leader, amLeader, members[i].CharacterId == me);
            }
        }

        // Pool rows up to the current member count (parties are tiny; rows are reused across refreshes).
        private void EnsureCapacity()
        {
            if (rowPrefab == null || container == null) return;
            int need = PartyState.Members.Length;
            while (_rows.Count < need)
            {
                GameObject go = Instantiate(rowPrefab, container);
                go.SetActive(false);
                _rows.Add(go.GetComponent<PartyMemberRow>());
            }
        }
    }
}
