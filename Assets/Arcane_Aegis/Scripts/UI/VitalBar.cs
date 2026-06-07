using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Arcane_Aegis.Entities;

namespace Arcane_Aegis.UI
{
    /// <summary>
    /// HUD bar bound to the local player's HP or mana (server-authoritative). Reads the exact vitals from the
    /// local entity view (<see cref="EntityManager.Local"/>). Set <see cref="fill"/> to an Image with
    /// Image Type = Filled (Horizontal). Optional TMP label shows "Hp/Max".
    /// </summary>
    public class VitalBar : MonoBehaviour
    {
        public enum Vital { Hp, Mana }

        [SerializeField] private Vital vital = Vital.Hp;
        [SerializeField] private Image fill;        // Image Type = Filled; fillAmount 0..1
        [SerializeField] private TMP_Text label;    // optional: "1482/2000"
        [SerializeField] private float smoothSpeed = 6f; // bar units/sec (0 = instant snap)

        private EntityManager _entities;
        private float _shown = 1f;

        private void Awake() => _entities = FindAnyObjectByType<EntityManager>();

        private void Update()
        {
            var local = _entities != null ? _entities.Local : null;
            if (local == null) return;

            float target = vital == Vital.Hp ? local.HpFraction : local.ManaFraction;
            _shown = smoothSpeed > 0f ? Mathf.MoveTowards(_shown, target, smoothSpeed * Time.deltaTime) : target;

            if (fill != null) fill.fillAmount = _shown;
            if (label != null)
                label.text = vital == Vital.Hp ? $"{local.Hp}/{local.MaxHp}" : $"{local.Mana}/{local.MaxMana}";
        }
    }
}
