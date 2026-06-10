using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using ArcaneShared.Enums;
using ArcaneShared.Models;
using Arcane_Aegis.Content;
using Arcane_Aegis.Network;

namespace Arcane_Aegis.UI
{
    /// <summary>
    /// Character lobby controller (ARPG style). Reads UI references from the scene (wired by hand) and fills the
    /// dynamic lists from the server — classes/races/genders into the create panel, characters into the select
    /// panel. Swap the visuals freely; this only needs the references.
    /// Option buttons clone <see cref="optionButtonPrefab"/> if set, else a plain button is created.
    /// </summary>
    public class CharacterLobby : MonoBehaviour
    {
        [Header("Panels")]
        [SerializeField] private GameObject createPanel;
        [SerializeField] private GameObject selectPanel;

        [Header("Create — list containers (filled at runtime; leave None if using fixed buttons)")]
        [SerializeField] private Transform classContainer;
        [SerializeField] private Transform raceContainer;
        [SerializeField] private Transform genderContainer;

        [Header("Create — your FIXED option buttons (auto-wired + highlighted; put an OptionButton on each)")]
        [SerializeField] private OptionButton[] optionButtons;

        [Header("Create — widgets")]
        [SerializeField] private TMP_InputField nameInput;
        [SerializeField] private TMP_Text selectedClassName;
        [SerializeField] private TMP_Text selectedClassDesc;
        [SerializeField] private TMP_Text createStatus;
        [SerializeField] private Button createButton;
        [SerializeField] private Button backButton;

        [Header("Select — info of the highlighted character")]
        [SerializeField] private TMP_Text infoName;
        [SerializeField] private TMP_Text infoLevel;
        [SerializeField] private TMP_Text infoPower;
        [SerializeField] private TMP_Text infoClan;
        [SerializeField] private TMP_Text infoLocal;

        [Header("Select — your fixed slot cards (put a CharacterSlot on each; max 5)")]
        [SerializeField] private CharacterSlot[] slots;
        [SerializeField] private TMP_Text selectStatus;
        [SerializeField] private Button enterButton;
        [SerializeField] private Button deleteButton;

        [Header("Optional: your styled option-button prefab (else a plain one is built)")]
        [SerializeField] private Button optionButtonPrefab;

        [Header("3D preview (optional — assign a ContentLibrary + a CharacterPreview)")]
        [SerializeField] private ContentLibrary library;
        [SerializeField] private CharacterPreview preview;

        private NetClient _net;
        private CreationOption[] _races = Array.Empty<CreationOption>();
        private CreationOption[] _classes = Array.Empty<CreationOption>();
        private CreationOption[] _genders = Array.Empty<CreationOption>();
        private string _classId = string.Empty, _raceId = string.Empty, _genderId = string.Empty;
        private readonly List<Button> _classBtns = new();
        private readonly List<Button> _raceBtns = new();
        private readonly List<Button> _genderBtns = new();

        private CharacterSummary[] _chars = Array.Empty<CharacterSummary>();
        private int _selectedSlot = -1;

        private void Awake()
        {
            _net = NetClient.Instance ?? FindAnyObjectByType<NetClient>();
            Wire(createButton, OnCreateClicked);
            Wire(backButton, ShowSelect);
            Wire(enterButton, OnEnterClicked);
            Wire(deleteButton, OnDeleteClicked);

            if (optionButtons != null)
                foreach (var ob in optionButtons)
                    if (ob != null && ob.Button != null)
                    {
                        OptionButton cap = ob;
                        ob.Button.onClick.AddListener(() => Select(cap.kind, cap.id));
                    }
        }

        private void OnEnable()
        {
            if (_net == null) return;
            _net.OnCreationData += OnCreationData;
            _net.OnCharacterList += OnCharacterList;
            _net.OnCharacterCreateResult += OnCreateResult;
            _net.OnConnectedToServer += OnReady;
        }

        private void OnDisable()
        {
            if (_net == null) return;
            _net.OnCreationData -= OnCreationData;
            _net.OnCharacterList -= OnCharacterList;
            _net.OnCharacterCreateResult -= OnCreateResult;
            _net.OnConnectedToServer -= OnReady;
        }

        private void Start()
        {
            ShowSelect();
            if (_net != null && _net.Connected) OnReady();
            else SetText(selectStatus, _net == null ? "Sem NetClient (entre pela Login)." : "Conectando…");
        }

        private void OnReady()
        {
            _net.RequestCreationData();
            _net.RequestCharacters();
            SetText(selectStatus, "Carregando personagens…");
        }

        // ── panels (also hooked to back/new buttons) ──
        public void ShowCreate() { if (createPanel) createPanel.SetActive(true); if (selectPanel) selectPanel.SetActive(false); }
        public void ShowSelect() { if (createPanel) createPanel.SetActive(false); if (selectPanel) selectPanel.SetActive(true); }

        // ── server events ──
        private void OnCreationData(CreationOption[] races, CreationOption[] classes, CreationOption[] genders)
        {
            _races = races ?? Array.Empty<CreationOption>();
            _classes = classes ?? Array.Empty<CreationOption>();
            _genders = genders ?? Array.Empty<CreationOption>();

            // If you assigned containers, buttons are generated; otherwise use YOUR fixed buttons → SelectClass/Race/Gender.
            Build(classContainer, _classes, _classBtns, i => SelectClass(_classes[i].Id));
            Build(raceContainer, _races, _raceBtns, i => SelectRace(_races[i].Id));
            Build(genderContainer, _genders, _genderBtns, i => SelectGender(_genders[i].Id));

            // Default to the first option so Create works even without a click.
            if (_classes.Length > 0 && string.IsNullOrEmpty(_classId)) SelectClass(_classes[0].Id);
            if (_races.Length > 0 && string.IsNullOrEmpty(_raceId)) SelectRace(_races[0].Id);
            if (_genders.Length > 0 && string.IsNullOrEmpty(_genderId)) SelectGender(_genders[0].Id);
        }

        // ── hook YOUR fixed buttons here (type the id in the button's OnClick); generated buttons call these too ──
        public void SelectClass(string id)  { _classId = id;  HighlightById(_classBtns, _classes, id);  HighlightOptions(OptionButton.Kind.Class, id);  ShowClassInfo(id); RefreshCreatePreview(); }
        public void SelectRace(string id)   { _raceId = id;   HighlightById(_raceBtns, _races, id);    HighlightOptions(OptionButton.Kind.Race, id); RefreshCreatePreview(); }
        public void SelectGender(string id) { _genderId = id; HighlightById(_genderBtns, _genders, id); HighlightOptions(OptionButton.Kind.Gender, id); RefreshCreatePreview(); }

        private void Select(OptionButton.Kind kind, string id)
        {
            switch (kind)
            {
                case OptionButton.Kind.Class: SelectClass(id); break;
                case OptionButton.Kind.Race: SelectRace(id); break;
                case OptionButton.Kind.Gender: SelectGender(id); break;
            }
        }

        private void HighlightOptions(OptionButton.Kind kind, string id)
        {
            if (optionButtons == null) return;
            foreach (var ob in optionButtons)
                if (ob != null && ob.kind == kind) ob.SetSelected(ob.id == id);
        }

        private void OnCharacterList(CharacterSummary[] chars)
        {
            _chars = chars ?? Array.Empty<CharacterSummary>();
            _selectedSlot = -1;
            ClearInfo();
            int n = slots?.Length ?? 0;
            for (int i = 0; i < n; i++)
            {
                var slot = slots[i];
                if (slot == null) continue;
                slot.SetSelected(false);
                if (i < _chars.Length)
                {
                    int idx = i;
                    slot.SetCharacter(_chars[i]);
                    WireSlot(slot, () => SelectSlot(idx)); // occupied → select it
                }
                else
                {
                    slot.SetEmpty();
                    WireSlot(slot, ShowCreate);            // empty (+) → create new
                }
            }
            SetText(selectStatus, _chars.Length == 0 ? "Nenhum personagem. Crie um!" : string.Empty);

            // No characters → go straight to Creation; otherwise show the Selection screen.
            if (_chars.Length == 0) ShowCreate();
            else ShowSelect();
        }

        private static void WireSlot(CharacterSlot slot, UnityEngine.Events.UnityAction action)
        {
            if (slot.Button == null) return;
            slot.Button.onClick.RemoveAllListeners();
            slot.Button.onClick.AddListener(action);
        }

        private void OnCreateResult(CharCreateResult r)
        {
            if (r == CharCreateResult.Ok) { SetText(createStatus, "Criado!"); ShowSelect(); _net.RequestCharacters(); return; }
            SetText(createStatus, r switch
            {
                CharCreateResult.NameTaken => "Nome já existe.",
                CharCreateResult.Invalid => "Dados inválidos (nome 2-16).",
                CharCreateResult.LimitReached => "Limite atingido.",
                _ => "Erro ao criar.",
            });
        }

        // ── button actions ──
        public void OnCreateClicked()
        {
            if (_net == null || !_net.Connected) { SetText(createStatus, "Conectando…"); return; }
            if (string.IsNullOrEmpty(_classId) || string.IsNullOrEmpty(_raceId)) { SetText(createStatus, "Escolha classe e raça."); return; }
            string name = nameInput != null ? nameInput.text : string.Empty;
            string gender = string.IsNullOrEmpty(_genderId) ? "male" : _genderId;
            SetText(createStatus, "Criando…");
            _net.CreateCharacter(name, _raceId, _classId, gender);
        }

        public void OnEnterClicked()
        {
            if (_selectedSlot < 0) { SetText(selectStatus, "Escolha um personagem."); return; }
            ClientSession.CharacterId = _chars[_selectedSlot].Id;
            SetText(selectStatus, $"Entrando com {_chars[_selectedSlot].Name}…");
            SceneManager.LoadScene("World");
        }

        public void OnDeleteClicked()
        {
            if (_selectedSlot < 0) { SetText(selectStatus, "Escolha um personagem pra excluir."); return; }
            if (_net == null || !_net.Connected) return;
            var c = _chars[_selectedSlot];
            SetText(selectStatus, $"Excluindo {c.Name}…");
            _net.DeleteCharacter(c.Id); // server deletes + re-sends the list → slots refresh
        }

        private void SelectSlot(int idx)
        {
            _selectedSlot = idx;
            for (int i = 0; i < (slots?.Length ?? 0); i++) if (slots[i] != null) slots[i].SetSelected(i == idx);
            var c = _chars[idx];
            SetText(infoName, c.Name);
            SetText(infoLevel, $"{c.Level}");
            SetText(infoPower, "—");
            SetText(infoClan, "—");
            SetText(infoLocal, $"{c.RaceId} / {c.ClassId}");
            ShowPreviewFor(c.RaceId, c.ClassId, c.GenderId);
        }

        private void ShowClassInfo(string id)
        {
            string display = id;
            foreach (var c in _classes) if (c.Id == id) { display = c.Name; break; }
            SetText(selectedClassName, display);
            SetText(selectedClassDesc, string.Empty); // descriptions come from content later
        }

        private void RefreshCreatePreview() => ShowPreviewFor(_raceId, _classId, _genderId);

        /// <summary>Resolve the (race+class) CharacterTemplate, take the gender's model, and show it in the preview.</summary>
        private void ShowPreviewFor(string raceId, string classId, string genderId)
        {
            if (preview == null || library == null || library.templates == null) return;
            var tpl = library.templates.Find(t => t != null
                && t.race != null && t.race.id == raceId
                && t.characterClass != null && t.characterClass.id == classId);
            // Dev fallback (few templates so far): no exact race+class match → show the first template that has a model.
            if (tpl == null) tpl = library.templates.Find(t => t != null && t.genders != null && t.genders.Exists(g => g != null && g.model != null));
            if (tpl == null) { preview.Show(null); return; }
            var gm = tpl.GetGender(genderId);
            if (gm == null || gm.model == null) gm = tpl.genders.Find(g => g != null && g.model != null); // any gender with a model
            preview.Show(gm != null ? gm.model : null);
        }

        private static void HighlightById(List<Button> store, CreationOption[] opts, string id)
        {
            for (int i = 0; i < store.Count && i < opts.Length; i++)
            {
                var img = store[i].GetComponent<Image>();
                if (img != null) img.color = (opts[i].Id == id) ? new Color(0.85f, 0.7f, 0.3f) : new Color(0.20f, 0.25f, 0.35f);
            }
        }

        // ── helpers ──
        private void Build(Transform container, CreationOption[] opts, List<Button> store, Action<int> onPick)
        {
            if (container == null) return;
            ClearChildren(container); store.Clear();
            for (int i = 0; i < opts.Length; i++)
            {
                int idx = i;
                store.Add(MakeOption(container, opts[i].Name, () => onPick(idx)));
            }
        }

        private Button MakeOption(Transform parent, string label, UnityEngine.Events.UnityAction onClick)
        {
            Button b = optionButtonPrefab != null ? Instantiate(optionButtonPrefab, parent) : MakePlainButton(parent);
            var t = b.GetComponentInChildren<TMP_Text>();
            if (t != null) t.text = label;
            b.onClick.RemoveAllListeners();
            b.onClick.AddListener(onClick);
            return b;
        }

        private static Button MakePlainButton(Transform parent)
        {
            var go = new GameObject("Option", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = new Color(0.20f, 0.25f, 0.35f);
            go.GetComponent<LayoutElement>().minHeight = 48;
            var txt = new GameObject("Text", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            txt.transform.SetParent(go.transform, false);
            var rt = (RectTransform)txt.transform; rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            txt.alignment = TextAlignmentOptions.Center; txt.fontSize = 22; txt.color = Color.white;
            return go.GetComponent<Button>();
        }

        private void ClearInfo()
        {
            SetText(infoName, "—"); SetText(infoLevel, ""); SetText(infoPower, ""); SetText(infoClan, ""); SetText(infoLocal, "");
        }

        private static void Wire(Button b, UnityEngine.Events.UnityAction a) { if (b != null) { b.onClick.RemoveListener(a); b.onClick.AddListener(a); } }
        private static void ClearChildren(Transform t) { if (t == null) return; for (int i = t.childCount - 1; i >= 0; i--) Destroy(t.GetChild(i).gameObject); }
        private static void SetText(TMP_Text l, string m) { if (l != null) l.text = m; }
    }
}
