using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ArcaneShared.Models;

namespace Arcane_Aegis.UI
{
    /// <summary>One row in the character list (put on the row prefab). Click selects it; a separate Enter button confirms.</summary>
    public class CharacterRow : MonoBehaviour
    {
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text infoLabel;   // "Nv 1 · human warrior"
        [SerializeField] private Button button;
        [SerializeField] private GameObject highlight; // optional selected frame

        public uint CharacterId { get; private set; }
        public string CharacterName { get; private set; } = string.Empty;

        public void Bind(CharacterSummary c, Action<CharacterRow> onClick)
        {
            CharacterId = c.Id;
            CharacterName = c.Name;
            if (nameLabel != null) nameLabel.text = c.Name;
            if (infoLabel != null) infoLabel.text = $"Nv {c.Level} · {c.RaceId} {c.ClassId}";
            SetSelected(false);
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => onClick(this));
            }
        }

        public void SetSelected(bool on) { if (highlight != null) highlight.SetActive(on); }
    }
}
