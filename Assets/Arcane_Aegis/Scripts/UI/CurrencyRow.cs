using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Arcane_Aegis.UI
{
    /// <summary>One currency line in the <see cref="CurrencyHud"/> (name + amount + optional icon). Build the row prefab by
    /// hand and wire these refs; the HUD instantiates one per defined currency and updates its amount live.</summary>
    public sealed class CurrencyRow : MonoBehaviour
    {
        [Tooltip("Optional: the currency's display name (e.g. 'Ouro').")] [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text amountLabel;
        [Tooltip("Optional: the currency icon.")] [SerializeField] private Image icon;

        /// <summary>Sets the fixed parts (name + icon) — called once when the row is created.</summary>
        public void SetDefinition(string displayName, Sprite sprite)
        {
            if (nameLabel != null) nameLabel.text = displayName;
            if (icon != null)
            {
                icon.sprite = sprite;
                icon.enabled = sprite != null;
            }
        }

        /// <summary>Updates the live balance.</summary>
        public void SetAmount(long amount)
        {
            if (amountLabel != null) amountLabel.text = amount.ToString();
        }
    }
}
