using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using TMPro;
using Arcane_Aegis.UI;
using Arcane_Aegis.Content;
using Arcane_Aegis.Controllers;
using Arcane_Aegis.Combat;

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

        [MenuItem("ArcaneMMO/UI/Friends Panel")]
        public static void BuildFriends()
        {
            var canvas = GetOrCreateCanvas();
            var holder = Holder(canvas.transform, "FriendPanel");
            var window = WindowVisual(holder.transform, new Vector2(340, 420), out var body);
            var panel = window.AddComponent<FriendPanel>();
            var title = Label(body, "Title", "Amigos", 20, TextAlignmentOptions.Center); Top(title.rectTransform, 32);

            var list = ScrollList(body, "FriendScroll", 44, 300);

            var addField = InputField(body, "AddName", "Nome do jogador…");
            var afr = RT(addField.gameObject); afr.anchorMin = new Vector2(0, 0); afr.anchorMax = new Vector2(1, 0); afr.pivot = new Vector2(0, 0); afr.anchoredPosition = new Vector2(0, 12); afr.sizeDelta = new Vector2(-120, 30);
            var addBtn = Button(body.transform, "AddButton", "Adicionar", new Vector2(112, 30)); var abr = RT(addBtn); abr.anchorMin = new Vector2(1, 0); abr.anchorMax = new Vector2(1, 0); abr.pivot = new Vector2(1, 0); abr.anchoredPosition = new Vector2(0, 12);

            var rowTemplate = FriendRowTemplate(holder.transform);

            var so = new SerializedObject(panel);
            Set(so, "rowPrefab", rowTemplate);
            Set(so, "container", list);
            Set(so, "addName", addField);
            Set(so, "addButton", addBtn.GetComponent<Button>());
            so.ApplyModifiedProperties();

            AddToggle(holder, window, Key.L);
            Done(holder, "Friends Panel (tecla L abre) — adicionar por nome, remover");
        }

        [MenuItem("ArcaneMMO/UI/Friend Request Prompt")]
        public static void BuildFriendRequest()
        {
            var canvas = GetOrCreateCanvas();
            var holder = Holder(canvas.transform, "FriendRequestPrompt");
            var prompt = holder.AddComponent<FriendRequestPrompt>();

            var window = Panel(holder.transform, "Window", new Vector2(340, 150), Bg);
            var rt = RT(window); rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); rt.pivot = new Vector2(0.5f, 0.5f); rt.anchoredPosition = new Vector2(0, 60);

            var msg = Label(window, "Message", "Fulano quer te adicionar como amigo.", 15, TextAlignmentOptions.Center);
            var mr = msg.rectTransform; mr.anchorMin = new Vector2(0, 1); mr.anchorMax = new Vector2(1, 1); mr.pivot = new Vector2(0.5f, 1); mr.anchoredPosition = new Vector2(0, -16); mr.sizeDelta = new Vector2(-20, 60);

            var accept = Button(window.transform, "Accept", "Aceitar", new Vector2(120, 34)); var ar = RT(accept); ar.anchorMin = ar.anchorMax = new Vector2(0, 0); ar.pivot = new Vector2(0, 0); ar.anchoredPosition = new Vector2(16, 16);
            var decline = Button(window.transform, "Decline", "Recusar", new Vector2(120, 34)); var dr = RT(decline); dr.anchorMin = dr.anchorMax = new Vector2(1, 0); dr.pivot = new Vector2(1, 0); dr.anchoredPosition = new Vector2(-16, 16);

            var so = new SerializedObject(prompt);
            Set(so, "panel", window);
            Set(so, "message", msg);
            Set(so, "acceptButton", accept.GetComponent<Button>());
            Set(so, "declineButton", decline.GetComponent<Button>());
            so.ApplyModifiedProperties();
            Done(holder, "Friend Request Prompt (aparece ao receber um pedido)");
        }

        [MenuItem("ArcaneMMO/UI/Guild Panel")]
        public static void BuildGuild()
        {
            var canvas = GetOrCreateCanvas();
            var holder = Holder(canvas.transform, "GuildPanel");
            var window = WindowVisual(holder.transform, new Vector2(380, 460), out var body);
            var panel = window.AddComponent<GuildPanel>();
            var title = Label(body, "Title", "Guilda", 20, TextAlignmentOptions.Center); Top(title.rectTransform, 32);

            // ── Create mode (no guild) ──
            var createRoot = New("CreateRoot", body); var crrt = RT(createRoot); Stretch(crrt); crrt.offsetMin = new Vector2(0, 0); crrt.offsetMax = new Vector2(0, -40);
            var createName = InputField(crrt, "CreateName", "Nome da guilda…");
            var cnr = RT(createName.gameObject); cnr.anchorMin = cnr.anchorMax = new Vector2(0.5f, 0.5f); cnr.pivot = new Vector2(0.5f, 0.5f); cnr.anchoredPosition = new Vector2(0, 20); cnr.sizeDelta = new Vector2(240, 32);
            var createBtn = Button(createRoot.transform, "CreateButton", "Criar Guilda", new Vector2(160, 34)); var cbr = RT(createBtn); cbr.anchorMin = cbr.anchorMax = new Vector2(0.5f, 0.5f); cbr.pivot = new Vector2(0.5f, 0.5f); cbr.anchoredPosition = new Vector2(0, -24);

            // ── Guild mode (in a guild) ──
            var guildRoot = New("GuildRoot", body); var grrt = RT(guildRoot); Stretch(grrt); grrt.offsetMin = new Vector2(0, 0); grrt.offsetMax = new Vector2(0, -40);
            var nameLabel = Label(guildRoot, "GuildName", "Guilda", 17, TextAlignmentOptions.Center); var nlr = nameLabel.rectTransform; nlr.anchorMin = new Vector2(0, 1); nlr.anchorMax = new Vector2(1, 1); nlr.pivot = new Vector2(0.5f, 1); nlr.anchoredPosition = new Vector2(0, 0); nlr.sizeDelta = new Vector2(0, 26);
            var list = ScrollList(grrt, "GuildScroll", 32, 232);
            var inviteName = InputField(grrt, "InviteName", "Convidar por nome…");
            var inr = RT(inviteName.gameObject); inr.anchorMin = new Vector2(0, 0); inr.anchorMax = new Vector2(1, 0); inr.pivot = new Vector2(0, 0); inr.anchoredPosition = new Vector2(0, 50); inr.sizeDelta = new Vector2(-120, 30);
            var inviteBtn = Button(guildRoot.transform, "InviteButton", "Convidar", new Vector2(112, 30)); var ibr = RT(inviteBtn); ibr.anchorMin = new Vector2(1, 0); ibr.anchorMax = new Vector2(1, 0); ibr.pivot = new Vector2(1, 0); ibr.anchoredPosition = new Vector2(0, 50);
            var leaveBtn = Button(guildRoot.transform, "LeaveButton", "Sair", new Vector2(112, 32)); var lbr = RT(leaveBtn); lbr.anchorMin = lbr.anchorMax = new Vector2(0, 0); lbr.pivot = new Vector2(0, 0); lbr.anchoredPosition = new Vector2(0, 12);
            var disbandBtn = Button(guildRoot.transform, "DisbandButton", "Desfazer", new Vector2(112, 32)); var dbr = RT(disbandBtn); dbr.anchorMin = dbr.anchorMax = new Vector2(1, 0); dbr.pivot = new Vector2(1, 0); dbr.anchoredPosition = new Vector2(0, 12);

            var rowTemplate = GuildRowTemplate(holder.transform);

            var so = new SerializedObject(panel);
            Set(so, "createRoot", createRoot);
            Set(so, "createName", createName);
            Set(so, "createButton", createBtn.GetComponent<Button>());
            Set(so, "guildRoot", guildRoot);
            Set(so, "guildNameLabel", nameLabel);
            Set(so, "rowPrefab", rowTemplate);
            Set(so, "container", list);
            Set(so, "inviteName", inviteName);
            Set(so, "inviteButton", inviteBtn.GetComponent<Button>());
            Set(so, "leaveButton", leaveBtn.GetComponent<Button>());
            Set(so, "disbandButton", disbandBtn.GetComponent<Button>());
            so.ApplyModifiedProperties();

            AddToggle(holder, window, Key.G);
            Done(holder, "Guild Panel (tecla G abre) — criar/convidar/ranks/sair/desfazer");
        }

        [MenuItem("ArcaneMMO/UI/Guild Invite Prompt")]
        public static void BuildGuildInvite()
        {
            var canvas = GetOrCreateCanvas();
            var holder = Holder(canvas.transform, "GuildInvitePrompt");
            var prompt = holder.AddComponent<GuildInvitePrompt>();

            var window = Panel(holder.transform, "Window", new Vector2(360, 150), Bg);
            var rt = RT(window); rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); rt.pivot = new Vector2(0.5f, 0.5f); rt.anchoredPosition = new Vector2(0, -40);

            var msg = Label(window, "Message", "Fulano convidou você para a guilda X.", 15, TextAlignmentOptions.Center);
            var mr = msg.rectTransform; mr.anchorMin = new Vector2(0, 1); mr.anchorMax = new Vector2(1, 1); mr.pivot = new Vector2(0.5f, 1); mr.anchoredPosition = new Vector2(0, -16); mr.sizeDelta = new Vector2(-20, 60);

            var accept = Button(window.transform, "Accept", "Aceitar", new Vector2(120, 34)); var ar = RT(accept); ar.anchorMin = ar.anchorMax = new Vector2(0, 0); ar.pivot = new Vector2(0, 0); ar.anchoredPosition = new Vector2(16, 16);
            var decline = Button(window.transform, "Decline", "Recusar", new Vector2(120, 34)); var dr = RT(decline); dr.anchorMin = dr.anchorMax = new Vector2(1, 0); dr.pivot = new Vector2(1, 0); dr.anchoredPosition = new Vector2(-16, 16);

            var so = new SerializedObject(prompt);
            Set(so, "panel", window);
            Set(so, "message", msg);
            Set(so, "acceptButton", accept.GetComponent<Button>());
            Set(so, "declineButton", decline.GetComponent<Button>());
            so.ApplyModifiedProperties();
            Done(holder, "Guild Invite Prompt (aparece ao receber um convite)");
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

            // Bottom row: [channel] [input.......] [send]
            var channel = Button(body.transform, "Channel", "Global", new Vector2(76, 30)); var chr = RT(channel); chr.anchorMin = new Vector2(0, 0); chr.anchorMax = new Vector2(0, 0); chr.pivot = new Vector2(0, 0); chr.anchoredPosition = new Vector2(0, 0);
            var channelLabel = channel.GetComponentInChildren<TMP_Text>();

            var input = InputField(brt, "ChatInput", "Mensagem…  (/g /z /p, /w Nome)");
            var ir = RT(input.gameObject); ir.anchorMin = new Vector2(0, 0); ir.anchorMax = new Vector2(1, 0); ir.pivot = new Vector2(0, 0); ir.anchoredPosition = new Vector2(80, 0); ir.sizeDelta = new Vector2(-164, 30);
            var send = Button(body.transform, "Send", "Enviar", new Vector2(76, 30)); var sndr = RT(send); sndr.anchorMin = new Vector2(1, 0); sndr.anchorMax = new Vector2(1, 0); sndr.pivot = new Vector2(1, 0); sndr.anchoredPosition = new Vector2(0, 0);

            var so = new SerializedObject(chat);
            Set(so, "logText", logText);
            Set(so, "scroll", sr);
            Set(so, "input", input);
            Set(so, "sendButton", send.GetComponent<Button>());
            Set(so, "channelButton", channel.GetComponent<Button>());
            Set(so, "channelLabel", channelLabel);
            so.ApplyModifiedProperties();
            Done(holder, "Chat Panel (canto inf. esq.) — botão troca canal; /g /z /p /w funcionam");
        }

        [MenuItem("ArcaneMMO/UI/Target Frame")]
        public static void BuildTargetFrame()
        {
            var canvas = GetOrCreateCanvas();
            var holder = Holder(canvas.transform, "TargetFrame");
            var frame = holder.AddComponent<TargetFrame>();

            // Toggled visual: a small panel at top-center (name + HP bar). TargetFrame.Update shows/hides it.
            var window = Panel(holder.transform, "Window", new Vector2(240, 60), Bg);
            var rt = RT(window); rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f); rt.pivot = new Vector2(0.5f, 1f); rt.anchoredPosition = new Vector2(0, -16);

            var name = Label(window, "Name", "Alvo", 16, TextAlignmentOptions.Center);
            var nr = name.rectTransform; nr.anchorMin = new Vector2(0, 1); nr.anchorMax = new Vector2(1, 1); nr.pivot = new Vector2(0.5f, 1); nr.anchoredPosition = new Vector2(0, -6); nr.sizeDelta = new Vector2(-12, 24);

            var barBg = Img(window.transform, "HpBarBG", Vector2.zero); var brt = RT(barBg); brt.anchorMin = new Vector2(0, 0); brt.anchorMax = new Vector2(1, 0); brt.pivot = new Vector2(0.5f, 0); brt.anchoredPosition = new Vector2(0, 8); brt.sizeDelta = new Vector2(-16, 16); barBg.GetComponent<Image>().color = new Color(0, 0, 0, 0.5f);
            var fillGo = Img(barBg.transform, "Fill", Vector2.zero); Stretch(RT(fillGo)); var fill = fillGo.GetComponent<Image>(); fill.color = new Color(0.85f, 0.3f, 0.3f, 1f); fill.type = Image.Type.Filled; fill.fillMethod = Image.FillMethod.Horizontal; fill.fillAmount = 1f;

            var targeting = EnsureController<TargetingSystem>("TargetingSystem"); // create + wire the targeting system

            var so = new SerializedObject(frame);
            Set(so, "root", window);
            Set(so, "fill", fill);
            Set(so, "nameLabel", name);
            Set(so, "targeting", targeting);
            so.ApplyModifiedProperties();
            Done(holder, "Target Frame (topo-centro) — nome + HP do alvo; cria/liga o TargetingSystem (Tab cicla, clique trava, Esc solta)");
        }

        [MenuItem("ArcaneMMO/UI/Admin Panel")]
        public static void BuildAdminPanel()
        {
            var canvas = GetOrCreateCanvas();
            var holder = Holder(canvas.transform, "AdminPanel");          // always active (the component runs Update)
            var admin = holder.AddComponent<AdminPanel>();
            var window = WindowVisual(holder.transform, new Vector2(440, 250), out var body); // the toggled visual (F8)

            var title = Label(body, "Title", "Admin / GM  (F8)", 18, TextAlignmentOptions.Center); Top(title.rectTransform, 30);
            var help = Label(body, "Help", "give <item> [n] · spawn <monstro> [n] · tp <x> <z> · setlevel <n> · heal · god · kill", 11, TextAlignmentOptions.Center, new Color(1f, 1f, 1f, 0.5f));
            var hr = help.rectTransform; hr.anchorMin = new Vector2(0, 1); hr.anchorMax = new Vector2(1, 1); hr.pivot = new Vector2(0.5f, 1); hr.anchoredPosition = new Vector2(0, -42); hr.sizeDelta = new Vector2(-8, 30);

            // command field + send
            var input = InputField(body, "CommandInput", "comando…  ex: give Ice_Sword 1");
            var ir = RT(input.gameObject); ir.anchorMin = new Vector2(0, 1); ir.anchorMax = new Vector2(1, 1); ir.pivot = new Vector2(0.5f, 1); ir.anchoredPosition = new Vector2(-60, -84); ir.sizeDelta = new Vector2(-128, 34);
            var send = Button(body.transform, "Send", "Enviar", new Vector2(110, 34)); var sr = RT(send); sr.anchorMin = new Vector2(1, 1); sr.anchorMax = new Vector2(1, 1); sr.pivot = new Vector2(1, 1); sr.anchoredPosition = new Vector2(0, -84);

            // quick buttons
            var heal = Button(body.transform, "Heal", "Heal", new Vector2(96, 34)); var hbr = RT(heal); hbr.anchorMin = hbr.anchorMax = new Vector2(0, 0); hbr.pivot = new Vector2(0, 0); hbr.anchoredPosition = new Vector2(0, 12);
            var god = Button(body.transform, "God", "God", new Vector2(96, 34)); var gbr = RT(god); gbr.anchorMin = gbr.anchorMax = new Vector2(0.5f, 0); gbr.pivot = new Vector2(0.5f, 0); gbr.anchoredPosition = new Vector2(0, 12);
            var kill = Button(body.transform, "Kill", "Kill", new Vector2(96, 34)); var kbr = RT(kill); kbr.anchorMin = kbr.anchorMax = new Vector2(1, 0); kbr.pivot = new Vector2(1, 0); kbr.anchoredPosition = new Vector2(0, 12);

            var so = new SerializedObject(admin);
            Set(so, "panelRoot", window);
            Set(so, "input", input);
            so.ApplyModifiedProperties();

            AddClick(send, admin, nameof(AdminPanel.Submit));
            AddClick(heal, admin, nameof(AdminPanel.QuickHeal));
            AddClick(god, admin, nameof(AdminPanel.QuickGod));
            AddClick(kill, admin, nameof(AdminPanel.QuickKill));
            Done(holder, "Admin Panel (F8, só pra conta admin) — campo de comando + Heal/God/Kill");
        }

        [MenuItem("ArcaneMMO/UI/Disconnect Screen")]
        public static void BuildDisconnectScreen()
        {
            var canvas = GetOrCreateCanvas();
            var holder = Holder(canvas.transform, "DisconnectScreen"); // always active (the component subscribes to NetClient)
            var comp = holder.AddComponent<DisconnectScreen>();

            // full-screen dim overlay (panelRoot) — the component hides it in Start, shows it on disconnect
            var overlay = Panel(holder.transform, "Overlay", Vector2.zero, new Color(0f, 0f, 0f, 0.85f)); Stretch(RT(overlay));

            var win = Panel(overlay.transform, "Window", new Vector2(460, 200), Bg);
            var wr = RT(win); wr.anchorMin = wr.anchorMax = new Vector2(0.5f, 0.5f); wr.pivot = new Vector2(0.5f, 0.5f); wr.anchoredPosition = Vector2.zero;

            var title = Label(win, "Title", "Desconectado", 22, TextAlignmentOptions.Center); Top(title.rectTransform, 40);
            var msg = Label(win, "Message", "Você foi desconectado do servidor.", 15, TextAlignmentOptions.Center, new Color(1f, 1f, 1f, 0.8f));
            var mr = msg.rectTransform; mr.anchorMin = new Vector2(0, 0.5f); mr.anchorMax = new Vector2(1, 0.5f); mr.pivot = new Vector2(0.5f, 0.5f); mr.anchoredPosition = new Vector2(0, 6); mr.sizeDelta = new Vector2(-24, 50);

            var back = Button(win.transform, "Back", "Voltar ao login", new Vector2(200, 40));
            var br = RT(back); br.anchorMin = br.anchorMax = new Vector2(0.5f, 0); br.pivot = new Vector2(0.5f, 0); br.anchoredPosition = new Vector2(0, 18);

            var so = new SerializedObject(comp);
            Set(so, "panelRoot", overlay);
            Set(so, "messageText", msg);
            so.ApplyModifiedProperties();

            AddClick(back, comp, nameof(DisconnectScreen.BackToLogin));
            Done(holder, "Disconnect Screen — overlay + mensagem + 'Voltar ao login' (some no Start, aparece ao cair a conexão)");
        }

        [MenuItem("ArcaneMMO/UI/Status Bar (buffs)")]
        public static void BuildStatusBar()
        {
            var canvas = GetOrCreateCanvas();
            var holder = Holder(canvas.transform, "StatusBar");
            var comp = holder.AddComponent<StatusBar>();

            var container = New("Container", holder.transform); var crt = RT(container);
            crt.anchorMin = new Vector2(0, 1); crt.anchorMax = new Vector2(0, 1); crt.pivot = new Vector2(0, 1);
            crt.anchoredPosition = new Vector2(16, -16); crt.sizeDelta = new Vector2(420, 44);
            var hlg = container.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 4; hlg.childAlignment = TextAnchor.UpperLeft;
            hlg.childControlWidth = false; hlg.childControlHeight = false; hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;

            // one slot template (icon + radial time overlay + stack count)
            var slot = Panel(container.transform, "Slot", new Vector2(40, 40), new Color(0f, 0f, 0f, 0.5f));
            var sc = slot.AddComponent<StatusSlot>();
            var iconGo = Img(slot.transform, "Icon", Vector2.zero); var irt = RT(iconGo); Stretch(irt); irt.offsetMin = new Vector2(2, 2); irt.offsetMax = new Vector2(-2, -2);
            var fillGo = Img(slot.transform, "Fill", Vector2.zero); Stretch(RT(fillGo));
            var fimg = fillGo.GetComponent<Image>(); fimg.color = new Color(0f, 0f, 0f, 0.55f); fimg.type = Image.Type.Filled; fimg.fillMethod = Image.FillMethod.Radial360; fimg.fillOrigin = (int)Image.Origin360.Top; fimg.fillClockwise = false; fimg.fillAmount = 1f;
            var stacks = Label(slot, "Stacks", "", 12, TextAlignmentOptions.BottomRight); Stretch(stacks.rectTransform);

            var sso = new SerializedObject(sc);
            Set(sso, "icon", iconGo.GetComponent<Image>());
            Set(sso, "fill", fimg);
            Set(sso, "stacks", stacks);
            sso.ApplyModifiedProperties();

            var so = new SerializedObject(comp);
            Set(so, "container", container.transform);
            Set(so, "slotTemplate", sc);
            so.ApplyModifiedProperties();
            Done(holder, "Status Bar — ícones de buff/debuff (radial = tempo; some quando vazio). Reestilize o Slot à vontade.");
        }

        [MenuItem("ArcaneMMO/UI/Collection Panel (pets+mounts)")]
        public static void BuildCollectionPanel()
        {
            var canvas = GetOrCreateCanvas();
            var holder = Holder(canvas.transform, "CollectionPanel");
            var comp = holder.AddComponent<CollectionPanel>();
            var window = WindowVisual(holder.transform, new Vector2(440, 360), out var body);
            AddToggle(holder, window, Key.K); // K toggles the collection

            var title = Label(body, "Title", "Coleção — Pets & Montarias (K)", 16, TextAlignmentOptions.Center); Top(title.rectTransform, 26);

            var list = New("List", body); var crt = RT(list);
            crt.anchorMin = new Vector2(0, 0); crt.anchorMax = new Vector2(1, 1); crt.offsetMin = new Vector2(6, 6); crt.offsetMax = new Vector2(-6, -34);
            var vlg = list.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 4; vlg.childControlHeight = false; vlg.childControlWidth = true; vlg.childForceExpandHeight = false; vlg.childForceExpandWidth = true; vlg.padding = new RectOffset(2, 2, 2, 2);

            // one row template (icon + name + active badge + Ativar)
            var row = Panel(list.transform, "Row", new Vector2(0, 40), BgSoft);
            var le = row.AddComponent<LayoutElement>(); le.preferredHeight = 40; le.minHeight = 40;
            var rc = row.AddComponent<CollectionRow>();
            var iconGo = Img(row.transform, "Icon", new Vector2(32, 32)); var irt = RT(iconGo); irt.anchorMin = irt.anchorMax = new Vector2(0, 0.5f); irt.pivot = new Vector2(0, 0.5f); irt.anchoredPosition = new Vector2(6, 0);
            var nameLbl = Label(row, "Name", "name", 13, TextAlignmentOptions.Left); var nrt = nameLbl.rectTransform; nrt.anchorMin = new Vector2(0, 0); nrt.anchorMax = new Vector2(1, 1); nrt.offsetMin = new Vector2(44, 0); nrt.offsetMax = new Vector2(-156, 0);
            var badge = Label(row, "Active", "ativo", 11, TextAlignmentOptions.Center, new Color(0.5f, 1f, 0.5f)); var brt = badge.rectTransform; brt.anchorMin = brt.anchorMax = new Vector2(1, 0.5f); brt.pivot = new Vector2(1, 0.5f); brt.anchoredPosition = new Vector2(-90, 0); brt.sizeDelta = new Vector2(48, 20);
            var setBtn = Button(row.transform, "SetActive", "Ativar", new Vector2(78, 28)); var sbrt = RT(setBtn); sbrt.anchorMin = sbrt.anchorMax = new Vector2(1, 0.5f); sbrt.pivot = new Vector2(1, 0.5f); sbrt.anchoredPosition = new Vector2(-6, 0);

            var rso = new SerializedObject(rc);
            Set(rso, "icon", iconGo.GetComponent<Image>());
            Set(rso, "label", nameLbl);
            Set(rso, "activeBadge", badge.gameObject);
            Set(rso, "setActiveBtn", setBtn.GetComponent<Button>());
            rso.ApplyModifiedProperties();

            var so = new SerializedObject(comp);
            Set(so, "container", list.transform);
            Set(so, "rowTemplate", rc);
            so.ApplyModifiedProperties();
            Done(holder, "Collection Panel (K) — pets/montarias possuídos + Ativar. Reestilize o Row à vontade.");
        }

        [MenuItem("ArcaneMMO/UI/Companion HUD (pet+mount)")]
        public static void BuildCompanionHud()
        {
            var canvas = GetOrCreateCanvas();
            var holder = Holder(canvas.transform, "CompanionHud");
            var comp = holder.AddComponent<CompanionHud>();

            var pet = Button(holder.transform, "PetBtn", "Pet", new Vector2(60, 48));
            var pr = RT(pet); pr.anchorMin = pr.anchorMax = new Vector2(1, 0); pr.pivot = new Vector2(1, 0); pr.anchoredPosition = new Vector2(-12, 210);
            var mount = Button(holder.transform, "MountBtn", "Mount", new Vector2(60, 48));
            var mr = RT(mount); mr.anchorMin = mr.anchorMax = new Vector2(1, 0); mr.pivot = new Vector2(1, 0); mr.anchoredPosition = new Vector2(-80, 210);

            AddClick(pet, comp, nameof(CompanionHud.TogglePet));
            AddClick(mount, comp, nameof(CompanionHud.ToggleMount));
            Done(holder, "Companion HUD — botões Pet + Mount (toggle). Reestilize/reposicione à vontade.");
        }

        [MenuItem("ArcaneMMO/UI/Cast Bar")]
        public static void BuildCastBar()
        {
            var canvas = GetOrCreateCanvas();
            var holder = Holder(canvas.transform, "CastBar");
            var comp = holder.AddComponent<CastBar>();

            var bar = Panel(holder.transform, "Bar", new Vector2(340, 30), Bg);
            var brt = RT(bar); brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 0f); brt.pivot = new Vector2(0.5f, 0f); brt.anchoredPosition = new Vector2(0, 170);

            var fillGo = Img(bar.transform, "Fill", Vector2.zero); var frt = RT(fillGo); Stretch(frt); frt.offsetMin = new Vector2(2, 2); frt.offsetMax = new Vector2(-2, -2);
            var fill = fillGo.GetComponent<Image>(); fill.color = new Color(0.55f, 0.45f, 0.95f, 1f); fill.type = Image.Type.Filled; fill.fillMethod = Image.FillMethod.Horizontal; fill.fillAmount = 0f;

            var iconGo = Img(bar.transform, "Icon", new Vector2(26, 26)); var irt = RT(iconGo); irt.anchorMin = irt.anchorMax = new Vector2(0, 0.5f); irt.pivot = new Vector2(0, 0.5f); irt.anchoredPosition = new Vector2(4, 0);
            var label = Label(bar, "Label", "Conjurando…", 14, TextAlignmentOptions.Center); Stretch(label.rectTransform);

            var so = new SerializedObject(comp);
            Set(so, "panelRoot", bar);
            Set(so, "fill", fill);
            Set(so, "label", label);
            Set(so, "icon", iconGo.GetComponent<Image>());
            so.ApplyModifiedProperties();
            Done(holder, "Cast Bar — barra de conjuração do próprio cast (some quando não conjura)");
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

        // Friend row prefab (name + online status + remove) carrying a FriendRow component.
        private static GameObject FriendRowTemplate(Transform parent)
        {
            var row = Row(parent, "FriendRowTemplate");
            var name = Label(row, "Name", "Amigo", 14, TextAlignmentOptions.Left); Flexible(name.gameObject);
            var status = Label(row, "Status", "Offline", 12, TextAlignmentOptions.Right); Fixed(status.gameObject, 60);
            var whisper = Button(row.transform, "Whisper", "Falar", new Vector2(56, 26)); Fixed(whisper, 56);
            var remove = Button(row.transform, "Remove", "X", new Vector2(26, 26)); Fixed(remove, 26);

            var fr = row.AddComponent<FriendRow>();
            var so = new SerializedObject(fr);
            Set(so, "nameLabel", name); Set(so, "statusLabel", status);
            Set(so, "whisperButton", whisper.GetComponent<Button>());
            Set(so, "removeButton", remove.GetComponent<Button>());
            so.ApplyModifiedProperties();
            row.SetActive(false);
            return row;
        }

        // Guild row prefab (name + rank + status + leader rank-toggle + kick) carrying a GuildRow component.
        private static GameObject GuildRowTemplate(Transform parent)
        {
            var row = Row(parent, "GuildRowTemplate");
            var name = Label(row, "Name", "Membro", 14, TextAlignmentOptions.Left); Flexible(name.gameObject);
            var rank = Label(row, "Rank", "Membro", 12, TextAlignmentOptions.Right); Fixed(rank.gameObject, 56);
            var status = Label(row, "Status", "Offline", 12, TextAlignmentOptions.Right); Fixed(status.gameObject, 52);
            var rankBtn = Button(row.transform, "RankBtn", "Promover", new Vector2(78, 26)); Fixed(rankBtn, 78);
            var kick = Button(row.transform, "Kick", "X", new Vector2(26, 26)); Fixed(kick, 26);

            var gr = row.AddComponent<GuildRow>();
            var so = new SerializedObject(gr);
            Set(so, "nameLabel", name); Set(so, "rankLabel", rank); Set(so, "statusLabel", status);
            Set(so, "rankButton", rankBtn.GetComponent<Button>());
            Set(so, "kickButton", kick.GetComponent<Button>());
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
