using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using TMPro;
using ArcaneShared.Enums;
using ArcaneShared.Models;
using Arcane_Aegis.Network;

namespace Arcane_Aegis.UI
{
    /// <summary>
    /// DEV SCAFFOLD — builds a working CharacterSelection UI entirely from code so we can test the character flow
    /// before the real art UI is wired. Data-driven: race/class buttons come from S2C_CreationData; the list from
    /// the account's characters. Throwaway: drop this on ONE empty GameObject in the Character_Selection scene
    /// (disable your own Canvas while testing), then replace it with your UI later. Uses NetClient.Instance.
    /// </summary>
    public class CharacterLobbyUI : MonoBehaviour
    {
        private NetClient _net;

        private GameObject _listPanel, _createPanel;
        private Transform _charList;
        private TMP_Text _listStatus;

        private TMP_InputField _nameInput;
        private Transform _raceRow, _classRow;
        private TMP_Text _createStatus;

        private CreationOption[] _races = Array.Empty<CreationOption>();
        private CreationOption[] _classes = Array.Empty<CreationOption>();
        private int _raceIdx, _classIdx;
        private readonly List<Button> _raceButtons = new();
        private readonly List<Button> _classButtons = new();

        private uint _selectedCharId;
        private string _selectedCharName = string.Empty;

        private void Awake()
        {
            _net = NetClient.Instance ?? FindAnyObjectByType<NetClient>();
            if (FindAnyObjectByType<EventSystem>() == null)
                Debug.LogWarning("[CharacterLobbyUI] No EventSystem in the scene — UI clicks won't work. Add one (GameObject → UI → Event System).");
            BuildUI();
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
            ShowList();
            if (_net != null && _net.Connected) OnReady();
            else SetListStatus(_net == null ? "Sem NetClient (entre pela cena de Login)." : "Conectando ao servidor…");
        }

        private void OnReady()
        {
            _net.RequestCreationData();
            _net.RequestCharacters();
            SetListStatus("Carregando personagens…");
        }

        // ── server events ──
        private void OnCreationData(CreationOption[] races, CreationOption[] classes)
        {
            _races = races ?? Array.Empty<CreationOption>();
            _classes = classes ?? Array.Empty<CreationOption>();
            BuildOptionButtons(_raceRow, _races, _raceButtons, i => { _raceIdx = i; Highlight(_raceButtons, _raceIdx); });
            BuildOptionButtons(_classRow, _classes, _classButtons, i => { _classIdx = i; Highlight(_classButtons, _classIdx); });
            _raceIdx = 0; _classIdx = 0;
            Highlight(_raceButtons, 0);
            Highlight(_classButtons, 0);
        }

        private void OnCharacterList(CharacterSummary[] chars)
        {
            ClearChildren(_charList);
            _selectedCharId = 0;
            if (chars == null || chars.Length == 0) { SetListStatus("Nenhum personagem. Crie um!"); return; }
            SetListStatus(string.Empty);
            foreach (var c in chars)
            {
                CharacterSummary cap = c;
                MakeButton(_charList, $"{c.Name}   (Nv {c.Level} · {c.RaceId}/{c.ClassId})", () =>
                {
                    _selectedCharId = cap.Id;
                    _selectedCharName = cap.Name;
                    SetListStatus($"{cap.Name} selecionado.");
                });
            }
        }

        private void OnCreateResult(CharCreateResult r)
        {
            if (r == CharCreateResult.Ok)
            {
                SetCreateStatus("Criado!");
                ShowList();
                _net.RequestCharacters();
                return;
            }
            SetCreateStatus(r switch
            {
                CharCreateResult.NameTaken    => "Esse nome já existe.",
                CharCreateResult.Invalid      => "Dados inválidos (nome 2-16, raça/classe).",
                CharCreateResult.LimitReached => "Limite de personagens atingido.",
                _                             => "Erro ao criar.",
            });
        }

        // ── actions ──
        private void DoCreate()
        {
            if (_net == null || !_net.Connected) { SetCreateStatus("Conectando…"); return; }
            if (_races.Length == 0 || _classes.Length == 0) { SetCreateStatus("Aguarde o catálogo…"); return; }
            string name = _nameInput != null ? _nameInput.text : string.Empty;
            string race = _races[Mathf.Clamp(_raceIdx, 0, _races.Length - 1)].Id;
            string cls = _classes[Mathf.Clamp(_classIdx, 0, _classes.Length - 1)].Id;
            SetCreateStatus("Criando…");
            _net.CreateCharacter(name, race, cls);
        }

        private void DoEnterWorld()
        {
            if (_selectedCharId == 0) { SetListStatus("Escolha um personagem."); return; }
            ClientSession.CharacterId = _selectedCharId;
            SetListStatus($"Entrando com {_selectedCharName}…");
            SceneManager.LoadScene("World"); // World scene's WorldEntry sends enter-world(CharacterId)
        }

        private void ShowList()   { if (_listPanel) _listPanel.SetActive(true);  if (_createPanel) _createPanel.SetActive(false); }
        private void ShowCreate() { if (_listPanel) _listPanel.SetActive(false); if (_createPanel) _createPanel.SetActive(true); }

        // ───────────────────────── UI building (throwaway) ─────────────────────────
        private void BuildUI()
        {
            var canvasGO = new GameObject("CharacterLobbyUI_Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100; // draw over the placeholder art UI
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            _listPanel = MakePanel(canvas.transform, "ListPanel");
            BuildListPanel(_listPanel.transform);

            _createPanel = MakePanel(canvas.transform, "CreatePanel");
            BuildCreatePanel(_createPanel.transform);
        }

        private void BuildListPanel(Transform p)
        {
            MakeLabel(p, "PERSONAGENS", 40);
            _listStatus = MakeLabel(p, string.Empty, 22);

            var listGO = new GameObject("CharList", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            listGO.transform.SetParent(p, false);
            var vlg = listGO.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = 6; vlg.childControlWidth = true; vlg.childForceExpandWidth = true;
            vlg.childControlHeight = true; vlg.childForceExpandHeight = false;
            listGO.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _charList = listGO.transform;

            MakeButton(p, "+ Novo Personagem", ShowCreate);
            MakeButton(p, "ENTRAR NO MUNDO", DoEnterWorld);
        }

        private void BuildCreatePanel(Transform p)
        {
            MakeLabel(p, "CRIAR PERSONAGEM", 40);
            _nameInput = MakeInput(p, "Nome do personagem");
            MakeLabel(p, "Raça:", 22);
            _raceRow = MakeRow(p, "RaceRow");
            MakeLabel(p, "Classe:", 22);
            _classRow = MakeRow(p, "ClassRow");
            _createStatus = MakeLabel(p, string.Empty, 22);
            MakeButton(p, "CRIAR", DoCreate);
            MakeButton(p, "← Voltar", ShowList);
        }

        private void BuildOptionButtons(Transform row, CreationOption[] opts, List<Button> store, Action<int> onPick)
        {
            ClearChildren(row);
            store.Clear();
            for (int i = 0; i < opts.Length; i++)
            {
                int idx = i;
                store.Add(MakeButton(row, opts[i].Name, () => onPick(idx)));
            }
        }

        private static void Highlight(List<Button> store, int selected)
        {
            for (int i = 0; i < store.Count; i++)
            {
                var img = store[i].GetComponent<Image>();
                if (img != null) img.color = (i == selected) ? new Color(0.85f, 0.7f, 0.3f) : new Color(0.20f, 0.25f, 0.35f);
            }
        }

        // ── widget factories ──
        private static GameObject MakePanel(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            go.GetComponent<Image>().color = new Color(0.05f, 0.07f, 0.12f, 0.92f);
            var v = go.GetComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(60, 60, 50, 50);
            v.spacing = 14;
            v.childAlignment = TextAnchor.UpperCenter;
            v.childControlWidth = true; v.childForceExpandWidth = false;
            v.childControlHeight = true; v.childForceExpandHeight = false;
            return go;
        }

        private static Transform MakeRow(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup));
            go.transform.SetParent(parent, false);
            var h = go.GetComponent<HorizontalLayoutGroup>();
            h.spacing = 8; h.childAlignment = TextAnchor.MiddleCenter;
            h.childControlWidth = true; h.childForceExpandWidth = false;
            h.childControlHeight = true; h.childForceExpandHeight = false;
            return go.transform;
        }

        private static TMP_Text MakeLabel(Transform parent, string text, int size)
        {
            var t = MakeTextRaw(parent, text, size);
            var le = t.gameObject.AddComponent<LayoutElement>();
            le.minHeight = size + 8;
            t.alignment = TextAlignmentOptions.Center;
            return t;
        }

        private static TextMeshProUGUI MakeTextRaw(Transform parent, string text, int size)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = text; t.fontSize = size; t.color = Color.white;
            t.alignment = TextAlignmentOptions.Center;
            return t;
        }

        private Button MakeButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = new Color(0.20f, 0.25f, 0.35f);
            var le = go.GetComponent<LayoutElement>();
            le.minHeight = 48; le.preferredHeight = 48; le.minWidth = 200; le.preferredWidth = 220;
            var btn = go.GetComponent<Button>();
            if (onClick != null) btn.onClick.AddListener(onClick);

            var txt = MakeTextRaw(go.transform, label, 22);
            var trt = (RectTransform)txt.transform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
            return btn;
        }

        private TMP_InputField MakeInput(Transform parent, string placeholder)
        {
            var go = new GameObject("NameInput", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.92f);
            var le = go.GetComponent<LayoutElement>();
            le.minHeight = 48; le.preferredHeight = 48; le.minWidth = 360; le.preferredWidth = 360;
            var input = go.AddComponent<TMP_InputField>();

            var area = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D));
            area.transform.SetParent(go.transform, false);
            var art = (RectTransform)area.transform;
            art.anchorMin = Vector2.zero; art.anchorMax = Vector2.one; art.offsetMin = new Vector2(12, 6); art.offsetMax = new Vector2(-12, -6);

            var ph = MakeTextRaw(area.transform, placeholder, 22);
            ph.color = new Color(0.35f, 0.35f, 0.35f, 0.8f); ph.alignment = TextAlignmentOptions.Left;
            Stretch((RectTransform)ph.transform);
            var txt = MakeTextRaw(area.transform, string.Empty, 22);
            txt.color = Color.black; txt.alignment = TextAlignmentOptions.Left;
            Stretch((RectTransform)txt.transform);

            input.textViewport = art;
            input.textComponent = txt;
            input.placeholder = ph;
            input.characterLimit = 16;
            input.text = string.Empty;
            return input;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        private static void ClearChildren(Transform t)
        {
            if (t == null) return;
            for (int i = t.childCount - 1; i >= 0; i--) Destroy(t.GetChild(i).gameObject);
        }

        private void SetListStatus(string m) { if (_listStatus != null) _listStatus.text = m; }
        private void SetCreateStatus(string m) { if (_createStatus != null) _createStatus.text = m; }
    }
}
