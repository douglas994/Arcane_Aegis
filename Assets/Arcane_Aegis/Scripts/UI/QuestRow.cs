using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Arcane_Aegis.UI
{
    /// <summary>One row of the quest panel / log: a title, a body (description + objective progress), and an optional
    /// action button (Aceitar / Entregar). Build the prefab by hand and wire the four refs.</summary>
    public sealed class QuestRow : MonoBehaviour
    {
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text bodyText;
        [SerializeField] private Button actionButton;
        [SerializeField] private TMP_Text actionLabel;

        public void Bind(string title, string body, string action, Action onClick)
        {
            if (titleText != null) titleText.text = title;
            if (bodyText != null) bodyText.text = body;
            if (actionButton == null) return;
            actionButton.onClick.RemoveAllListeners();
            bool hasAction = !string.IsNullOrEmpty(action);
            actionButton.gameObject.SetActive(hasAction);
            if (hasAction)
            {
                if (actionLabel != null) actionLabel.text = action;
                actionButton.onClick.AddListener(() => onClick?.Invoke());
            }
        }
    }
}
