using UnityEngine;
using UnityEngine.Events;
using TMPro;
using ArcaneShared.Enums;
using ArcaneShared.Models;
using Arcane_Aegis.Network;

namespace Arcane_Aegis.UI
{
    /// <summary>
    /// Server/realm select (two-step): the list is requested from the Master and a <see cref="ServerRow"/> is
    /// spawned per server. Clicking a row MARKS it (highlight); the separate "Select" button confirms via
    /// <see cref="ConfirmSelection"/> → stores it on the MasterClient + fires <see cref="onServerChosen"/>.
    /// Assign a ScrollView's Content as the container + a row prefab; show this panel after login.
    /// </summary>
    public class ServerSelectScreen : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private MasterClient master;
        [SerializeField] private Transform listContainer; // ScrollView → Content
        [SerializeField] private ServerRow rowPrefab;
        [SerializeField] private TMP_Text statusText;

        [Header("On confirm")]
        [SerializeField] private UnityEvent onServerChosen; // wire: go to the Character screen

        private ServerRow _selected;

        private void Awake() { if (master == null) master = FindAnyObjectByType<MasterClient>(); }
        private void OnEnable()
        {
            if (master != null) { master.OnServerList += Populate; master.OnEnterServer += HandleEnterServer; }
            Refresh();
        }

        private void OnDisable()
        {
            if (master != null) { master.OnServerList -= Populate; master.OnEnterServer -= HandleEnterServer; }
        }

        /// <summary>(Re)request the server list. Also hook to a "Refresh" button if you want.</summary>
        public void Refresh()
        {
            if (master == null) { SetStatus("Sem MasterClient na cena."); return; }
            SetStatus("Carregando servidores…");
            master.RequestServerList();
        }

        /// <summary>Hook the "Select" button here. Asks the Master to pick the realm; only after the NetClient
        /// actually connects (authenticated) do we proceed to the Character scene.</summary>
        public void ConfirmSelection()
        {
            if (_selected == null) { SetStatus("Escolha um servidor primeiro."); return; }
            SetStatus($"Selecionando {_selected.ServerName}…");
            master.SelectServer(_selected.Id); // Master validates + pre-registers the session → HandleEnterServer
        }

        // Master answered the pick: connect the persistent NetClient to the realm's address.
        private void HandleEnterServer(bool ok, string host, ushort port, EnterReason reason)
        {
            if (!ok)
            {
                SetStatus(reason == EnterReason.BadToken ? "Sessão expirada — faça login de novo." : "Servidor indisponível.");
                return;
            }
            var net = NetClient.Instance;
            if (net == null) { SetStatus("NetClient ausente (coloque-o na cena de Login)."); return; }
            SetStatus("Entrando no servidor…");
            net.OnConnectedToServer += HandleConnected; // proceed only once truly connected
            net.ConnectTo(host, port);
        }

        private void HandleConnected()
        {
            if (NetClient.Instance != null) NetClient.Instance.OnConnectedToServer -= HandleConnected;
            onServerChosen?.Invoke(); // → SceneLoader.Load("CharacterSelection")
        }

        private void Populate(ServerInfo[] servers)
        {
            ClearList();
            _selected = null;

            if (servers == null || servers.Length == 0) { SetStatus("Sessão inválida ou sem servidores."); return; }

            SetStatus("Escolha um servidor.");
            foreach (var s in servers)
            {
                ServerRow row = Instantiate(rowPrefab, listContainer);
                row.Bind(s, OnRowClicked);
            }
        }

        private void OnRowClicked(ServerRow row)
        {
            if (_selected != null) _selected.SetSelected(false);
            _selected = row;
            row.SetSelected(true);
            SetStatus($"{row.ServerName} selecionado.");
        }

        private void ClearList()
        {
            if (listContainer == null) return;
            for (int i = listContainer.childCount - 1; i >= 0; i--)
                Destroy(listContainer.GetChild(i).gameObject);
        }

        private void SetStatus(string msg) { if (statusText != null) statusText.text = msg; }
    }
}
