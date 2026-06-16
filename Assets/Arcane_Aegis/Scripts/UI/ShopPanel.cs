using System.Collections.Generic;
using UnityEngine;
using TMPro;
using ArcaneShared.Enums;
using Arcane_Aegis.Content;
using Arcane_Aegis.Items;
using Arcane_Aegis.Network;

namespace Arcane_Aegis.UI
{
    /// <summary>
    /// The shop window: a BUY list (the vendor's stock, priced by each item's npcPrice/currency) and a SELL list (your
    /// sellable bag items, at a fraction of the price). Buy/Sell ask the server (it validates range/gold/space). Opened by
    /// <see cref="ShopController"/> near a vendor. You build the UI: a row prefab (ShopRow) + two list parents.
    /// </summary>
    public class ShopPanel : MonoBehaviour
    {
        public const float SellFactor = 0.25f; // mirror the server (display only)

        public static ShopPanel Instance { get; private set; }

        [SerializeField] private ContentLibrary library;
        [SerializeField] private GameObject panel;       // root, toggled
        [SerializeField] private TMP_Text titleLabel;    // vendor name
        [SerializeField] private GameObject rowPrefab;   // ShopRow
        [SerializeField] private Transform buyParent;    // where buy rows go
        [SerializeField] private Transform sellParent;   // where sell rows go

        private readonly List<GameObject> _buyRows = new();
        private readonly List<GameObject> _sellRows = new();
        private VendorDefinitionSO _vendor;

        private void Awake() { Instance = this; if (panel != null) panel.SetActive(false); }
        private void OnDestroy() { if (Instance == this) Instance = null; }

        private void OnEnable() { if (InventoryStore.Instance != null) InventoryStore.Instance.OnChanged += RebuildSell; }
        private void OnDisable() { if (InventoryStore.Instance != null) InventoryStore.Instance.OnChanged -= RebuildSell; }

        /// <summary>Opens the shop for a vendor id (resolved from the ContentLibrary).</summary>
        public void Open(string vendorId)
        {
            if (library == null) return;
            _vendor = library.GetVendor(vendorId);
            if (_vendor == null) return;
            if (titleLabel != null) titleLabel.text = string.IsNullOrEmpty(_vendor.displayName) ? _vendor.name : _vendor.displayName;
            if (panel != null) panel.SetActive(true);
            RebuildBuy();
            RebuildSell();
        }

        public void Close() { if (panel != null) panel.SetActive(false); _vendor = null; }
        public bool IsOpen => panel != null && panel.activeSelf;

        /// <summary>Repairs ALL damaged gear at the vendor (server charges gold + restores). Wire to a "Consertar" button.</summary>
        public void RepairAll() { if (NetClient.Instance != null) NetClient.Instance.SendVendorRepair(); }

        private void RebuildBuy()
        {
            Clear(_buyRows);
            if (_vendor == null || rowPrefab == null || buyParent == null) return;
            foreach (var item in _vendor.stock)
            {
                if (item == null) continue;
                var row = NewRow(buyParent, _buyRows);
                if (row == null) continue;
                string price = $"{item.npcPrice} {CurrencyName(item.priceCurrency)}";
                string iid = item.id;
                // Always clickable — the server validates gold/space and replies with a Notice if you can't afford it.
                row.Bind(item.icon, Display(item.displayName, item.name), price, "Comprar",
                    () => { if (NetClient.Instance != null) NetClient.Instance.SendVendorBuy(iid, 1); });
            }
        }

        private void RebuildSell()
        {
            Clear(_sellRows);
            var store = InventoryStore.Instance;
            if (store == null || rowPrefab == null || sellParent == null) return;
            foreach (var it in store.Items)
            {
                if (it.Container != ItemContainer.Bag) continue;
                if (!store.TryGetTemplate(it.TemplateId, out var tpl)) continue;
                if (!tpl.Sellable || string.IsNullOrEmpty(tpl.PriceCurrency) || tpl.NpcPrice <= 0) continue;
                long unit = (long)Mathf.Max(1, Mathf.Floor(tpl.NpcPrice * SellFactor));
                var row = NewRow(sellParent, _sellRows);
                if (row == null) continue;
                Sprite icon = library != null ? IconOf(it.TemplateId) : null;
                string title = it.Quantity > 1 ? $"{ItemName(it.TemplateId, tpl.Name)} x{it.Quantity}" : ItemName(it.TemplateId, tpl.Name);
                uint inst = it.InstanceId;
                row.Bind(icon, title, $"+{unit} {CurrencyName(tpl.PriceCurrency)}", "Vender",
                    () => { if (NetClient.Instance != null) NetClient.Instance.SendVendorSell(inst, 1); });
            }
        }

        private ShopRow NewRow(Transform parent, List<GameObject> bucket)
        {
            var go = Instantiate(rowPrefab, parent);
            go.SetActive(true);
            bucket.Add(go);
            return go.GetComponent<ShopRow>();
        }

        /// <summary>Destroys only the rows this bucket created — so buy + sell can share one parent without wiping each other.</summary>
        private static void Clear(List<GameObject> bucket)
        {
            for (int i = bucket.Count - 1; i >= 0; i--) if (bucket[i] != null) Destroy(bucket[i]);
            bucket.Clear();
        }

        private string CurrencyName(string currencyId)
        {
            if (library != null) { var c = library.GetCurrency(currencyId); if (c != null) return Display(c.displayName, c.name); }
            return currencyId ?? "";
        }

        private Sprite IconOf(string itemId) { var so = library.GetItem(itemId); return so != null ? so.icon : null; }
        private string ItemName(string itemId, string fallback) { var so = library != null ? library.GetItem(itemId) : null; return so != null ? Display(so.displayName, so.name) : (string.IsNullOrEmpty(fallback) ? itemId : fallback); }
        private static string Display(string primary, string fallback) => string.IsNullOrEmpty(primary) ? fallback : primary;
    }
}
