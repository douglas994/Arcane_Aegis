using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Arcane_Aegis.Network;

namespace Arcane_Aegis.UI
{
    /// <summary>The friends panel: renders the live friend list (one <see cref="FriendRow"/> each from <see cref="FriendState"/>)
    /// and adds-by-name. Requests a fresh list when shown. Build the visuals by hand / via the UIBuilder and wire the refs.</summary>
    public sealed class FriendPanel : MonoBehaviour
    {
        [Tooltip("Row prefab — must have a FriendRow component.")]
        [SerializeField] private GameObject rowPrefab;
        [Tooltip("Parent the friend rows are created under (e.g. a Vertical Layout Group).")]
        [SerializeField] private Transform container;
        [Tooltip("Name to add as a friend.")]
        [SerializeField] private TMP_InputField addName;
        [SerializeField] private Button addButton;

        private readonly List<FriendRow> _rows = new();

        private void OnEnable()
        {
            WireButtons();
            FriendState.OnChanged += Refresh;
            NetClient.Instance?.RequestFriends(); // pull a fresh list (with live online status) when opened
            Refresh();
        }

        private void OnDisable() => FriendState.OnChanged -= Refresh;

        private void WireButtons()
        {
            if (addButton != null) { addButton.onClick.RemoveAllListeners(); addButton.onClick.AddListener(Add); }
        }

        private void Add()
        {
            if (addName == null || NetClient.Instance == null) return;
            string n = addName.text?.Trim();
            if (string.IsNullOrEmpty(n)) return;
            NetClient.Instance.SendFriendAdd(n);
            addName.text = "";
        }

        private void Refresh()
        {
            EnsureCapacity();
            var friends = FriendState.Friends;
            for (int i = 0; i < _rows.Count; i++)
            {
                bool used = i < friends.Length;
                _rows[i].gameObject.SetActive(used);
                if (used)
                {
                    uint id = friends[i].CharacterId;
                    _rows[i].Bind(friends[i], () => NetClient.Instance?.SendFriendRemove(id));
                }
            }
        }

        private void EnsureCapacity()
        {
            if (rowPrefab == null || container == null) return;
            int need = FriendState.Friends.Length;
            while (_rows.Count < need)
            {
                GameObject go = Instantiate(rowPrefab, container);
                go.SetActive(false);
                _rows.Add(go.GetComponent<FriendRow>());
            }
        }
    }
}
