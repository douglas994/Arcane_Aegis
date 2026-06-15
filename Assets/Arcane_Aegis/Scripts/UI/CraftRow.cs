using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Arcane_Aegis.UI
{
    /// <summary>One recipe row in the crafting panel: name, output, the "have/need" ingredient list, and a Craft button.
    /// Built by <see cref="CraftingPanel"/>. You author the prefab (texts + button + optional icon) and wire the fields.</summary>
    public sealed class CraftRow : MonoBehaviour
    {
        [SerializeField] private Image icon;            // optional: output icon
        [SerializeField] private TMP_Text nameLabel;    // recipe / output name
        [SerializeField] private TMP_Text ingredientsLabel; // "Madeira 2/3  ·  Couro 0/1"
        [SerializeField] private Button craftButton;

        private Action _onCraft;

        public void Bind(Sprite outputIcon, string title, Action onCraft)
        {
            _onCraft = onCraft;
            if (icon != null) { icon.sprite = outputIcon; icon.enabled = outputIcon != null; }
            if (nameLabel != null) nameLabel.text = title;
            if (craftButton != null)
            {
                craftButton.onClick.RemoveAllListeners();
                craftButton.onClick.AddListener(() => _onCraft?.Invoke());
            }
        }

        /// <summary>Updates the live "have/need" line + enables the button only when everything is in the bag.</summary>
        public void SetStatus(string ingredientsText, bool canCraft)
        {
            if (ingredientsLabel != null) ingredientsLabel.text = ingredientsText;
            if (craftButton != null) craftButton.interactable = canCraft;
        }
    }
}
