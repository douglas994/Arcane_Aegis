using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ArcaneShared.Enums;

namespace Arcane_Aegis.UI
{
    /// <summary>
    /// HUD panel that shows the local player's profession mastery (level + XP bar), driven by <see cref="ProfessionState"/>
    /// (filled from S2C_ProfessionXp). You build the UI by hand: add one <see cref="Row"/> per profession you want to show
    /// and drag its texts/fill in the inspector. Refreshes live on every mastery change.
    /// </summary>
    public sealed class ProfessionPanel : MonoBehaviour
    {
        [Serializable]
        public struct Row
        {
            public Profession profession;
            [Tooltip("Optional: the profession name label (e.g. 'Lenhador').")] public TMP_Text nameLabel;
            [Tooltip("Optional: the level label (shows 'Nv X').")] public TMP_Text levelLabel;
            [Tooltip("Optional: XP bar (Image Type = Filled).")] public Image fill;
            [Tooltip("Optional: XP numbers (e.g. '320/500').")] public TMP_Text xpLabel;
        }

        [Tooltip("One row per profession to display. Each field is optional — wire only what your layout uses.")]
        [SerializeField] private Row[] rows;

        private void OnEnable()
        {
            ProfessionState.OnChanged += Refresh;
            Refresh();
        }

        private void OnDisable() => ProfessionState.OnChanged -= Refresh;

        private void Refresh()
        {
            if (rows == null) return;
            for (int i = 0; i < rows.Length; i++)
            {
                var r = rows[i];
                byte key = (byte)r.profession;
                bool has = ProfessionState.TryGet(key, out var e);
                ushort level = has ? e.Level : (ushort)1; // untrained → show level 1, empty bar
                float frac = has ? ProfessionState.Fraction(key) : 0f;

                if (r.nameLabel != null) r.nameLabel.text = ProfessionState.Name(key);
                if (r.levelLabel != null) r.levelLabel.text = $"Nv {level}";
                if (r.fill != null) r.fill.fillAmount = frac;
                if (r.xpLabel != null) r.xpLabel.text = has ? $"{e.Xp}/{e.XpToNext}" : "0/0";
            }
        }
    }
}
