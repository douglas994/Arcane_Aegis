using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Arcane_Aegis.UI
{
    /// <summary>HUD bar bound to the local player's HUNGER or THIRST (server-authoritative, via <see cref="SurvivalState"/>).
    /// Mirror of <see cref="VitalBar"/>: set <see cref="fill"/> to an Image (Type = Filled, Horizontal); optional TMP label
    /// shows "cur/max". Drop two of these on the HUD (one Hunger, one Thirst) and style by hand.</summary>
    public class SurvivalBar : MonoBehaviour
    {
        public enum Need { Hunger, Thirst, Stamina }

        [SerializeField] private Need need = Need.Hunger;
        [SerializeField] private Image fill;        // Image Type = Filled; fillAmount 0..1
        [SerializeField] private TMP_Text label;    // optional: "73/100"
        [SerializeField] private float smoothSpeed = 6f; // bar units/sec (0 = instant)

        private float _shown = 1f;

        private void Update()
        {
            float target = need switch
            {
                Need.Thirst => SurvivalState.ThirstFraction,
                Need.Stamina => SurvivalState.StaminaFraction,
                _ => SurvivalState.HungerFraction,
            };
            _shown = smoothSpeed > 0f ? Mathf.MoveTowards(_shown, target, smoothSpeed * Time.deltaTime) : target;

            if (fill != null) fill.fillAmount = _shown;
            if (label != null)
                label.text = need switch
                {
                    Need.Thirst => $"{SurvivalState.Thirst}/{SurvivalState.MaxThirst}",
                    Need.Stamina => $"{SurvivalState.Stamina}/{SurvivalState.MaxStamina}",
                    _ => $"{SurvivalState.Hunger}/{SurvivalState.MaxHunger}",
                };
        }
    }
}
