using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Arcane_Aegis.UI
{
    /// <summary>HUD that shows the player's wallet balances (from <see cref="CurrencyState"/>). You build it: add one
    /// <see cref="Row"/> per currency you want visible and wire its amount text (+ optional icon). Refreshes live.</summary>
    public sealed class CurrencyHud : MonoBehaviour
    {
        [Serializable]
        public struct Row
        {
            [Tooltip("Currency id (ex.: 'gold').")] public string currencyId;
            public TMP_Text amountLabel;
            [Tooltip("Optional icon (kept as-is; this just shows the amount).")] public Image icon;
        }

        [SerializeField] private Row[] rows;

        private void OnEnable() { CurrencyState.OnChanged += Refresh; Refresh(); }
        private void OnDisable() => CurrencyState.OnChanged -= Refresh;

        private void Refresh()
        {
            if (rows == null) return;
            for (int i = 0; i < rows.Length; i++)
                if (rows[i].amountLabel != null)
                    rows[i].amountLabel.text = CurrencyState.Amount(rows[i].currencyId).ToString();
        }
    }
}
