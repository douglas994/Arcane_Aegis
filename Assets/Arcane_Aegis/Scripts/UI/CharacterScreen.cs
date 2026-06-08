using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using TMPro;
using ArcaneShared.Enums;
using ArcaneShared.Models;
using Arcane_Aegis.Network;

namespace Arcane_Aegis.UI
{
    /// <summary>
    /// Character lobby: lists this account's characters (select one) and creates new ones (race/class are
    /// DATA-DRIVEN from the server's S2C_CreationData → TMP dropdowns). Uses the <see cref="NetClient"/> (UDP to
    /// the ArcaneServer). Buttons: Refresh, ShowCreate, ShowList, OnCreateClicked, OnEnterClicked.
    /// </summary>
    public class CharacterScreen : MonoBehaviour
    {
        [Header("Net")]
        [SerializeField] private NetClient net;

        [Header("Panels")]
        [SerializeField] private GameObject listPanel;
        [SerializeField] private GameObject createPanel;

        [Header("List")]
        [SerializeField] private Transform listContainer;  // ScrollView Content
        [SerializeField] private CharacterRow rowPrefab;
        [SerializeField] private TMP_Text listStatus;

        [Header("Create")]
        [SerializeField] private TMP_InputField nameField;
        [SerializeField] private TMP_Dropdown raceDropdown;
        [SerializeField] private TMP_Dropdown classDropdown;
        [SerializeField] private TMP_Text createStatus;

        [Header("On enter (3d)")]
        [SerializeField] private UnityEvent onCharacterChosen; // wire: enter the world

        private CreationOption[] _races = Array.Empty<CreationOption>();
        private CreationOption[] _classes = Array.Empty<CreationOption>();
        private CharacterRow _selected;

        private void Awake() { if (net == null) net = NetClient.Instance ?? FindAnyObjectByType<NetClient>(); }

        private void OnEnable()
        {
            if (net != null)
            {
                net.OnCharacterList += Populate;
                net.OnCreationData += FillCreation;
                net.OnCharacterCreateResult += OnCreated;
                net.OnConnectedToServer += OnServerReady;
            }
            ShowList();

            // Request only once connected (the connect is async). If already connected, go now.
            if (net != null && net.Connected) OnServerReady();
            else SetText(listStatus, "Conectando ao servidor…");
        }

        private void OnDisable()
        {
            if (net != null)
            {
                net.OnCharacterList -= Populate;
                net.OnCreationData -= FillCreation;
                net.OnCharacterCreateResult -= OnCreated;
                net.OnConnectedToServer -= OnServerReady;
            }
        }

        private void OnServerReady()
        {
            net.RequestCreationData();
            Refresh();
        }

        // ── panels (wire to buttons) ──
        public void ShowList()   { Set(listPanel, true);  Set(createPanel, false); }
        public void ShowCreate() { Set(listPanel, false); Set(createPanel, true);  }

        /// <summary>(Re)request this account's characters.</summary>
        public void Refresh()
        {
            if (net == null || !net.Connected) { SetText(listStatus, "Conectando ao servidor…"); return; }
            SetText(listStatus, "Carregando personagens…");
            net.RequestCharacters();
        }

        /// <summary>Hook to the "Criar" button (in the create panel).</summary>
        public void OnCreateClicked()
        {
            if (net == null || !net.Connected) { SetText(createStatus, "Conectando…"); return; }
            if (_races.Length == 0 || _classes.Length == 0) { SetText(createStatus, "Aguarde o catálogo…"); return; }

            string name = nameField != null ? nameField.text : string.Empty;
            string race = _races[Mathf.Clamp(raceDropdown != null ? raceDropdown.value : 0, 0, _races.Length - 1)].Id;
            string cls = _classes[Mathf.Clamp(classDropdown != null ? classDropdown.value : 0, 0, _classes.Length - 1)].Id;

            SetText(createStatus, "Criando…");
            net.CreateCharacter(name, race, cls);
        }

        /// <summary>Hook to the "Entrar/Jogar" button (in the list panel).</summary>
        public void OnEnterClicked()
        {
            if (_selected == null) { SetText(listStatus, "Escolha um personagem."); return; }
            ClientSession.CharacterId = _selected.CharacterId;
            SetText(listStatus, $"Entrando com {_selected.CharacterName}…");
            onCharacterChosen?.Invoke(); // 3d: spawn into the world
        }

        // ── events from the server ──
        private void Populate(CharacterSummary[] chars)
        {
            ClearList();
            _selected = null;
            if (chars.Length == 0) { SetText(listStatus, "Nenhum personagem. Crie um!"); return; }
            SetText(listStatus, string.Empty);
            foreach (var c in chars)
            {
                CharacterRow row = Instantiate(rowPrefab, listContainer);
                row.Bind(c, OnRowClicked);
            }
        }

        private void OnRowClicked(CharacterRow row)
        {
            if (_selected != null) _selected.SetSelected(false);
            _selected = row;
            row.SetSelected(true);
            SetText(listStatus, $"{row.CharacterName} selecionado.");
        }

        private void FillCreation(CreationOption[] races, CreationOption[] classes)
        {
            _races = races;
            _classes = classes;
            FillDropdown(raceDropdown, races);
            FillDropdown(classDropdown, classes);
        }

        private void OnCreated(CharCreateResult result)
        {
            if (result == CharCreateResult.Ok)
            {
                SetText(createStatus, "Criado!");
                ShowList();
                Refresh();
                return;
            }
            SetText(createStatus, Message(result));
        }

        // ── helpers ──
        private static void FillDropdown(TMP_Dropdown dd, CreationOption[] opts)
        {
            if (dd == null) return;
            dd.ClearOptions();
            var names = new List<string>(opts.Length);
            foreach (var o in opts) names.Add(o.Name);
            dd.AddOptions(names);
        }

        private static string Message(CharCreateResult r) => r switch
        {
            CharCreateResult.NameTaken    => "Esse nome já existe.",
            CharCreateResult.Invalid      => "Dados inválidos (nome 2-16, raça/classe).",
            CharCreateResult.LimitReached => "Limite de personagens atingido.",
            _                             => "Erro ao criar personagem.",
        };

        private void ClearList()
        {
            if (listContainer == null) return;
            for (int i = listContainer.childCount - 1; i >= 0; i--)
                Destroy(listContainer.GetChild(i).gameObject);
        }

        private static void Set(GameObject go, bool on) { if (go != null) go.SetActive(on); }
        private void SetText(TMP_Text label, string msg) { if (label != null) label.text = msg; }
    }
}
