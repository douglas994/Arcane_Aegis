using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Arcane_Aegis.Entities;

namespace Arcane_Aegis.UI
{
    /// <summary>
    /// HUD XP bar: fills from the local player's XP/XpToNext (from S2C_Experience) and shows the level + percent.
    /// Assign a Filled Image (Horizontal) to <see cref="fill"/>; optional level/percent labels. Binds to
    /// EntityManager.Local each frame (cheap), so it works as soon as the player spawns.
    /// </summary>
    public sealed class ExpBar : MonoBehaviour
    {
        [SerializeField] private Image fill;          // Image Type = Filled, Fill Method = Horizontal
        [SerializeField] private TMP_Text levelText;  // optional: "Lv. 12"
        [SerializeField] private TMP_Text percentText;// optional: "67.2%"
        [SerializeField] private float lerp = 8f;     // smooth the fill

        private EntityManager _entities;

        private void Awake() => _entities = FindAnyObjectByType<EntityManager>();

        private void Update()
        {
            if (_entities == null) _entities = FindAnyObjectByType<EntityManager>();
            var local = _entities != null ? _entities.Local : null;
            if (local == null) return;

            float target = local.XpFraction;
            if (fill != null) fill.fillAmount = Mathf.Lerp(fill.fillAmount, target, Time.deltaTime * lerp);
            if (levelText != null) levelText.text = $"Lv. {local.Level}";
            if (percentText != null) percentText.text = $"{target * 100f:0.0}%";
        }
    }
}
