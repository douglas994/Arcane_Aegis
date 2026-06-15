using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Arcane_Aegis.UI
{
    /// <summary>One row in the shop (a buy entry or a sell entry): icon, name, price, action button. Built by
    /// <see cref="ShopPanel"/>. Author the prefab (icon + texts + button) and wire the fields.</summary>
    public sealed class ShopRow : MonoBehaviour
    {
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text priceLabel;
        [SerializeField] private Button button;

        private Action _onClick;

        public void Bind(Sprite sprite, string title, string price, string buttonText, Action onClick, bool enabled = true)
        {
            _onClick = onClick;
            if (icon != null) { icon.sprite = sprite; icon.enabled = sprite != null; }
            if (nameLabel != null) nameLabel.text = title;
            if (priceLabel != null) priceLabel.text = price;
            if (button != null)
            {
                button.interactable = enabled;
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => _onClick?.Invoke());
                var t = button.GetComponentInChildren<TMP_Text>();
                if (t != null && !string.IsNullOrEmpty(buttonText)) t.text = buttonText;
            }
        }
    }
}
