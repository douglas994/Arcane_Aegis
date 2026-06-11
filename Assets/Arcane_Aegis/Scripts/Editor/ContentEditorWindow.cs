using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using UnityEditor;
using UnityEngine;
using NetworkLibrary;
using NetworkLibrary.Serialization;
using NetworkLibrary.Transport;
using ArcaneShared.Constants;
using ArcaneShared.Protocol;
using ArcaneShared.Protocol.Internal;
using Arcane_Aegis.Content;

namespace Arcane_Aegis.EditorTools
{
    /// <summary>
    /// ArcaneMMO Content Editor — a 3-column content browser (categories | items | inspector). The content lives as
    /// ScriptableObjects (client source of truth + art); "Sync" pushes the gameplay fields to the ArcaneDatabase
    /// (tcp:47000). Add a new content type = one Category entry (+ its SO + optional sync lambda). Items/skills/etc.
    /// slot in here. Menu: Window ▸ ArcaneMMO ▸ Content Editor.
    /// </summary>
    public class ContentEditorWindow : EditorWindow, INetEventListener
    {
        [Serializable] private class ClassDto  { public string Id, Name; public int Str, Dex, Int, Vit, Spi, Luk, StrPerLevel, DexPerLevel, IntPerLevel, VitPerLevel, SpiPerLevel, LukPerLevel; }
        [Serializable] private class RaceDto   { public string Id, Name, Element; public int Str, Dex, Int, Vit, Spi, Luk, Armor, HomeZoneId; }
        [Serializable] private class GenderDto { public string Id, Name; }

        /// <summary>A content category: an SO type + its folder + (optional) how to serialize it for the server.</summary>
        private sealed class Category
        {
            public string Name;
            public Type SoType;
            public string Folder;
            public string Icon;
            public Func<ScriptableObject, (string type, string id, string json)> ToContent; // null = client-only (no sync)
        }

        private const string Root = "Assets/Arcane_Aegis/Content";
        private List<Category> _cats;

        private NetManager _net;
        private NetPeer _db;
        private bool _connected;
        private string _host = "127.0.0.1";
        private int _port = 47000;
        private string _status = "Desconectado.";

        private int _cat;
        private string _newName = "";
        private UnityEngine.Object _selected;
        private Editor _inspector;
        private Vector2 _catScroll, _listScroll, _inspScroll;

        [MenuItem("Window/ArcaneMMO/Content Editor")]
        public static void Open()
        {
            var w = GetWindow<ContentEditorWindow>("Content Editor");
            w.minSize = new Vector2(760, 440);
        }

        private void OnEnable()
        {
            BuildCategories();
            EditorApplication.update += Pump;
        }
        private void OnDisable()
        {
            EditorApplication.update -= Pump;
            Disconnect();
            if (_inspector != null) DestroyImmediate(_inspector);
        }
        private void Pump() => _net?.PollEvents();

        private void BuildCategories()
        {
            _cats = new List<Category>
            {
                new() { Name = "Classes", Icon = "", SoType = typeof(ClassDefinitionSO),  Folder = "Classes", ToContent = so => ("class",  ((ClassDefinitionSO)so).id,  JsonUtility.ToJson(ToDto((ClassDefinitionSO)so))) },
                new() { Name = "Races",   Icon = "", SoType = typeof(RaceDefinitionSO),   Folder = "Races",   ToContent = so => ("race",   ((RaceDefinitionSO)so).id,   JsonUtility.ToJson(ToDto((RaceDefinitionSO)so))) },
                new() { Name = "Genders", Icon = "", SoType = typeof(GenderDefinitionSO), Folder = "Genders", ToContent = so => ("gender", ((GenderDefinitionSO)so).id, JsonUtility.ToJson(ToDto((GenderDefinitionSO)so))) },
                // Playable archetypes (Atavism "Player Template"): binds race+class + models per gender. Client-only (ToContent = null) — race/class STATS sync via the catalogs above.
                new() { Name = "Templates", Icon = "", SoType = typeof(CharacterTemplateSO), Folder = "Templates", ToContent = null },
                // Add more here as the SOs land, e.g.:
                // new() { Name = "Items", Icon = "🎒", SoType = typeof(ItemSO), Folder = "Items", ToContent = so => ("item", ((ItemSO)so).id, JsonUtility.ToJson(ToDto((ItemSO)so))) },
                // new() { Name = "Skills", Icon = "✨", SoType = typeof(SkillSO), Folder = "Skills", ToContent = ... },
            };
        }

        // ── connection ──
        private void Connect()
        {
            Disconnect();
            _net = new NetManager(this, TransportType.Tcp) { ConnectionKey = NetConstants.ConnectionKey, ProtocolVersion = NetConstants.ProtocolVersion };
            _net.Connect(_host, _port);
            _status = $"Conectando em {_host}:{_port}…";
        }
        private void Disconnect() { _net?.Stop(); _net = null; _db = null; _connected = false; }

        public void OnPeerConnected(NetPeer peer) { _db = peer; _connected = true; _status = "Conectado ✓"; Repaint(); }
        public void OnPeerDisconnected(NetPeer peer, DisconnectReason reason) { _db = null; _connected = false; _status = $"Desconectado ({reason})."; Repaint(); }
        public void OnNetworkError(SocketError socketError) { _status = $"Erro: {socketError}"; Repaint(); }
        public void OnNetworkReceive(NetPeer peer, BitBuffer reader, DeliveryMethod deliveryMethod) { }

        private void Send<T>(in T packet) where T : IPacket
        {
            if (_db == null) return;
            var buffer = new BitBuffer();
            try { PacketWriter.Write(ref buffer, packet); _db.Send(buffer, DeliveryMethod.ReliableOrdered); }
            finally { buffer.Dispose(); }
        }

        private void SyncAll()
        {
            if (!_connected) { _status = "Conecte primeiro."; return; }
            Send(new I_Db_ClearContent { Type = "" }); // full replace: wipe the table, then re-insert (mirror)
            int n = 0;
            foreach (var cat in _cats)
            {
                if (cat.ToContent == null) continue;
                foreach (var so in FindAll(cat))
                {
                    var (type, id, json) = cat.ToContent(so);
                    if (!string.IsNullOrWhiteSpace(id)) { Send(new I_Db_UpsertContent { Type = type, ContentId = id, Json = json }); n++; }
                }
            }
            _status = $"Mirror ✓ — DB limpo + {n} item(ns) re-inseridos.";
        }

        private static ClassDto ToDto(ClassDefinitionSO c) => new()
        {
            Id = c.id, Name = c.displayName,
            Str = c.str, Dex = c.dex, Int = c.intel, Vit = c.vit, Spi = c.spi, Luk = c.luk,
            StrPerLevel = c.strPerLevel, DexPerLevel = c.dexPerLevel, IntPerLevel = c.intPerLevel,
            VitPerLevel = c.vitPerLevel, SpiPerLevel = c.spiPerLevel, LukPerLevel = c.lukPerLevel,
        };
        private static RaceDto ToDto(RaceDefinitionSO r) => new()
        {
            Id = r.id, Name = r.displayName, Element = r.element,
            Str = r.str, Dex = r.dex, Int = r.intel, Vit = r.vit, Spi = r.spi, Luk = r.luk, Armor = r.armor,
            HomeZoneId = r.homeZoneId,
        };
        private static GenderDto ToDto(GenderDefinitionSO g) => new() { Id = g.id, Name = g.displayName };

        private void CollectIntoLibrary()
        {
            var found = FindAllOf<ContentLibrary>();
            ContentLibrary lib = found.Length > 0 ? found[0] : CreateLibrary();
            lib.classes = new List<ClassDefinitionSO>(FindAllOf<ClassDefinitionSO>());
            lib.races = new List<RaceDefinitionSO>(FindAllOf<RaceDefinitionSO>());
            lib.genders = new List<GenderDefinitionSO>(FindAllOf<GenderDefinitionSO>());
            lib.templates = new List<CharacterTemplateSO>(FindAllOf<CharacterTemplateSO>());
            EditorUtility.SetDirty(lib);
            AssetDatabase.SaveAssets();
            _status = $"ContentLibrary: {lib.classes.Count} classes, {lib.races.Count} raças, {lib.genders.Count} gêneros, {lib.templates.Count} templates.";
            Selection.activeObject = lib;
        }

        // ───────────────────────── UI ─────────────────────────
        private void OnGUI()
        {
            DrawHeader();
            DrawConnectionBar();
            EditorGUILayout.Space(4);

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawCategories();
                DrawItems();
                DrawInspector();
            }
        }

        private void DrawHeader()
        {
            var rect = EditorGUILayout.GetControlRect(false, 32);
            EditorGUI.DrawRect(rect, new Color(0.16f, 0.18f, 0.26f));
            var style = new GUIStyle(EditorStyles.boldLabel) { fontSize = 14, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(10, 0, 0, 0) };
            style.normal.textColor = new Color(0.85f, 0.88f, 1f);
            EditorGUI.LabelField(rect, "⚔  ArcaneMMO — Content Editor", style);
        }

        private void DrawConnectionBar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                GUILayout.Label("DB", GUILayout.Width(22));
                _host = EditorGUILayout.TextField(_host, GUILayout.Width(100));
                _port = EditorGUILayout.IntField(_port, GUILayout.Width(54));
                string dot = _connected ? "<color=#6cf06c>●</color>" : "<color=#f06c6c>●</color>";
                GUILayout.Label($"<b>{dot}</b> {_status}", Rich(), GUILayout.MinWidth(140));
                GUILayout.FlexibleSpace();
                if (!_connected) { if (GUILayout.Button("Connect", GUILayout.Width(80))) Connect(); }
                else if (GUILayout.Button("Disconnect", GUILayout.Width(80))) Disconnect();
                if (GUILayout.Button("Collect→Library", GUILayout.Width(110))) CollectIntoLibrary();
                var prev = GUI.backgroundColor;
                GUI.backgroundColor = _connected ? new Color(0.45f, 0.8f, 0.45f) : Color.gray;
                using (new EditorGUI.DisabledScope(!_connected))
                    if (GUILayout.Button("⟳ SYNC ALL → DB", GUILayout.Width(140))) SyncAll();
                GUI.backgroundColor = prev;
            }
        }

        private void DrawCategories()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Width(140)))
            {
                EditorGUILayout.LabelField("CONTENT", EditorStyles.miniBoldLabel);
                _catScroll = EditorGUILayout.BeginScrollView(_catScroll);
                for (int i = 0; i < _cats.Count; i++)
                {
                    bool sel = _cat == i;
                    var style = new GUIStyle(EditorStyles.miniButton) { alignment = TextAnchor.MiddleLeft, fixedHeight = 26 };
                    var prev = GUI.backgroundColor;
                    if (sel) GUI.backgroundColor = new Color(0.45f, 0.6f, 1f);
                    if (GUILayout.Button($"  {_cats[i].Icon}  {_cats[i].Name}", style) && !sel) { _cat = i; Select(null); }
                    GUI.backgroundColor = prev;
                }
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawItems()
        {
            var cat = _cats[_cat];
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Width(190)))
            {
                EditorGUILayout.LabelField(cat.Name, EditorStyles.boldLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    _newName = EditorGUILayout.TextField(_newName);
                    var prev = GUI.backgroundColor;
                    GUI.backgroundColor = new Color(0.5f, 0.7f, 1f);
                    if (GUILayout.Button("+ New", GUILayout.Width(56))) CreateNew(cat);
                    GUI.backgroundColor = prev;
                }
                DrawDivider();
                _listScroll = EditorGUILayout.BeginScrollView(_listScroll);
                foreach (var so in FindAll(cat))
                {
                    bool sel = _selected == so;
                    var style = new GUIStyle(EditorStyles.miniButton) { alignment = TextAnchor.MiddleLeft, fixedHeight = 22 };
                    var prev = GUI.backgroundColor;
                    if (sel) GUI.backgroundColor = new Color(0.45f, 0.6f, 1f);
                    if (GUILayout.Button(so.name, style) && !sel) Select(so);
                    GUI.backgroundColor = prev;
                }
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawInspector()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (_selected == null) { EditorGUILayout.HelpBox("Selecione um item ou clique + New.", MessageType.Info); return; }
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(_selected.name, EditorStyles.boldLabel);
                    if (GUILayout.Button("Rename file → id", GUILayout.Width(110))) RenameToId();
                    if (GUILayout.Button("Delete", GUILayout.Width(60))) DeleteSelected();
                }
                DrawDivider();
                _inspScroll = EditorGUILayout.BeginScrollView(_inspScroll);
                if (_inspector == null || _inspector.target != _selected)
                {
                    if (_inspector != null) DestroyImmediate(_inspector);
                    _inspector = Editor.CreateEditor(_selected);
                }
                _inspector.OnInspectorGUI();
                EditorGUILayout.EndScrollView();
            }
        }

        // ── create / rename / delete (generic via SerializedObject) ──
        private void CreateNew(Category cat)
        {
            string name = string.IsNullOrWhiteSpace(_newName) ? $"New{cat.Name}" : _newName.Trim();
            string folder = $"{Root}/{cat.Folder}";
            EnsureFolder(folder);
            var so = CreateInstance(cat.SoType);
            string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{name}.asset");
            AssetDatabase.CreateAsset(so, path);

            var sb = new SerializedObject(so);
            var idProp = sb.FindProperty("id");
            var nameProp = sb.FindProperty("displayName");
            if (idProp != null) idProp.stringValue = Sanitize(name);
            if (nameProp != null) nameProp.stringValue = name;
            sb.ApplyModifiedProperties();

            AssetDatabase.SaveAssets();
            Select(so);
            _newName = "";
            _status = $"Criado: {name} em {cat.Folder}/";
        }

        private void RenameToId()
        {
            string id = new SerializedObject(_selected).FindProperty("id")?.stringValue;
            if (string.IsNullOrWhiteSpace(id)) { _status = "id vazio — preencha antes."; return; }
            AssetDatabase.RenameAsset(AssetDatabase.GetAssetPath(_selected), id);
            AssetDatabase.SaveAssets();
            _status = $"Renomeado → {id}";
        }

        private void DeleteSelected()
        {
            if (_selected == null) return;
            if (!EditorUtility.DisplayDialog("Excluir", $"Apagar '{_selected.name}'?", "Apagar", "Cancelar")) return;
            AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(_selected));
            Select(null);
        }

        private void Select(UnityEngine.Object o)
        {
            _selected = o;
            if (_inspector != null) { DestroyImmediate(_inspector); _inspector = null; }
            Repaint();
        }

        // ── assets ──
        private static ScriptableObject[] FindAll(Category cat)
        {
            var guids = AssetDatabase.FindAssets($"t:{cat.SoType.Name}");
            var arr = new ScriptableObject[guids.Length];
            for (int i = 0; i < guids.Length; i++)
                arr[i] = (ScriptableObject)AssetDatabase.LoadAssetAtPath(AssetDatabase.GUIDToAssetPath(guids[i]), cat.SoType);
            return arr;
        }

        private static T[] FindAllOf<T>() where T : ScriptableObject
        {
            var guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
            var arr = new T[guids.Length];
            for (int i = 0; i < guids.Length; i++)
                arr[i] = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[i]));
            return arr;
        }

        private static ContentLibrary CreateLibrary()
        {
            EnsureFolder(Root);
            var lib = CreateInstance<ContentLibrary>();
            AssetDatabase.CreateAsset(lib, $"{Root}/ContentLibrary.asset");
            AssetDatabase.SaveAssets();
            return lib;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            string leaf = System.IO.Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        private static string Sanitize(string s)
        {
            var sb = new StringBuilder();
            foreach (char ch in (s ?? "").ToLowerInvariant())
                if (char.IsLetterOrDigit(ch)) sb.Append(ch);
            return sb.ToString();
        }

        private static void DrawDivider()
        {
            var r = EditorGUILayout.GetControlRect(false, 1);
            EditorGUI.DrawRect(r, new Color(1, 1, 1, 0.1f));
        }

        private static GUIStyle Rich() => new(EditorStyles.label) { richText = true };
    }
}
