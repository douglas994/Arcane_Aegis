using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Arcane_Aegis.UI
{
    /// <summary>
    /// One floating enemy health bar (you build the visual). Put this on your bar prefab and assign the <see cref="fill"/>
    /// Image (set its Image Type = Filled, Fill Method = Horizontal) and an optional <see cref="label"/> (name/level).
    /// The <see cref="EnemyHealthBars"/> pool instantiates, positions and fills it — you only design how it looks.
    /// </summary>
    public sealed class EnemyBar : MonoBehaviour
    {
        [SerializeField] private Image fill;       // Image Type = Filled, Fill Method = Horizontal
        [SerializeField] private TMP_Text label;   // optional: name / level

        public RectTransform Rt => (RectTransform)transform;

        public void SetHp(float f01) { if (fill != null) fill.fillAmount = Mathf.Clamp01(f01); }
        public void SetLabel(string s) { if (label != null) label.text = s; }
        public void SetVisible(bool on) { if (gameObject.activeSelf != on) gameObject.SetActive(on); }
    }
}
