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
using ArcaneShared.Enums;
using ArcaneShared.Models;
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
        private int _spawnZone = 1; // zone id for the spawner export
        private string _newName = "";
        private string _search = "";
        private UnityEngine.Object _selected;
        private Editor _inspector;
        private Vector2 _catScroll, _listScroll, _inspScroll;

        // ── palette ──
        private static readonly Color ColAccent   = new(0.45f, 0.62f, 1f);
        private static readonly Color ColRowSel    = new(0.24f, 0.30f, 0.48f);
        private static readonly Color ColRowHover  = new(1f, 1f, 1f, 0.05f);
        private static readonly Color ColHeader    = new(0.15f, 0.17f, 0.25f);
        private static readonly Color ColHeader2   = new(0.10f, 0.12f, 0.18f);
        private static readonly Color ColText      = new(0.86f, 0.89f, 1f);
        private static readonly Color ColMuted     = new(0.55f, 0.58f, 0.66f);

        private GUIStyle _rowLabel, _rowMuted, _badge, _sectionLabel;
        private void EnsureStyles()
        {
            if (_rowLabel != null) return;
            _rowLabel = new GUIStyle(EditorStyles.label) { fontSize = 12, alignment = TextAnchor.MiddleLeft, richText = true };
            _rowLabel.normal.textColor = ColText;
            _rowMuted = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleRight, richText = true };
            _rowMuted.normal.textColor = ColMuted;
            _badge = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter };
            _badge.normal.textColor = new Color(0.8f, 0.85f, 1f);
            _sectionLabel = new GUIStyle(EditorStyles.miniBoldLabel);
            _sectionLabel.normal.textColor = ColMuted;
        }

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
            wantsMouseMove = true; // for row hover highlights
        }
        private void OnDisable()
        {
            EditorApplication.update -= Pump;
            Disconnect();
            if (_inspector != null) DestroyImmediate(_inspector);
        }
        private void Pump() => _net?.PollEvents();

        private void OnFocus() => InvalidateCache(); // pick up assets created/edited in the Project window

        private void BuildCategories()
        {
            _cats = new List<Category>
            {
                new() { Name = "Classes", Icon = "🛡", SoType = typeof(ClassDefinitionSO),  Folder = "Classes", ToContent = so => ("class",  ((ClassDefinitionSO)so).id,  JsonUtility.ToJson(ToDto((ClassDefinitionSO)so))) },
                new() { Name = "Races",   Icon = "🧬", SoType = typeof(RaceDefinitionSO),   Folder = "Races",   ToContent = so => ("race",   ((RaceDefinitionSO)so).id,   JsonUtility.ToJson(ToDto((RaceDefinitionSO)so))) },
                new() { Name = "Genders", Icon = "⚥", SoType = typeof(GenderDefinitionSO), Folder = "Genders", ToContent = so => ("gender", ((GenderDefinitionSO)so).id, JsonUtility.ToJson(ToDto((GenderDefinitionSO)so))) },
                // Playable archetypes (Atavism "Player Template"): binds race+class + models per gender. Client-only (ToContent = null) — race/class STATS sync via the catalogs above.
                new() { Name = "Templates", Icon = "🎭", SoType = typeof(CharacterTemplateSO), Folder = "Templates", ToContent = null },
                // Items: authored as SOs (icon/art client-side); synced to the TYPED item-template tables (see SyncAll).
                new() { Name = "Items", Icon = "🎒", SoType = typeof(ItemDefinitionSO), Folder = "Items", ToContent = null },
                // Skills + Statuses: authored as SOs (icon client-side); synced to the TYPED Ability/Status tables (see SyncAll).
                new() { Name = "Skills",   Icon = "✨", SoType = typeof(SkillDefinitionSO),  Folder = "Skills",   ToContent = null },
                new() { Name = "Statuses", Icon = "☠", SoType = typeof(StatusDefinitionSO), Folder = "Statuses", ToContent = null },
                // Monsters: authored as SOs (stats/XP/loot); synced to the TYPED Monster table (see SyncAll).
                new() { Name = "Monsters", Icon = "👹", SoType = typeof(MonsterDefinitionSO), Folder = "Monsters", ToContent = null },
                // Resource nodes (trees/rocks): authored as SOs; synced to the TYPED ResourceNode table (see SyncAll).
                new() { Name = "Resources", Icon = "🌳", SoType = typeof(ResourceNodeDefinitionSO), Folder = "Resources", ToContent = null },
                // Crafting recipes: authored as SOs; synced to the TYPED Recipe table (see SyncAll).
                new() { Name = "Recipes", Icon = "📜", SoType = typeof(RecipeDefinitionSO), Folder = "Recipes", ToContent = null },
                // Currencies: authored as SOs; synced to the TYPED Currency table (see SyncAll).
                new() { Name = "Currencies", Icon = "💰", SoType = typeof(CurrencyDefinitionSO), Folder = "Currencies", ToContent = null },
                // Vendors: authored as SOs; synced to the TYPED Vendor table (see SyncAll).
                new() { Name = "Vendors", Icon = "🏪", SoType = typeof(VendorDefinitionSO), Folder = "Vendors", ToContent = null },
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
            // Items → the TYPED item-template tables in content.db (not the generic Content store).
            Send(new I_Db_ClearItemTemplates());
            int items = 0;
            foreach (var so in FindAllOf<ItemDefinitionSO>())
                if (!string.IsNullOrWhiteSpace(so.id)) { Send(new I_Db_UpsertItemTemplate { Template = ToItemTemplate(so) }); items++; }

            // Races/Classes/Genders → the TYPED stats-content tables (what the server actually reads). The legacy
            // Content(Json) upserts above are ignored by the server after first boot — this is the real sync path.
            var races = BuildRaces();
            var classes = BuildClasses();
            var genders = BuildGenders();
            Send(new I_Db_UpsertStatsContent { Races = races, Classes = classes, Genders = genders });

            // Skills + Statuses → the TYPED Ability/Status tables in content.db (upsert by id; the server loads them on boot).
            int skills = 0;
            foreach (var so in FindAllOf<SkillDefinitionSO>())
                if (so.id is > 0 and < 256) { Send(new I_Db_UpsertAbility { Ability = ToAbilityRecord(so) }); skills++; }
            int statuses = 0;
            foreach (var so in FindAllOf<StatusDefinitionSO>())
                if (!string.IsNullOrWhiteSpace(so.id)) { Send(new I_Db_UpsertStatus { Status = ToStatusRecord(so) }); statuses++; }

            // Monsters → the TYPED Monster/MonsterLoot tables in content.db.
            int monsters = 0;
            foreach (var so in FindAllOf<MonsterDefinitionSO>())
                if (!string.IsNullOrWhiteSpace(so.id)) { Send(new I_Db_UpsertMonster { Monster = ToMonsterRecord(so) }); monsters++; }

            // Resource nodes → the TYPED ResourceNode/ResourceYield tables in content.db.
            int nodes = 0;
            foreach (var so in FindAllOf<ResourceNodeDefinitionSO>())
                if (!string.IsNullOrWhiteSpace(so.id)) { Send(new I_Db_UpsertResourceNode { Node = ToResourceNodeRecord(so) }); nodes++; }

            // Recipes → the TYPED Recipe/RecipeIngredient tables in content.db.
            int recipes = 0;
            foreach (var so in FindAllOf<RecipeDefinitionSO>())
                if (!string.IsNullOrWhiteSpace(so.id)) { Send(new I_Db_UpsertRecipe { Recipe = ToRecipeRecord(so) }); recipes++; }

            // Currencies → the TYPED Currency table.
            int currencies = 0;
            foreach (var so in FindAllOf<CurrencyDefinitionSO>())
                if (!string.IsNullOrWhiteSpace(so.id)) { Send(new I_Db_UpsertCurrency { Currency = ToCurrencyRecord(so) }); currencies++; }

            // Vendors → the TYPED Vendor/VendorStock tables.
            int vendors = 0;
            foreach (var so in FindAllOf<VendorDefinitionSO>())
                if (!string.IsNullOrWhiteSpace(so.id)) { Send(new I_Db_UpsertVendor { Vendor = ToVendorRecord(so) }); vendors++; }

            _status = $"Mirror ✓ — {items} item(ns) + {races.Length}r/{classes.Length}c/{genders.Length}g + {skills} skill(s) + {statuses} status(es) + {monsters} monstro(s) + {nodes} nó(s) + {recipes} receita(s) + {currencies} moeda(s) + {vendors} vendedor(es).";
        }

        /// <summary>ItemDefinitionSO → the server's ItemTemplate. The SO uses the shared enums directly (no parsing);
        /// stat ids are emitted as the StatId enum name. The icon is client art (only its name travels).</summary>
        private static ItemTemplate ToItemTemplate(ItemDefinitionSO so)
        {
            var t = new ItemTemplate
            {
                Id = so.id, Name = so.displayName ?? "", Description = so.description ?? "",
                Icon = so.icon != null ? so.icon.name : "", Model3D = so.model3D != null ? so.model3D.name : "",
                Type = so.type,
                Category = so.category ?? "",
                Slot = so.slot,
                TwoHanded = so.twoHanded,
                Rarity = so.rarity,
                ElementId = so.element.ToString(),
                LevelReq = so.levelReq, ClassReq = so.classReq ?? "",
                MaxRolls = so.maxRolls,
                TierMax = (byte)Mathf.Clamp(so.tierMax, 0, 255),
                EnhanceMax = (byte)Mathf.Clamp(so.enhanceMax, 0, 255),
                SocketsMax = (byte)Mathf.Clamp(so.socketsMax, 0, 255),
                DurabilityMax = so.durabilityMax, Weight = so.weight,
                Sellable = so.sellable, Tradeable = so.tradeable, NpcPrice = so.npcPrice, PriceCurrency = so.priceCurrency ?? "",
                StackMax = (ushort)Mathf.Clamp(so.stackMax, 1, ushort.MaxValue),
            };
            if (so.statsBase != null) foreach (var s in so.statsBase) if (s.statId != StatId.None) t.StatsBase[s.statId.ToString()] = s.value;
            if (so.rollsPossible != null) foreach (var r in so.rollsPossible) if (r.statId != StatId.None) t.RollsPossible.Add(new ItemTemplate.RollRange { StatId = r.statId.ToString(), Min = r.min, Max = r.max });
            if (so.effects != null) foreach (var e in so.effects) if (e.kind != ConsumableEffectKind.None) t.Effects.Add(new ItemEffect { Kind = e.kind, Stat = e.statId, Amount = e.amount, DurationSeconds = e.durationSeconds });
            return t;
        }

        /// <summary>SkillDefinitionSO → the server's AbilityRecord (shared enums cast to bytes; icon is client art).</summary>
        private static AbilityRecord ToAbilityRecord(SkillDefinitionSO so)
        {
            var effects = new List<AbilityRecord.EffectRecord>();
            if (so.effects != null)
                foreach (var e in so.effects)
                    effects.Add(new AbilityRecord.EffectRecord { Type = (byte)e.type, Amount = e.amount, ScalesWith = (byte)e.scalesWith, StatusId = e.statusId ?? "" });
            return new AbilityRecord
            {
                Id = (byte)Mathf.Clamp(so.id, 1, 255),
                Name = so.displayName ?? "",
                CastTime = so.castTime, Cooldown = so.cooldown, Cost = so.cost,
                Targeting = (byte)so.targeting, Range = so.range, ConeAngle = so.coneAngle, Width = so.width,
                Element = (byte)so.element,
                RequiredWeapon = so.requiredWeapon ?? "",
                Effects = effects.ToArray(),
            };
        }

        /// <summary>MonsterDefinitionSO → the server's MonsterRecord (loot items emit their template id).</summary>
        private static MonsterRecord ToMonsterRecord(MonsterDefinitionSO so)
        {
            var loot = new System.Collections.Generic.List<MonsterRecord.LootEntry>();
            if (so.loot != null)
                foreach (var e in so.loot)
                    if (e.item != null && !string.IsNullOrWhiteSpace(e.item.id))
                        loot.Add(new MonsterRecord.LootEntry { ItemId = e.item.id, ChancePct = e.chancePct, Min = e.min, Max = e.max });
            return new MonsterRecord
            {
                Id = so.id, Name = so.displayName ?? "",
                Level = (ushort)Mathf.Clamp(so.level, 1, ushort.MaxValue),
                Str = so.str, Dex = so.dex, Int = so.intel, Vit = so.vit, Spi = so.spi, Luk = so.luk, Armor = so.armor,
                Element = (byte)so.element,
                AggroRadius = so.aggroRadius, LeashRadius = so.leashRadius, AttackRange = so.attackRange, MoveSpeed = so.moveSpeed,
                AttackAbilityId = (byte)Mathf.Clamp(so.attackAbilityId, 1, 255),
                XpReward = (uint)Mathf.Max(0, so.xpReward),
                Disposition = (byte)so.disposition,
                Kind = (byte)so.kind,
                MaxHp = Mathf.Max(0, so.maxHp),
                Loot = loot.ToArray(),
            };
        }

        /// <summary>ResourceNodeDefinitionSO → the server's ResourceNodeRecord (yield items emit their template id).</summary>
        private static ResourceNodeRecord ToResourceNodeRecord(ResourceNodeDefinitionSO so)
        {
            var yields = new System.Collections.Generic.List<ResourceNodeRecord.Yield>();
            if (so.yields != null)
                foreach (var y in so.yields)
                    if (y.item != null && !string.IsNullOrWhiteSpace(y.item.id))
                        yields.Add(new ResourceNodeRecord.Yield { ItemId = y.item.id, ChancePct = y.chancePct, Min = y.min, Max = y.max });
            return new ResourceNodeRecord
            {
                Id = so.id, Name = so.displayName ?? "",
                Profession = (byte)so.profession, RequiredLevel = (ushort)Mathf.Max(1, so.requiredLevel), RequiredTool = so.requiredTool ?? "",
                GatherSeconds = so.gatherSeconds, Charges = Mathf.Max(1, so.charges), RespawnSeconds = so.respawnSeconds,
                XpReward = (uint)Mathf.Max(0, so.xpReward),
                Yields = yields.ToArray(),
            };
        }

        /// <summary>RecipeDefinitionSO → the server's RecipeRecord (output + ingredients emit their template ids).</summary>
        private static RecipeRecord ToRecipeRecord(RecipeDefinitionSO so)
        {
            var ings = new System.Collections.Generic.List<RecipeRecord.Ingredient>();
            if (so.ingredients != null)
                foreach (var ing in so.ingredients)
                    if (ing.item != null && !string.IsNullOrWhiteSpace(ing.item.id) && ing.qty > 0)
                        ings.Add(new RecipeRecord.Ingredient { ItemId = ing.item.id, Qty = ing.qty });
            return new RecipeRecord
            {
                Id = so.id, Name = so.displayName ?? "",
                Profession = (byte)so.profession, RequiredLevel = (ushort)Mathf.Max(1, so.requiredLevel),
                CraftSeconds = so.craftSeconds, XpReward = (uint)Mathf.Max(0, so.xpReward),
                OutputItemId = so.output != null ? so.output.id : "", OutputQty = Mathf.Max(1, so.outputQty),
                Ingredients = ings.ToArray(),
            };
        }

        /// <summary>CurrencyDefinitionSO → CurrencyRecord (icon emits its sprite name as the client asset id).</summary>
        private static CurrencyRecord ToCurrencyRecord(CurrencyDefinitionSO so) => new()
        {
            Id = so.id, Name = so.displayName ?? "", Icon = so.icon != null ? so.icon.name : "", SortOrder = so.sortOrder,
        };

        /// <summary>VendorDefinitionSO → VendorRecord (stock emits each item's template id).</summary>
        private static VendorRecord ToVendorRecord(VendorDefinitionSO so)
        {
            var stock = new System.Collections.Generic.List<string>();
            if (so.stock != null)
                foreach (var it in so.stock)
                    if (it != null && !string.IsNullOrWhiteSpace(it.id)) stock.Add(it.id);
            return new VendorRecord { Id = so.id, Name = so.displayName ?? "", StockItemIds = stock.ToArray() };
        }

        /// <summary>StatusDefinitionSO → the server's StatusRecord.</summary>
        private static StatusRecord ToStatusRecord(StatusDefinitionSO so) => new()
        {
            Id = so.id, Name = so.displayName ?? "",
            Kind = (byte)so.kind, Duration = so.duration, TickInterval = so.tickInterval,
            Element = (byte)so.element, Amount = so.amount,
            Stacking = (byte)so.stacking, MaxStacks = so.maxStacks, Stat = (byte)so.stat,
        };

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

        // ── typed stats-content builders (SO → the records the server reads from content.db) ──
        private static RaceRecord[] BuildRaces()
        {
            var sos = FindAllOf<RaceDefinitionSO>();
            var arr = new RaceRecord[sos.Length];
            for (int i = 0; i < sos.Length; i++)
            {
                var r = sos[i];
                arr[i] = new RaceRecord
                {
                    Id = r.id, Name = r.displayName, Element = ParseElement(r.element),
                    Str = r.str, Dex = r.dex, Int = r.intel, Vit = r.vit, Spi = r.spi, Luk = r.luk, Armor = r.armor,
                    HomeZoneId = r.homeZoneId,
                };
            }
            return arr;
        }

        private static ClassRecord[] BuildClasses()
        {
            var sos = FindAllOf<ClassDefinitionSO>();
            var arr = new ClassRecord[sos.Length];
            for (int i = 0; i < sos.Length; i++)
            {
                var c = sos[i];
                arr[i] = new ClassRecord
                {
                    Id = c.id, Name = c.displayName,
                    Str = c.str, Dex = c.dex, Int = c.intel, Vit = c.vit, Spi = c.spi, Luk = c.luk,
                    StrPerLevel = c.strPerLevel, DexPerLevel = c.dexPerLevel, IntPerLevel = c.intPerLevel,
                    VitPerLevel = c.vitPerLevel, SpiPerLevel = c.spiPerLevel, LukPerLevel = c.lukPerLevel,
                    SkillIds = ToSkillIdBytes(c.skillIds),
                };
            }
            return arr;
        }

        private static GenderRecord[] BuildGenders()
        {
            var sos = FindAllOf<GenderDefinitionSO>();
            var arr = new GenderRecord[sos.Length];
            for (int i = 0; i < sos.Length; i++) arr[i] = new GenderRecord { Id = sos[i].id, Name = sos[i].displayName };
            return arr;
        }

        private static byte ParseElement(string s) => System.Enum.TryParse<ElementType>(s, true, out var e) ? (byte)e : (byte)0;

        /// <summary>Class skill ids (int, 1..255) → the byte[] the server reads (dedup + in-range).</summary>
        private static byte[] ToSkillIdBytes(List<int> ids)
        {
            if (ids == null || ids.Count == 0) return System.Array.Empty<byte>();
            var seen = new HashSet<byte>();
            foreach (var i in ids) if (i is > 0 and < 256) seen.Add((byte)i);
            var arr = new byte[seen.Count]; seen.CopyTo(arr); return arr;
        }

        private void CollectIntoLibrary()
        {
            var found = FindAllOf<ContentLibrary>();
            ContentLibrary lib = found.Length > 0 ? found[0] : CreateLibrary();
            lib.classes = new List<ClassDefinitionSO>(FindAllOf<ClassDefinitionSO>());
            lib.races = new List<RaceDefinitionSO>(FindAllOf<RaceDefinitionSO>());
            lib.genders = new List<GenderDefinitionSO>(FindAllOf<GenderDefinitionSO>());
            lib.templates = new List<CharacterTemplateSO>(FindAllOf<CharacterTemplateSO>());
            lib.items = new List<ItemDefinitionSO>(FindAllOf<ItemDefinitionSO>());
            lib.skills = new List<SkillDefinitionSO>(FindAllOf<SkillDefinitionSO>());
            lib.statuses = new List<StatusDefinitionSO>(FindAllOf<StatusDefinitionSO>());
            lib.monsters = new List<MonsterDefinitionSO>(FindAllOf<MonsterDefinitionSO>());
            lib.resourceNodes = new List<ResourceNodeDefinitionSO>(FindAllOf<ResourceNodeDefinitionSO>());
            lib.recipes = new List<RecipeDefinitionSO>(FindAllOf<RecipeDefinitionSO>());
            lib.currencies = new List<CurrencyDefinitionSO>(FindAllOf<CurrencyDefinitionSO>());
            lib.vendors = new List<VendorDefinitionSO>(FindAllOf<VendorDefinitionSO>());
            EditorUtility.SetDirty(lib);
            AssetDatabase.SaveAssets();
            _status = $"ContentLibrary: {lib.classes.Count} classes, {lib.races.Count} raças, {lib.genders.Count} gêneros, {lib.templates.Count} templates, {lib.items.Count} itens, {lib.skills.Count} skills, {lib.statuses.Count} status, {lib.monsters.Count} monstros.";
            Selection.activeObject = lib;
        }

        // ───────────────────────── UI ─────────────────────────
        private void OnGUI()
        {
            EnsureStyles();
            if (Event.current.type == EventType.MouseMove) Repaint();

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
            var rect = EditorGUILayout.GetControlRect(false, 40);
            EditorGUI.DrawRect(rect, ColHeader);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 2, rect.width, 2), ColAccent); // accent underline

            var title = new GUIStyle(EditorStyles.boldLabel) { fontSize = 15, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(12, 0, 0, 0) };
            title.normal.textColor = ColText;
            EditorGUI.LabelField(new Rect(rect.x, rect.y + 2, rect.width, 22), "⚔  ArcaneMMO — Content Editor", title);

            var sub = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleLeft, padding = new RectOffset(14, 0, 0, 0) };
            sub.normal.textColor = ColMuted;
            EditorGUI.LabelField(new Rect(rect.x, rect.y + 20, rect.width, 16), "ScriptableObjects → DB tipado · edição por dropdowns", sub);
        }

        /// <summary>A selectable list row with optional thumbnail + count badge, accent bar when selected, hover tint.</summary>
        private bool Row(string label, int count, bool selected, float height, Texture thumb = null)
        {
            Rect r = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(height), GUILayout.ExpandWidth(true));
            bool hover = r.Contains(Event.current.mousePosition);
            if (selected) EditorGUI.DrawRect(r, ColRowSel);
            else if (hover) EditorGUI.DrawRect(r, ColRowHover);
            if (selected) EditorGUI.DrawRect(new Rect(r.x, r.y, 3, r.height), ColAccent);

            float x = r.x + 8;
            if (thumb != null) { GUI.DrawTexture(new Rect(x, r.y + (height - 22) / 2, 22, 22), thumb, ScaleMode.ScaleToFit); x += 28; }
            GUI.Label(new Rect(x, r.y, r.width - (x - r.x) - 30, r.height), label, _rowLabel);
            if (count >= 0) GUI.Label(new Rect(r.xMax - 30, r.y, 26, r.height), count.ToString(), _rowMuted);

            if (Event.current.type == EventType.MouseDown && hover && Event.current.button == 0) { Event.current.Use(); return true; }
            return false;
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
                GUILayout.Space(6);
                GUILayout.Label("Zona", GUILayout.Width(34));
                _spawnZone = EditorGUILayout.IntField(_spawnZone, GUILayout.Width(34));
                using (new EditorGUI.DisabledScope(!_connected))
                    if (GUILayout.Button("⛺ Export Spawners", GUILayout.Width(140))) ExportSpawners();
            }
        }

        /// <summary>Reads every SpawnMarker in the open scene → wipes the zone's spawners in content.db, then inserts one
        /// per marker (global x/z; the server converts to local). Author placement in the WORLD scene, then click this.</summary>
        private void ExportSpawners()
        {
            if (!_connected) { _status = "Conecte primeiro."; return; }
            var markers = UnityEngine.Object.FindObjectsByType<SpawnMarker>(FindObjectsSortMode.None);
            Send(new I_Db_ClearSpawners { ZoneId = (byte)_spawnZone });
            int n = 0;
            foreach (var mk in markers)
            {
                // priority: vendor > node > monster; skip empty markers.
                byte kind; string contentId;
                if (mk.vendor != null && !string.IsNullOrWhiteSpace(mk.vendor.id)) { kind = 2; contentId = mk.vendor.id; }
                else if (mk.node != null && !string.IsNullOrWhiteSpace(mk.node.id)) { kind = 1; contentId = mk.node.id; }
                else if (mk.monster != null && !string.IsNullOrWhiteSpace(mk.monster.id)) { kind = 0; contentId = mk.monster.id; }
                else continue;
                var pos = mk.transform.position;
                Send(new I_Db_UpsertSpawner
                {
                    Spawner = new SpawnerRecord
                    {
                        ZoneId = (byte)_spawnZone, Kind = kind, MonsterId = contentId,
                        X = pos.x, Z = pos.z, Radius = mk.radius, Count = mk.count, RespawnSeconds = mk.respawnSeconds,
                    }
                });
                n++;
            }
            _status = markers.Length == 0 ? "Nenhum SpawnMarker na cena." : $"Spawners exportados (zona {_spawnZone}): {n}. Reinicie a zona.";
        }

        private void DrawCategories()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Width(156)))
            {
                EditorGUILayout.LabelField("CONTEÚDO", _sectionLabel);
                EditorGUILayout.Space(2);
                _catScroll = EditorGUILayout.BeginScrollView(_catScroll);
                for (int i = 0; i < _cats.Count; i++)
                {
                    int count = CachedAll(_cats[i]).Length;
                    if (Row($"{_cats[i].Icon}  {_cats[i].Name}", count, _cat == i, 30) && _cat != i) { _cat = i; Select(null); }
                }
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawItems()
        {
            var cat = _cats[_cat];
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Width(214)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField($"{cat.Icon}  {cat.Name}", EditorStyles.boldLabel);
                    if (GUILayout.Button("↻", GUILayout.Width(24))) InvalidateCache(); // manual rescan
                }
                _search = EditorGUILayout.TextField(_search, EditorStyles.toolbarSearchField);
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
                var all = CachedAll(cat);
                int shown = 0;
                foreach (var so in all)
                {
                    if (!string.IsNullOrEmpty(_search) && so.name.IndexOf(_search, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    Texture thumb = (so is ItemDefinitionSO it && it.icon != null) ? (AssetPreview.GetAssetPreview(it.icon) ?? it.icon.texture) : null;
                    if (Row(so.name, -1, _selected == so, 28, thumb)) Select(so);
                    shown++;
                }
                if (shown == 0) EditorGUILayout.LabelField(all.Length == 0 ? "— vazio —" : "— sem resultados —", _rowMuted);
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
            if (idProp != null && idProp.propertyType == SerializedPropertyType.String) idProp.stringValue = Sanitize(name); // skills use an int id → set it manually
            if (nameProp != null) nameProp.stringValue = name;
            sb.ApplyModifiedProperties();

            AssetDatabase.SaveAssets();
            InvalidateCache();
            Select(so);
            _newName = "";
            _status = $"Criado: {name} em {cat.Folder}/";
        }

        private void RenameToId()
        {
            var idProp = new SerializedObject(_selected).FindProperty("id");
            string id = idProp != null && idProp.propertyType == SerializedPropertyType.String ? idProp.stringValue : null;
            if (string.IsNullOrWhiteSpace(id)) { _status = "id vazio (ou id numérico) — renomeie manualmente."; return; }
            AssetDatabase.RenameAsset(AssetDatabase.GetAssetPath(_selected), id);
            AssetDatabase.SaveAssets();
            InvalidateCache();
            _status = $"Renomeado → {id}";
        }

        private void DeleteSelected()
        {
            if (_selected == null) return;
            if (!EditorUtility.DisplayDialog("Excluir", $"Apagar '{_selected.name}'?", "Apagar", "Cancelar")) return;
            AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(_selected));
            InvalidateCache();
            Select(null);
        }

        private void Select(UnityEngine.Object o)
        {
            _selected = o;
            if (_inspector != null) { DestroyImmediate(_inspector); _inspector = null; }
            Repaint();
        }

        // ── assets ──
        // Per-category cache: scanning the AssetDatabase every repaint (and we repaint on every mouse move) was the
        // cause of the editor lag. We cache the loaded lists and only rescan on create/delete/rename, on focus, or ↻.
        private readonly Dictionary<Type, ScriptableObject[]> _cache = new();

        /// <summary>Cached asset list for a category (rescans only when the cache was invalidated).</summary>
        private ScriptableObject[] CachedAll(Category cat)
        {
            if (!_cache.TryGetValue(cat.SoType, out var arr)) { arr = FindAll(cat); _cache[cat.SoType] = arr; }
            return arr;
        }

        private void InvalidateCache() => _cache.Clear();

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
