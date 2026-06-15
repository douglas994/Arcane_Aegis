using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using TMPro;
using Arcane_Aegis.UI;
using Arcane_Aegis.Content;
using Arcane_Aegis.Controllers;

namespace Arcane_Aegis.EditorTools
{
    /// <summary>
    /// One-click builders for the gameplay UI panels (Shop, Crafting, Currency HUD, Profession, progress bars). They
    /// create a FUNCTIONAL skeleton with the right hierarchy + the component refs already wired (ContentLibrary
    /// auto-found) — then you restyle it by hand to taste. Menu: ArcaneMMO ▸ UI ▸ …
    ///
    /// Pattern: components that use a static Instance (ShopPanel, GatherProgress, CraftProgress) sit on an ALWAYS-ACTIVE
    /// holder, with the visual as a child (the toggled "panel"); the component's Awake hides it. Panels that just build
    /// on enable (Crafting/Profession) get a UIPanelToggle (hotkey) on the holder. The HUD is always visible.
    /// </summary>
    public static class UIBuilder
    {
        private static readonly Color Bg = new(0.08f, 0.09f, 0.12f, 0.92f);
        private static readonly Color BgSoft = new(1f, 1f, 1f, 0.05f);
        private static readonly Color Accent = new(0.85f, 0.7f, 0.3f, 1f);

        // ── menu items ──
        [MenuItem("ArcaneMMO/UI/Shop Panel")]
        public static void BuildShop()
        {
            var canvas = GetOrCreateCanvas();
            var holder = Holder(canvas.transform, "ShopPanel");
            var shop = holder.AddComponent<ShopPanel>();
            var window = WindowVisual(holder.transform, new Vector2(520, 560), out var body);

            var title = Label(body, "Title", "Loja", 22, TextAlignmentOptions.Center); Top(title.rectTransform, 36);
            var buyHead = Label(body, "BuyHeader", "Comprar", 16, TextAlignmentOptions.Left, Accent); Band(buyHead.rectTransform, 44, 22);
            var buyContent = ScrollList(body, "BuyScroll", 70, 200);
            var sellHead = Label(body, "SellHeader", "Vender", 16, TextAlignmentOptions.Left, Accent); Band(sellHead.rectTransform, 282, 22);
            var sellContent = ScrollList(body, "SellScroll", 308, 200);
            var close = Button(body, "CloseButton", "Fechar", new Vector2(120, 34)); Bottom(close.GetComponent<RectTransform>(), 12);

            var rowTemplate = ShopRowTemplate(holder.transform);

            var so = new SerializedObject(shop);
            Set(so, "library", FindLibrary());
            Set(so, "panel", window);
            Set(so, "titleLabel", title);
            Set(so, "rowPrefab", rowTemplate);
            Set(so, "buyParent", buyContent);
            Set(so, "sellParent", sellContent);
            so.ApplyModifiedProperties();

            AddClick(close, shop, nameof(ShopPanel.Close));
            EnsureController<ShopController>("ShopController");
            Done(holder, "Shop Panel (E perto do vendedor abre)");
        }

        [MenuItem("ArcaneMMO/UI/Crafting Panel")]
        public static void BuildCrafting()
        {
            var canvas = GetOrCreateCanvas();
            var holder = Holder(canvas.transform, "CraftingPanel");
            var window = WindowVisual(holder.transform, new Vector2(520, 520), out var body);
            var craft = window.AddComponent<CraftingPanel>();

            var title = Label(body, "Title", "Criação", 22, TextAlignmentOptions.Center); Top(title.rectTransform, 36);
            var content = ScrollList(body, "RecipeScroll", 48, 420);
            var rowTemplate = CraftRowTemplate(holder.transform);

            var so = new SerializedObject(craft);
            Set(so, "library", FindLibrary());
            Set(so, "rowPrefab", rowTemplate);
            Set(so, "listParent", content);
            so.ApplyModifiedProperties();

            AddToggle(holder, window, Key.C);
            Done(holder, "Crafting Panel (tecla C abre)");
        }

        [MenuItem("ArcaneMMO/UI/Profession Panel")]
        public static void BuildProfession()
        {
            var canvas = GetOrCreateCanvas();
            var holder = Holder(canvas.transform, "ProfessionPanel");
            var window = WindowVisual(holder.transform, new Vector2(360, 220), out var body);
            var panel = window.AddComponent<ProfessionPanel>();
            var title = Label(body, "Title", "Profissões", 20, TextAlignmentOptions.Center); Top(title.rectTransform, 32);

            var row = ProfessionRow(body, out var nameL, out var levelL, out var fill, out var xpL); Band(RT(row), 48, 44);

            var so = new SerializedObject(panel);
            var rows = so.FindProperty("rows"); rows.arraySize = 1;
            var e = rows.GetArrayElementAtIndex(0);
            e.FindPropertyRelative("profession").enumValueIndex = 0; // Woodcutting
            e.FindPropertyRelative("nameLabel").objectReferenceValue = nameL;
            e.FindPropertyRelative("levelLabel").objectReferenceValue = levelL;
            e.FindPropertyRelative("fill").objectReferenceValue = fill;
            e.FindPropertyRelative("xpLabel").objectReferenceValue = xpL;
            so.ApplyModifiedProperties();

            AddToggle(holder, window, Key.P);
            Done(holder, "Profession Panel (tecla P abre)");
        }

        [MenuItem("ArcaneMMO/UI/Currency HUD")]
        public static void BuildCurrencyHud()
        {
            var canvas = GetOrCreateCanvas();
            var root = Panel(canvas.transform, "CurrencyHud", new Vector2(220, 40), BgSoft);
            var rt = RT(root); rt.anchorMin = rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(1, 1); rt.anchoredPosition = new Vector2(-20, -20);
            var hud = root.AddComponent<CurrencyHud>();

            var icon = Img(root.transform, "GoldIcon", new Vector2(24, 24));
            var irt = RT(icon); irt.anchorMin = irt.anchorMax = new Vector2(0, 0.5f); irt.pivot = new Vector2(0, 0.5f); irt.anchoredPosition = new Vector2(10, 0);
            var amount = Label(root, "GoldAmount", "0", 18, TextAlignmentOptions.Right);
            var art = amount.rectTransform; art.anchorMin = new Vector2(1, 0.5f); art.anchorMax = new Vector2(1, 0.5f); art.pivot = new Vector2(1, 0.5f); art.anchoredPosition = new Vector2(-10, 0); art.sizeDelta = new Vector2(140, 30);

            var so = new SerializedObject(hud);
            var rows = so.FindProperty("rows"); rows.arraySize = 1;
            var e = rows.GetArrayElementAtIndex(0);
            e.FindPropertyRelative("currencyId").stringValue = "gold";
            e.FindPropertyRelative("amountLabel").objectReferenceValue = amount;
            e.FindPropertyRelative("icon").objectReferenceValue = icon.GetComponent<Image>();
            so.ApplyModifiedProperties();
            Done(root, "Currency HUD (row: gold)");
        }

        [MenuItem("ArcaneMMO/UI/Gather + Craft Bars")]
        public static void BuildBars()
        {
            var canvas = GetOrCreateCanvas();
            BuildBar<GatherProgress>(canvas, "GatherProgress", "Coletando…", 90);
            BuildBar<CraftProgress>(canvas, "CraftProgress", "Criando…", 122);
            Debug.Log("[UIBuilder] Barras de coleta + criação criadas.");
        }

        private static void BuildBar<T>(Canvas canvas, string name, string text, float y) where T : Component
        {
            var holder = Holder(canvas.transform, name);
            var comp = holder.AddComponent<T>();
            var bar = Panel(holder.transform, "Bar", new Vector2(300, 26), Bg);
            var brt = RT(bar); brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 0f); brt.pivot = new Vector2(0.5f, 0f); brt.anchoredPosition = new Vector2(0, y);
            var fillGo = Img(bar.transform, "Fill", Vector2.zero); var frt = RT(fillGo); Stretch(frt); frt.offsetMin = new Vector2(2, 2); frt.offsetMax = new Vector2(-2, -2);
            var fill = fillGo.GetComponent<Image>(); fill.color = Accent; fill.type = Image.Type.Filled; fill.fillMethod = Image.FillMethod.Horizontal; fill.fillAmount = 0.4f;
            var label = Label(bar, "Label", text, 13, TextAlignmentOptions.Center); Stretch(label.rectTransform);

            var so = new SerializedObject(comp);
            Set(so, "panel", bar);   // Awake hides this
            Set(so, "fill", fill);
            so.ApplyModifiedProperties();
        }

        // ── structure helpers ──
        private static GameObject Holder(Transform parent, string name)
        {
            var go = New(name, parent); Stretch(RT(go));
            Undo.RegisterCreatedObjectUndo(go, "Create " + name);
            return go; // always active; the component lives here
        }

        /// <summary>A centered window (dark bg) + an inset 'body' for content. This is the toggled visual.</summary>
        private static GameObject WindowVisual(Transform holder, Vector2 size, out RectTransform body)
        {
            var win = Panel(holder, "Window", size, Bg);
            var rt = RT(win); rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); rt.pivot = new Vector2(0.5f, 0.5f); rt.anchoredPosition = Vector2.zero;
            var b = New("Body", win.transform); body = RT(b); Stretch(body); body.offsetMin = new Vector2(12, 12); body.offsetMax = new Vector2(-12, -12);
            return win;
        }

        private static GameObject Panel(Transform parent, string name, Vector2 size, Color color)
        {
            var go = New(name, parent); RT(go).sizeDelta = size; go.AddComponent<Image>().color = color;
            return go;
        }

        private static RectTransform ScrollList(RectTransform parentBody, string name, float topInset, float height)
        {
            var sv = New(name, parentBody); var svrt = RT(sv);
            svrt.anchorMin = new Vector2(0, 1); svrt.anchorMax = new Vector2(1, 1); svrt.pivot = new Vector2(0.5f, 1);
            svrt.anchoredPosition = new Vector2(0, -topInset); svrt.sizeDelta = new Vector2(0, height);
            sv.AddComponent<Image>().color = BgSoft;
            var sr = sv.AddComponent<ScrollRect>(); sr.horizontal = false;

            var viewport = New("Viewport", sv.transform); var vrt = RT(viewport); Stretch(vrt);
            // RectMask2D: rectangular clip with no stencil/material — far more reliable than Mask+Image for scroll lists.
            viewport.AddComponent<RectMask2D>();

            var content = New("Content", viewport.transform); var crt = RT(content);
            crt.anchorMin = new Vector2(0, 1); crt.anchorMax = new Vector2(1, 1); crt.pivot = new Vector2(0.5f, 1); crt.anchoredPosition = Vector2.zero; crt.sizeDelta = Vector2.zero;
            var vlg = content.AddComponent<VerticalLayoutGroup>(); vlg.childControlWidth = true; vlg.childForceExpandWidth = true; vlg.childControlHeight = true; vlg.childForceExpandHeight = false; vlg.spacing = 4; vlg.padding = new RectOffset(4, 4, 4, 4);
            var csf = content.AddComponent<ContentSizeFitter>(); csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            sr.viewport = vrt; sr.content = crt;
            return crt;
        }

        private static GameObject ShopRowTemplate(Transform parent)
        {
            var row = Row(parent, "ShopRowTemplate");
            var icon = Img(row.transform, "Icon", new Vector2(32, 32)); Fixed(icon, 32);
            var name = Label(row, "Name", "Item", 15, TextAlignmentOptions.Left); Flexible(name.gameObject);
            var price = Label(row, "Price", "0", 14, TextAlignmentOptions.Right); Fixed(price.gameObject, 90);
            var btn = Button(row.transform, "Action", "Comprar", new Vector2(90, 30)); Fixed(btn, 92);

            var sr = row.AddComponent<ShopRow>();
            var so = new SerializedObject(sr);
            Set(so, "icon", icon.GetComponent<Image>());
            Set(so, "nameLabel", name);
            Set(so, "priceLabel", price);
            Set(so, "button", btn.GetComponent<Button>());
            so.ApplyModifiedProperties();
            row.SetActive(false);
            return row;
        }

        private static GameObject CraftRowTemplate(Transform parent)
        {
            var row = Row(parent, "CraftRowTemplate");
            var icon = Img(row.transform, "Icon", new Vector2(32, 32)); Fixed(icon, 32);
            var name = Label(row, "Name", "Receita", 15, TextAlignmentOptions.Left); Flexible(name.gameObject);
            var ing = Label(row, "Ingredients", "Madeira 0/3", 12, TextAlignmentOptions.Left); Fixed(ing.gameObject, 150);
            var btn = Button(row.transform, "Craft", "Criar", new Vector2(80, 30)); Fixed(btn, 82);

            var cr = row.AddComponent<CraftRow>();
            var so = new SerializedObject(cr);
            Set(so, "icon", icon.GetComponent<Image>());
            Set(so, "nameLabel", name);
            Set(so, "ingredientsLabel", ing);
            Set(so, "craftButton", btn.GetComponent<Button>());
            so.ApplyModifiedProperties();
            row.SetActive(false);
            return row;
        }

        private static GameObject ProfessionRow(RectTransform body, out TMP_Text nameL, out TMP_Text levelL, out Image fill, out TMP_Text xpL)
        {
            var row = New("Row_Lenhador", body); var rt = RT(row); rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(0.5f, 1); rt.sizeDelta = new Vector2(0, 44);
            nameL = Label(row, "Name", "Lenhador", 15, TextAlignmentOptions.Left); var nr = nameL.rectTransform; nr.anchorMin = new Vector2(0, 1); nr.anchorMax = new Vector2(0, 1); nr.pivot = new Vector2(0, 1); nr.anchoredPosition = new Vector2(6, -2); nr.sizeDelta = new Vector2(160, 20);
            levelL = Label(row, "Level", "Nv 1", 15, TextAlignmentOptions.Right); var lr = levelL.rectTransform; lr.anchorMin = new Vector2(1, 1); lr.anchorMax = new Vector2(1, 1); lr.pivot = new Vector2(1, 1); lr.anchoredPosition = new Vector2(-6, -2); lr.sizeDelta = new Vector2(80, 20);
            var bar = Img(row.transform, "BarBG", Vector2.zero); var brt = RT(bar); brt.anchorMin = new Vector2(0, 0); brt.anchorMax = new Vector2(1, 0); brt.pivot = new Vector2(0.5f, 0); brt.anchoredPosition = new Vector2(0, 4); brt.sizeDelta = new Vector2(-12, 14); bar.GetComponent<Image>().color = new Color(0, 0, 0, 0.4f);
            var fillGo = Img(bar.transform, "Fill", Vector2.zero); Stretch(RT(fillGo)); fill = fillGo.GetComponent<Image>(); fill.color = Accent; fill.type = Image.Type.Filled; fill.fillMethod = Image.FillMethod.Horizontal; fill.fillAmount = 0f;
            xpL = Label(bar, "Xp", "0/0", 11, TextAlignmentOptions.Center); Stretch(xpL.rectTransform);
            return row;
        }

        // ── primitives ──
        private static GameObject Row(Transform parent, string name)
        {
            var row = New(name, parent); RT(row).sizeDelta = new Vector2(0, 40);
            row.AddComponent<Image>().color = BgSoft;
            var le = row.AddComponent<LayoutElement>(); le.preferredHeight = 40; le.minHeight = 40;
            var hlg = row.AddComponent<HorizontalLayoutGroup>(); hlg.childAlignment = TextAnchor.MiddleLeft; hlg.spacing = 6; hlg.padding = new RectOffset(6, 6, 4, 4); hlg.childControlWidth = true; hlg.childControlHeight = true; hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = true; // expand children to the row height (else fixed-width children like the button get height 0)
            return row;
        }

        private static GameObject Img(Transform parent, string name, Vector2 size)
        {
            var go = New(name, parent); RT(go).sizeDelta = size; go.AddComponent<Image>().color = Color.white;
            return go;
        }

        private static TMP_Text Label(GameObject parent, string name, string text, int size, TextAlignmentOptions align, Color? color = null)
        {
            var go = New(name, parent.transform);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = text; t.fontSize = size; t.alignment = align; t.color = color ?? Color.white; t.overflowMode = TextOverflowModes.Ellipsis;
            return t;
        }

        private static TMP_Text Label(RectTransform parent, string name, string text, int size, TextAlignmentOptions align, Color? color = null)
            => Label(parent.gameObject, name, text, size, align, color);

        private static GameObject Button(Transform parent, string name, string label, Vector2 size)
        {
            var go = New(name, parent); RT(go).sizeDelta = size;
            go.AddComponent<Image>().color = new Color(0.2f, 0.45f, 0.8f, 1f);
            go.AddComponent<Button>();
            var t = Label(go, name + "Text", label, 14, TextAlignmentOptions.Center); Stretch(t.rectTransform);
            return go;
        }

        // ── utils ──
        private static GameObject New(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false); return go;
        }
        private static RectTransform RT(GameObject go) => (RectTransform)go.transform;
        private static void Stretch(RectTransform rt) { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero; }
        private static void Top(RectTransform rt, float h) { rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(0.5f, 1); rt.anchoredPosition = new Vector2(0, -4); rt.sizeDelta = new Vector2(0, h); }
        private static void Bottom(RectTransform rt, float margin) { rt.anchorMin = new Vector2(0.5f, 0); rt.anchorMax = new Vector2(0.5f, 0); rt.pivot = new Vector2(0.5f, 0); rt.anchoredPosition = new Vector2(0, margin); }
        private static void Band(RectTransform rt, float topInset, float h) { rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(0.5f, 1); rt.anchoredPosition = new Vector2(8, -topInset); rt.sizeDelta = new Vector2(-16, h); }
        private static void Fixed(GameObject go, float w) { var le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>(); le.preferredWidth = w; le.minWidth = w; le.flexibleWidth = 0; }
        private static void Fixed(TMP_Text t, float w) => Fixed(t.gameObject, w);
        private static void Flexible(GameObject go) { var le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>(); le.flexibleWidth = 1; }

        private static void Set(SerializedObject so, string field, Object value)
        {
            var p = so.FindProperty(field);
            if (p != null) p.objectReferenceValue = value;
            else Debug.LogWarning($"[UIBuilder] campo '{field}' não encontrado em {so.targetObject.GetType().Name}");
        }

        private static ContentLibrary FindLibrary()
        {
            var guids = AssetDatabase.FindAssets("t:ContentLibrary");
            return guids.Length > 0 ? AssetDatabase.LoadAssetAtPath<ContentLibrary>(AssetDatabase.GUIDToAssetPath(guids[0])) : null;
        }

        private static void AddToggle(GameObject holder, GameObject window, Key hotkey)
        {
            var toggle = holder.AddComponent<UIPanelToggle>();
            var so = new SerializedObject(toggle);
            Set(so, "panel", window);
            var hk = so.FindProperty("hotkey"); if (hk != null) hk.enumValueIndex = (int)hotkey;
            var sh = so.FindProperty("startHidden"); if (sh != null) sh.boolValue = true;
            so.ApplyModifiedProperties();
        }

        private static void AddClick(GameObject buttonGo, Object target, string method)
        {
            var btn = buttonGo.GetComponent<Button>();
            if (btn == null) return;
            var call = (UnityEngine.Events.UnityAction)System.Delegate.CreateDelegate(typeof(UnityEngine.Events.UnityAction), target, method);
            UnityEditor.Events.UnityEventTools.AddVoidPersistentListener(btn.onClick, call);
        }

        private static T EnsureController<T>(string name) where T : Component
        {
            var existing = Object.FindAnyObjectByType<T>();
            if (existing != null) return existing;
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Create " + name);
            return go.AddComponent<T>();
        }

        private static Canvas GetOrCreateCanvas()
        {
            var canvas = Object.FindAnyObjectByType<Canvas>();
            if (canvas != null) return canvas;
            var go = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080);
            if (Object.FindAnyObjectByType<EventSystem>() == null) new GameObject("EventSystem", typeof(EventSystem));
            Undo.RegisterCreatedObjectUndo(go, "Create Canvas");
            return canvas;
        }

        private static void Done(GameObject created, string what)
        {
            Selection.activeGameObject = created;
            Debug.Log($"[UIBuilder] {what} — criado + ligado. Reestilize à vontade.");
        }
    }
}
