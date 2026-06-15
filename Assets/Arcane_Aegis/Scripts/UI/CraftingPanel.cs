using System.Collections.Generic;
using UnityEngine;
using ArcaneShared.Enums;
using Arcane_Aegis.Content;
using Arcane_Aegis.Items;
using Arcane_Aegis.Network;

namespace Arcane_Aegis.UI
{
    /// <summary>
    /// Crafting panel: lists the recipes for one <see cref="profession"/> from the ContentLibrary, each as a CraftRow
    /// (name + "have/need" ingredients + Craft button). Clicking Craft asks the server (it validates + resolves on a
    /// timer; the item/XP come back via inventory + profession packets). Live-refreshes ingredient counts on bag changes.
    /// You build the UI: a row prefab (CraftRow) + a list parent (e.g. a Vertical Layout Group).
    /// </summary>
    public class CraftingPanel : MonoBehaviour
    {
        [Tooltip("Resolves recipes/items for display.")]
        [SerializeField] private ContentLibrary library;
        [Tooltip("Which crafting profession's recipes to show.")]
        [SerializeField] private Profession profession = Profession.Carpentry;
        [Tooltip("Row prefab — must have a CraftRow component.")]
        [SerializeField] private GameObject rowPrefab;
        [Tooltip("Parent (e.g. a Vertical Layout Group) the rows are created under.")]
        [SerializeField] private Transform listParent;

        private readonly List<(RecipeDefinitionSO recipe, CraftRow row)> _rows = new();
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

            foreach (var recipe in library.recipes)
            {
                if (recipe == null || recipe.profession != profession) continue;
                var go = Instantiate(rowPrefab, listParent);
                go.SetActive(true);
                var row = go.GetComponent<CraftRow>();
                if (row == null) continue;

                string outName = recipe.output != null ? Display(recipe.output.displayName, recipe.output.name) : recipe.displayName;
                string title = recipe.outputQty > 1 ? $"{Display(recipe.displayName, outName)} x{recipe.outputQty}" : Display(recipe.displayName, outName);
                Sprite icon = recipe.output != null ? recipe.output.icon : recipe.icon;
                string rid = recipe.id;
                row.Bind(icon, title, () => { if (NetClient.Instance != null) NetClient.Instance.SendCraft(rid); });
                _rows.Add((recipe, row));
            }
        }

        private void Refresh()
        {
            var store = InventoryStore.Instance;
            for (int i = 0; i < _rows.Count; i++)
            {
                var (recipe, row) = _rows[i];
                bool canCraft = true;
                var sb = new System.Text.StringBuilder();
                var ings = recipe.ingredients;
                for (int k = 0; k < ings.Count; k++)
                {
                    var ing = ings[k];
                    if (ing.item == null) continue;
                    int have = store != null ? CountInBag(store, ing.item.id) : 0;
                    if (have < ing.qty) canCraft = false;
                    if (sb.Length > 0) sb.Append("   ");
                    sb.Append(Display(ing.item.displayName, ing.item.name)).Append(' ').Append(have).Append('/').Append(ing.qty);
                }
                row.SetStatus(sb.ToString(), canCraft);
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
