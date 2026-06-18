using System.Collections.Generic;
using UnityEngine;
using Arcane_Aegis.Content;

namespace Arcane_Aegis.UI
{
    /// <summary>HUD that AUTO-LISTS the player's wallet: one <see cref="CurrencyRow"/> per currency defined in the
    /// <see cref="ContentLibrary"/> (sorted by <c>sortOrder</c>), built once from a row prefab you wire by hand, then
    /// refreshed live from <see cref="CurrencyState"/>. New currencies authored in the editor show up with no code change.</summary>
    public sealed class CurrencyHud : MonoBehaviour
    {
        [Tooltip("Row prefab — must have a CurrencyRow component (amount label + optional name/icon).")]
        [SerializeField] private GameObject rowPrefab;
        [Tooltip("Parent the rows are created under (e.g. a Vertical/Horizontal Layout Group).")]
        [SerializeField] private Transform container;
        [Tooltip("Source of currency definitions (id/displayName/icon/order). Falls back to ContentLibrary.Active.")]
        [SerializeField] private ContentLibrary library;
        [Tooltip("Hide a currency's row until the player actually owns some of it.")]
        [SerializeField] private bool hideUntilOwned;

        private readonly List<(string id, CurrencyRow row)> _rows = new();
        private bool _built;

        private void OnEnable() { CurrencyState.OnChanged += Refresh; Refresh(); }
        private void OnDisable() => CurrencyState.OnChanged -= Refresh;

        // Build is idempotent + deferred: if the ContentLibrary isn't loaded yet at OnEnable, the next refresh retries.
        private void Build()
        {
            if (_built || rowPrefab == null || container == null) return;
            ContentLibrary lib = library != null ? library : ContentLibrary.Active;
            if (lib == null || lib.currencies == null) return;

            for (int i = container.childCount - 1; i >= 0; i--) Destroy(container.GetChild(i).gameObject); // clear placeholders

            var defs = new List<CurrencyDefinitionSO>(lib.currencies);
            defs.RemoveAll(c => c == null || string.IsNullOrEmpty(c.id));
            defs.Sort((a, b) => a.sortOrder.CompareTo(b.sortOrder));

            foreach (var def in defs)
            {
                GameObject go = Instantiate(rowPrefab, container);
                go.SetActive(true);
                var row = go.GetComponent<CurrencyRow>();
                if (row == null) continue;
                row.SetDefinition(string.IsNullOrEmpty(def.displayName) ? def.id : def.displayName, def.icon);
                _rows.Add((def.id, row));
            }
            _built = true;
        }

        private void Refresh()
        {
            Build();
            for (int i = 0; i < _rows.Count; i++)
            {
                var (id, row) = _rows[i];
                if (row == null) continue;
                long amount = CurrencyState.Amount(id);
                if (hideUntilOwned) row.gameObject.SetActive(amount > 0);
                row.SetAmount(amount);
            }
        }
    }
}
