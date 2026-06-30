using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Arcane_Aegis.Content;

namespace Arcane_Aegis.UI
{
    /// <summary>
    /// Shows an NPC's branching dialogue: the NPC's name + the current node's text, and a button per answer option.
    /// You build the panel by hand and drop the refs here (root, name/body TMP texts, an options container, and an
    /// option-button prefab with a TMP label). The dialogue is client art (no server round-trip); only an option's
    /// ACTION may call the server (e.g. "Loja" opens the server-validated shop). One scene instance.
    /// </summary>
    public sealed class DialoguePanel : MonoBehaviour
    {
        public static DialoguePanel Instance { get; private set; }

        [Tooltip("O objeto raiz do painel (ligado/desligado ao abrir/fechar).")]
        [SerializeField] private GameObject root;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text bodyText;
        [Tooltip("Container onde os botões de opção são instanciados (ex.: um VerticalLayoutGroup).")]
        [SerializeField] private Transform optionsContainer;
        [Tooltip("Prefab de um botão de opção (Button com um TMP_Text no filho).")]
        [SerializeField] private Button optionButtonPrefab;

        private NpcDefinitionSO _npc;
        private readonly List<Button> _spawned = new();

        public bool IsOpen => root != null && root.activeSelf;

        private void Awake()
        {
            Instance = this;
            if (root != null) root.SetActive(false);
        }
        private void OnDestroy() { if (Instance == this) Instance = null; }

        /// <summary>Open the dialogue for the NPC with this content id (resolves the NpcDefinition + shows its greeting).</summary>
        public void Open(string npcId)
        {
            _npc = ContentLibrary.Active != null ? ContentLibrary.Active.GetNpc(npcId) : null;
            if (_npc == null) { Close(); return; }
            if (root != null) root.SetActive(true);
            if (nameText != null) nameText.text = string.IsNullOrEmpty(_npc.displayName) ? _npc.id : _npc.displayName;
            ShowNode(_npc.EntryNode());
        }

        public void Close()
        {
            ClearOptions();
            _npc = null;
            if (root != null) root.SetActive(false);
        }

        private void ShowNode(NpcDefinitionSO.Node node)
        {
            if (node == null) { Close(); return; }
            if (bodyText != null) bodyText.text = node.text;
            ClearOptions();
            if (optionButtonPrefab == null || optionsContainer == null) return;

            // No options authored → a default "Sair" so the player can always leave.
            if (node.options == null || node.options.Count == 0)
            {
                AddOption("Sair", default, isDefaultClose: true);
                return;
            }
            for (int i = 0; i < node.options.Count; i++) AddOption(node.options[i].label, node.options[i], false);
        }

        private void AddOption(string label, NpcDefinitionSO.Option option, bool isDefaultClose)
        {
            var btn = Instantiate(optionButtonPrefab, optionsContainer);
            var txt = btn.GetComponentInChildren<TMP_Text>();
            if (txt != null) txt.text = string.IsNullOrEmpty(label) ? "…" : label;
            btn.onClick.AddListener(() => { if (isDefaultClose) Close(); else HandleOption(option); });
            _spawned.Add(btn);
        }

        private void HandleOption(NpcDefinitionSO.Option option)
        {
            switch (option.action)
            {
                case NpcDefinitionSO.DialogueAction.Goto:
                    ShowNode(_npc.FindNode(option.nextNodeId));
                    break;
                case NpcDefinitionSO.DialogueAction.OpenShop:
                    string shopId = _npc != null ? _npc.id : null; // a vendor NPC's shop is keyed by its own id
                    Close();
                    if (string.IsNullOrEmpty(shopId)) break;
                    if (ShopPanel.Instance == null) { Debug.LogWarning("[Dialogue] 'Loja' clicada mas NÃO há ShopPanel na cena — crie/ative o painel de loja (ArcaneMMO ▸ UI ▸ Shop Panel)."); break; }
                    ShopPanel.Instance.Open(shopId);
                    break;
                default:
                    Close();
                    break;
            }
        }

        private void ClearOptions()
        {
            for (int i = 0; i < _spawned.Count; i++) if (_spawned[i] != null) Destroy(_spawned[i].gameObject);
            _spawned.Clear();
        }
    }
}
