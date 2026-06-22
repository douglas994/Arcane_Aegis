using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Arcane_Aegis.UI
{
    /// <summary>One buff/debuff icon in the <see cref="StatusBar"/>: the status icon, a radial "time left" overlay, and a
    /// stack count. Pooled/instantiated by StatusBar from a template — wire the three refs on the template.</summary>
    public sealed class StatusSlot : MonoBehaviour
    {
        [SerializeField] private Image icon;    // the status art
        [SerializeField] private Image fill;    // Image type=Filled, Radial360 — shows remaining time
        [SerializeField] private TMP_Text stacks;

        public void Bind(Sprite sprite, float remain01, int stackCount)
        {
            if (icon != null) { icon.sprite = sprite; icon.enabled = sprite != null; }
            if (fill != null) fill.fillAmount = remain01;
            if (stacks != null) stacks.text = stackCount > 1 ? stackCount.ToString() : "";
        }
    }
}
