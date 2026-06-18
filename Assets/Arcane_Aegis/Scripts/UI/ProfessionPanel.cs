using System;
using System.Collections.Generic;
using UnityEngine;
using ArcaneShared.Enums;

namespace Arcane_Aegis.UI
{
    /// <summary>HUD panel that AUTO-LISTS every profession (one <see cref="ProfessionRow"/> each, in enum order), built once
    /// from a row prefab you wire by hand, then refreshed live from <see cref="ProfessionState"/> (filled from
    /// S2C_ProfessionXp). Adding a profession to the enum adds a row with no code change here.</summary>
    public sealed class ProfessionPanel : MonoBehaviour
    {
        [Tooltip("Row prefab — must have a ProfessionRow component (name/level/XP bar/XP numbers).")]
        [SerializeField] private GameObject rowPrefab;
        [Tooltip("Parent the rows are created under (e.g. a Vertical Layout Group).")]
        [SerializeField] private Transform container;

        private readonly List<(byte prof, ProfessionRow row)> _rows = new();
        private bool _built;

        private void OnEnable() { ProfessionState.OnChanged += Refresh; Refresh(); }
        private void OnDisable() => ProfessionState.OnChanged -= Refresh;

        private void Build()
        {
            if (_built || rowPrefab == null || container == null) return;

            for (int i = container.childCount - 1; i >= 0; i--) Destroy(container.GetChild(i).gameObject); // clear placeholders

            foreach (Profession p in Enum.GetValues(typeof(Profession)))
            {
                GameObject go = Instantiate(rowPrefab, container);
                go.SetActive(true);
                var row = go.GetComponent<ProfessionRow>();
                if (row == null) continue;
                byte key = (byte)p;
                row.SetName(ProfessionState.Name(key));
                _rows.Add((key, row));
            }
            _built = true;
        }

        private void Refresh()
        {
            Build();
            for (int i = 0; i < _rows.Count; i++)
            {
                var (key, row) = _rows[i];
                if (row == null) continue;
                bool has = ProfessionState.TryGet(key, out var e);
                ushort level = has ? e.Level : (ushort)1;     // untrained → show level 1, empty bar
                float frac = has ? ProfessionState.Fraction(key) : 0f;
                row.Set(level, frac, has ? e.Xp : 0, has ? e.XpToNext : 0, has);
            }
        }
    }
}
