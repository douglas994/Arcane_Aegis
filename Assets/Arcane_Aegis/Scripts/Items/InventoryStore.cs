using System;
using System.Collections.Generic;
using UnityEngine;
using ArcaneShared.Models;

namespace Arcane_Aegis.Items
{
    /// <summary>
    /// Client-side inventory state: the item-template catalog (moulds, from S2C_ItemTemplates) + the player's current
    /// items (from S2C_InventoryState). Persistent singleton (auto-created) so handlers can fill it and the UI can read
    /// it. Raises <see cref="OnChanged"/> whenever the item list updates so the bag UI re-renders.
    /// </summary>
    public sealed class InventoryStore : MonoBehaviour
    {
        public static InventoryStore Instance { get; private set; }

        private readonly Dictionary<string, ItemTemplate> _catalog = new();
        private ItemInstance[] _items = Array.Empty<ItemInstance>();

        /// <summary>Raised when the item list changes (the bag UI subscribes to re-render).</summary>
        public event Action OnChanged;

        public IReadOnlyList<ItemInstance> Items => _items;

        public bool TryGetTemplate(string id, out ItemTemplate template) => _catalog.TryGetValue(id ?? "", out template);

        public void SetCatalog(ItemTemplate[] templates)
        {
            _catalog.Clear();
            if (templates != null)
                foreach (var t in templates)
                    if (!string.IsNullOrEmpty(t.Id)) _catalog[t.Id] = t;
        }

        public void SetItems(ItemInstance[] items)
        {
            _items = items ?? Array.Empty<ItemInstance>();
            OnChanged?.Invoke();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            var go = new GameObject("InventoryStore");
            Instance = go.AddComponent<InventoryStore>();
            DontDestroyOnLoad(go);
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
    }
}
