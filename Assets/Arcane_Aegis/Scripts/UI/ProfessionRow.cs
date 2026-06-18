using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Arcane_Aegis.UI
{
    /// <summary>One profession line in the <see cref="ProfessionPanel"/> (name + level + XP bar + XP numbers). Build the row
    /// prefab by hand and wire these refs; the panel instantiates one per profession and refreshes it live.</summary>
    public sealed class ProfessionRow : MonoBehaviour
    {
        [Tooltip("Optional: the profession name (e.g. 'Lenhador').")] [SerializeField] private TMP_Text nameLabel;
        [Tooltip("Optional: the level label (shows 'Nv X').")] [SerializeField] private TMP_Text levelLabel;
        [Tooltip("Optional: XP bar (Image Type = Filled).")] [SerializeField] private Image fill;
        [Tooltip("Optional: XP numbers (e.g. '320/500').")] [SerializeField] private TMP_Text xpLabel;

        /// <summary>Sets the fixed name — called once when the row is created.</summary>
        public void SetName(string name) { if (nameLabel != null) nameLabel.text = name; }

        /// <summary>Updates the live mastery. <paramref name="trained"/> false → untrained (level 1, empty bar, "0/0").</summary>
        public void Set(ushort level, float fraction, uint xp, uint xpToNext, bool trained)
        {
            if (levelLabel != null) levelLabel.text = $"Nv {level}";
            if (fill != null) fill.fillAmount = fraction;
            if (xpLabel != null) xpLabel.text = trained ? $"{xp}/{xpToNext}" : "0/0";
        }
    }
}
