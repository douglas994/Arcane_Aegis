using System.Collections.Generic;
using UnityEngine;
using ArcaneShared.Enums;
using Arcane_Aegis.Content;
using Arcane_Aegis.Controllers;
using Arcane_Aegis.Items;

namespace Arcane_Aegis.UI
{
    /// <summary>Build menu: lists the building pieces from the ContentLibrary, each as a row (icon + name + material
    /// cost). Clicking a row tells the <see cref="BuildModeController"/> to start placing that piece. Reuses the CraftRow
    /// prefab (Bind + SetStatus). You build the UI: a row prefab (CraftRow) + a list parent (Vertical Layout Group).</summary>
    public class BuildMenuUI : MonoBehaviour
    {
        [Tooltip("Resolves building pieces + item costs for display.")]
        [SerializeField] private ContentLibrary library;
        [Tooltip("The build engine (ghost/place/demolish). Clicking a row calls SelectPiece on it.")]
        [SerializeField] private BuildModeController builder;
        [Tooltip("Row prefab — reuse CraftRow (icon + title + button + status).")]
        [SerializeField] private GameObject rowPrefab;
        [Tooltip("Parent (e.g. a Vertical Layout Group) the rows are created under.")]
        [SerializeField] private Transform listParent;

        private readonly List<(BuildingDefinitionSO def, CraftRow row)> _rows = new();
        private bool _built;

        private void OnEnable()
        {
            Build();
            if (InventoryStore.Instance != null) InventoryStore.Instance.OnChanged += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            if (InventoryStore.Instance != null) InventoryStore.Instance.OnChanged -= Refresh;
        }

        private void Build()
        {
            if (_built || library == null || rowPrefab == null || listParent == null) return;
            _built = true;

            for (int i = listParent.childCount - 1; i >= 0; i--) Destroy(listParent.GetChild(i).gameObject); // clear placeholders

            foreach (var def in library.buildingDefs)
            {
                if (def == null) continue;
                var go = Instantiate(rowPrefab, listParent);
                go.SetActive(true);
                var row = go.GetComponent<CraftRow>();
                if (row == null) continue;

                string title = Display(def.displayName, def.name);
                var picked = def;
                row.Bind(def.icon, title, () => { if (builder != null) builder.SelectPiece(picked); });
                _rows.Add((def, row));
            }
        }

        private void Refresh()
        {
            var store = InventoryStore.Instance;
            for (int i = 0; i < _rows.Count; i++)
            {
                var (def, row) = _rows[i];
                bool canBuild = true;
                var sb = new System.Text.StringBuilder();
                var ings = def.ingredients;
                for (int k = 0; k < ings.Count; k++)
                {
                    var ing = ings[k];
                    if (ing.item == null) continue;
                    int have = store != null ? CountInBag(store, ing.item.id) : 0;
                    if (have < ing.qty) canBuild = false;
                    if (sb.Length > 0) sb.Append("   ");
                    sb.Append(Display(ing.item.displayName, ing.item.name)).Append(' ').Append(have).Append('/').Append(ing.qty);
                }
                row.SetStatus(sb.ToString(), canBuild);
            }
        }

        private static int CountInBag(InventoryStore store, string templateId)
        {
            int n = 0;
            var items = store.Items;
            for (int i = 0; i < items.Count; i++)
                if (items[i].Container == ItemContainer.Bag && items[i].TemplateId == templateId) n += items[i].Quantity;
            return n;
        }

        private static string Display(string primary, string fallback) => string.IsNullOrEmpty(primary) ? fallback : primary;
    }
}
