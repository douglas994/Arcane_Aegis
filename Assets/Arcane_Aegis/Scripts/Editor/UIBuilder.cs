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
            close.GetComponent<RectTransform>().anchoredPosition = new Vector2(66, 12);   // right of center
            var repair = Button(body, "RepairButton", "Consertar", new Vector2(120, 34)); Bottom(repair.GetComponent<RectTransform>(), 12);
            repair.GetComponent<RectTransform>().anchoredPosition = new Vector2(-66, 12);  // left of center

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
            AddClick(repair, shop, nameof(ShopPanel.RepairAll));
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
            var window = WindowVisual(holder.transform, new Vector2(380, 320), out var body);
            var panel = window.AddComponent<ProfessionPanel>();
            var title = Label(body, "Title", "Profissões", 20, TextAlignmentOptions.Center); Top(title.rectTransform, 32);

            var list = ScrollList(body, "ProfessionScroll", 44, 250);
            var rowTemplate = ProfessionRowTemplate(holder.transform);

            var so = new SerializedObject(panel);
            Set(so, "rowPrefab", rowTemplate);
            Set(so, "container", list);
            so.ApplyModifiedProperties();

            AddToggle(holder, window, Key.P);
            Done(holder, "Profession Panel (tecla P abre) — auto-lista as 7 profissões");
        }

        [MenuItem("ArcaneMMO/UI/Currency HUD")]
        public static void BuildCurrencyHud()
        {
            var canvas = GetOrCreateCanvas();
            var root = New("CurrencyHud", canvas.transform);
            var rt = RT(root); rt.anchorMin = rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(1, 1); rt.anchoredPosition = new Vector2(-20, -20); rt.sizeDelta = new Vector2(220, 40);
            var hud = root.AddComponent<CurrencyHud>();

            // Vertical container the currency rows stack in (grows with content). The row template is a SIBLING of this
            // (not a child) — CurrencyHud.Build() clears the container's children, which would otherwise destroy it.
            var rowsGo = New("Rows", root.transform); var rrt = RT(rowsGo);
            rrt.anchorMin = new Vector2(0, 1); rrt.anchorMax = new Vector2(1, 1); rrt.pivot = new Vector2(0.5f, 1); rrt.anchoredPosition = Vector2.zero; rrt.sizeDelta = Vector2.zero;
            var vlg = rowsGo.AddComponent<VerticalLayoutGroup>(); vlg.childControlWidth = true; vlg.childForceExpandWidth = true; vlg.childControlHeight = true; vlg.childForceExpandHeight = false; vlg.spacing = 2;
            var csf = rowsGo.AddComponent<ContentSizeFitter>(); csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var rowTemplate = CurrencyRowTemplate(root.transform);

            var so = new SerializedObject(hud);
            Set(so, "library", FindLibrary());
            Set(so, "rowPrefab", rowTemplate);
            Set(so, "container", rrt);
            so.ApplyModifiedProperties();
            Done(root, "Currency HUD — auto-lista as moedas do ContentLibrary");
        }

        [MenuItem("ArcaneMMO/UI/Party Panel")]
        public static void BuildParty()
        {
            var canvas = GetOrCreateCanvas();
            var holder = Holder(canvas.transform, "PartyPanel");
            var window = WindowVisual(holder.transform, new Vector2(360, 420), out var body);
            var panel = window.AddComponent<PartyPanel>();
            var title = Label(body, "Title", "Grupo", 20, TextAlignmentOptions.Center); Top(title.rectTransform, 32);

            var list = ScrollList(body, "RosterScroll", 44, 244);
            var rosterRoot = list.parent.parent.gameObject; // the ScrollRect view → hidden when you're solo

            var inviteField = InputField(body, "InviteName", "Nome do jogador…");
            var ifr = RT(inviteField.gameObject); ifr.anchorMin = new Vector2(0, 0); ifr.anchorMax = new Vector2(1, 0); ifr.pivot = new Vector2(0.5f, 0); ifr.anchoredPosition = new Vector2(-60, 92); ifr.sizeDelta = new Vector2(-128, 30);
            var inviteBtn = Button(body, "InviteButton", "Convidar", new Vector2(112, 30)); var ibr = RT(inviteBtn); ibr.anchorMin = new Vector2(1, 0); ibr.anchorMax = new Vector2(1, 0); ibr.pivot = new Vector2(1, 0); ibr.anchoredPosition = new Vector2(0, 92);

            var leaveBtn = Button(body, "LeaveButton", "Sair", new Vector2(112, 32)); var lbr = RT(leaveBtn); lbr.anchorMin = lbr.anchorMax = new Vector2(0, 0); lbr.pivot = new Vector2(0, 0); lbr.anchoredPosition = new Vector2(0, 12);
            var disbandBtn = Button(body, "DisbandButton", "Desfazer", new Vector2(112, 32)); var dbr = RT(disbandBtn); dbr.anchorMin = dbr.anchorMax = new Vector2(1, 0); dbr.pivot = new Vector2(1, 0); dbr.anchoredPosition = new Vector2(0, 12);

            var rowTemplate = PartyMemberRowTemplate(holder.transform);

            var so = new SerializedObject(panel);
            Set(so, "rosterRoot", rosterRoot);
            Set(so, "rowPrefab", rowTemplate);
            Set(so, "container", list);
            Set(so, "leaveButton", leaveBtn.GetComponent<Button>());
            Set(so, "disbandButton", disbandBtn.GetComponent<Button>());
            Set(so, "inviteName", inviteField);
            Set(so, "inviteButton", inviteBtn.GetComponent<Button>());
            so.ApplyModifiedProperties();

            AddToggle(holder, window, Key.O);
            Done(holder, "Party Panel (tecla O abre) — convidar por nome, sair/expulsar/desfazer");
        }

        [MenuItem("ArcaneMMO/UI/Party Invite Prompt")]
        public static void BuildPartyInvite()
        {
            var canvas = GetOrCreateCanvas();
            var holder = Holder(canvas.transform, "PartyInvitePrompt");
            var prompt = holder.AddComponent<PartyInvitePrompt>();

            var window = Panel(holder.transform, "Window", new Vector2(340, 150), Bg);
            var rt = RT(window); rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); rt.pivot = new Vector2(0.5f, 0.5f); rt.anchoredPosition = new Vector2(0, 160);

            var msg = Label(window, "Message", "Fulano convidou você para um grupo.", 15, TextAlignmentOptions.Center);
            var mr = msg.rectTransform; mr.anchorMin = new Vector2(0, 1); mr.anchorMax = new Vector2(1, 1); mr.pivot = new Vector2(0.5f, 1); mr.anchoredPosition = new Vector2(0, -16); mr.sizeDelta = new Vector2(-20, 60);

            var accept = Button(window.transform, "Accept", "Aceitar", new Vector2(120, 34)); var ar = RT(accept); ar.anchorMin = ar.anchorMax = new Vector2(0, 0); ar.pivot = new Vector2(0, 0); ar.anchoredPosition = new Vector2(16, 16);
            var decline = Button(window.transform, "Decline", "Recusar", new Vector2(120, 34)); var dr = RT(decline); dr.anchorMin = dr.anchorMax = new Vector2(1, 0); dr.pivot = new Vector2(1, 0); dr.anchoredPosition = new Vector2(-16, 16);

            var so = new SerializedObject(prompt);
            Set(so, "panel", window);
            Set(so, "message", msg);
            Set(so, "acceptButton", accept.GetComponent<Button>());
            Set(so, "declineButton", decline.GetComponent<Button>());
            so.ApplyModifiedProperties();
            Done(holder, "Party Invite Prompt (aparece ao receber um convite)");
        }

        [MenuItem("ArcaneMMO/UI/Chat Panel")]
        public static void BuildChat()
        {
            var canvas = GetOrCreateCanvas();
            var holder = Holder(canvas.transform, "ChatPanel");
            var chat = holder.AddComponent<ChatPanel>();

            var win = Panel(holder.transform, "Window", new Vector2(440, 220), Bg);
            var wrt = RT(win); wrt.anchorMin = wrt.anchorMax = new Vector2(0, 0); wrt.pivot = new Vector2(0, 0); wrt.anchoredPosition = new Vector2(16, 16);
            var body = New("Body", win.transform); var brt = RT(body); Stretch(brt); brt.offsetMin = new Vector2(8, 8); brt.offsetMax = new Vector2(-8, -8);

            // Scrollable log: a single growing TMP_Text (vertical ContentSizeFitter) inside a masked viewport.
            var sv = New("LogScroll", body.transform); var svrt = RT(sv); svrt.anchorMin = new Vector2(0, 0); svrt.anchorMax = new Vector2(1, 1); svrt.offsetMin = new Vector2(0, 38); svrt.offsetMax = new Vector2(0, 0);
            sv.AddComponent<Image>().color = BgSoft;
            var sr = sv.AddComponent<ScrollRect>(); sr.horizontal = false;
            var viewport = New("Viewport", sv.transform); var vrt = RT(viewport); Stretch(vrt); viewport.AddComponent<RectMask2D>();
            var content = New("Content", viewport.transform); var crt = RT(content); crt.anchorMin = new Vector2(0, 1); crt.anchorMax = new Vector2(1, 1); crt.pivot = new Vector2(0.5f, 1); crt.anchoredPosition = Vector2.zero; crt.sizeDelta = Vector2.zero;
            var logText = content.AddComponent<TextMeshProUGUI>(); logText.fontSize = 13; logText.alignment = TextAlignmentOptions.BottomLeft; logText.color = Color.white; logText.richText = true;
            var csf = content.AddComponent<ContentSizeFitter>(); csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            sr.viewport = vrt; sr.content = crt;

            var input = InputField(brt, "ChatInput", "Mensagem do grupo…");
            var ir = RT(input.gameObject); ir.anchorMin = new Vector2(0, 0); ir.anchorMax = new Vector2(1, 0); ir.pivot = new Vector2(0, 0); ir.anchoredPosition = new Vector2(0, 0); ir.sizeDelta = new Vector2(-84, 30);
            var send = Button(body.transform, "Send", "Enviar", new Vector2(76, 30)); var sndr = RT(send); sndr.anchorMin = new Vector2(1, 0); sndr.anchorMax = new Vector2(1, 0); sndr.pivot = new Vector2(1, 0); sndr.anchoredPosition = new Vector2(0, 0);

            var so = new SerializedObject(chat);
            Set(so, "logText", logText);
            Set(so, "scroll", sr);
            Set(so, "input", input);
            Set(so, "sendButton", send.GetComponent<Button>());
            so.ApplyModifiedProperties();
            Done(holder, "Chat Panel (canto inf. esq.) — Enter ou Enviar manda no grupo");
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

        // Profession row prefab (name + level on top, XP bar below) carrying a ProfessionRow component. The panel
        // instantiates one per profession; this is hidden (it's the template).
        private static GameObject ProfessionRowTemplate(Transform parent)
        {
            var row = New("ProfessionRowTemplate", parent); RT(row).sizeDelta = new Vector2(0, 44);
            row.AddComponent<Image>().color = BgSoft;
            var le = row.AddComponent<LayoutElement>(); le.preferredHeight = 44; le.minHeight = 44;

            var nameL = Label(row, "Name", "Profissão", 15, TextAlignmentOptions.Left); var nr = nameL.rectTransform; nr.anchorMin = new Vector2(0, 1); nr.anchorMax = new Vector2(0, 1); nr.pivot = new Vector2(0, 1); nr.anchoredPosition = new Vector2(6, -2); nr.sizeDelta = new Vector2(160, 20);
            var levelL = Label(row, "Level", "Nv 1", 15, TextAlignmentOptions.Right); var lr = levelL.rectTransform; lr.anchorMin = new Vector2(1, 1); lr.anchorMax = new Vector2(1, 1); lr.pivot = new Vector2(1, 1); lr.anchoredPosition = new Vector2(-6, -2); lr.sizeDelta = new Vector2(80, 20);
            var bar = Img(row.transform, "BarBG", Vector2.zero); var brt = RT(bar); brt.anchorMin = new Vector2(0, 0); brt.anchorMax = new Vector2(1, 0); brt.pivot = new Vector2(0.5f, 0); brt.anchoredPosition = new Vector2(0, 4); brt.sizeDelta = new Vector2(-12, 14); bar.GetComponent<Image>().color = new Color(0, 0, 0, 0.4f);
            var fillGo = Img(bar.transform, "Fill", Vector2.zero); Stretch(RT(fillGo)); var fill = fillGo.GetComponent<Image>(); fill.color = Accent; fill.type = Image.Type.Filled; fill.fillMethod = Image.FillMethod.Horizontal; fill.fillAmount = 0f;
            var xpL = Label(bar, "Xp", "0/0", 11, TextAlignmentOptions.Center); Stretch(xpL.rectTransform);

            var pr = row.AddComponent<ProfessionRow>();
            var so = new SerializedObject(pr);
            Set(so, "nameLabel", nameL); Set(so, "levelLabel", levelL); Set(so, "fill", fill); Set(so, "xpLabel", xpL);
            so.ApplyModifiedProperties();
            row.SetActive(false);
            return row;
        }

        // Currency row prefab (icon + name + amount) carrying a CurrencyRow component.
        private static GameObject CurrencyRowTemplate(Transform parent)
        {
            var row = Row(parent, "CurrencyRowTemplate");
            var icon = Img(row.transform, "Icon", new Vector2(22, 22)); Fixed(icon, 22);
            var name = Label(row, "Name", "Moeda", 14, TextAlignmentOptions.Left); Flexible(name.gameObject);
            var amount = Label(row, "Amount", "0", 14, TextAlignmentOptions.Right); Fixed(amount.gameObject, 90);

            var cr = row.AddComponent<CurrencyRow>();
            var so = new SerializedObject(cr);
            Set(so, "nameLabel", name); Set(so, "amountLabel", amount); Set(so, "icon", icon.GetComponent<Image>());
            so.ApplyModifiedProperties();
            row.SetActive(false);
            return row;
        }

        // Party member row prefab (leader marker + name + level + HP bar + kick) carrying a PartyMemberRow component.
        private static GameObject PartyMemberRowTemplate(Transform parent)
        {
            var row = Row(parent, "PartyMemberRowTemplate");
            var leader = Img(row.transform, "LeaderMarker", new Vector2(14, 14)); Fixed(leader, 14); leader.GetComponent<Image>().color = Accent;
            var name = Label(row, "Name", "Membro", 14, TextAlignmentOptions.Left); Flexible(name.gameObject);
            var level = Label(row, "Level", "Nv 1", 13, TextAlignmentOptions.Right); Fixed(level.gameObject, 52);
            var barBg = Img(row.transform, "HpBarBG", new Vector2(80, 14)); Fixed(barBg, 80); barBg.GetComponent<Image>().color = new Color(0, 0, 0, 0.4f);
            var fillGo = Img(barBg.transform, "Fill", Vector2.zero); Stretch(RT(fillGo)); var fill = fillGo.GetComponent<Image>(); fill.color = new Color(0.4f, 0.85f, 0.4f, 1f); fill.type = Image.Type.Filled; fill.fillMethod = Image.FillMethod.Horizontal; fill.fillAmount = 1f;
            var kick = Button(row.transform, "Kick", "X", new Vector2(26, 26)); Fixed(kick, 26);

            var pr = row.AddComponent<PartyMemberRow>();
            var so = new SerializedObject(pr);
            Set(so, "nameLabel", name); Set(so, "levelLabel", level); Set(so, "hpFill", fill);
            Set(so, "leaderMarker", leader); Set(so, "kickButton", kick.GetComponent<Button>());
            so.ApplyModifiedProperties();
            row.SetActive(false);
            return row;
        }

        // A single-line TMP input field (background + masked text area + placeholder), wired and ready.
        private static TMP_InputField InputField(RectTransform parent, string name, string placeholder)
        {
            var go = New(name, parent); go.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.12f);
            var input = go.AddComponent<TMP_InputField>();

            var area = New("TextArea", go.transform); var art = RT(area); Stretch(art); art.offsetMin = new Vector2(8, 4); art.offsetMax = new Vector2(-8, -4);
            area.AddComponent<RectMask2D>();

            var ph = Label(area, "Placeholder", placeholder, 14, TextAlignmentOptions.Left, new Color(1f, 1f, 1f, 0.4f)); Stretch(ph.rectTransform);
            var txt = Label(area, "Text", "", 14, TextAlignmentOptions.Left); Stretch(txt.rectTransform);

            input.textViewport = art;
            input.textComponent = txt;
            input.placeholder = ph;
            input.lineType = TMP_InputField.LineType.SingleLine;
            return input;
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
