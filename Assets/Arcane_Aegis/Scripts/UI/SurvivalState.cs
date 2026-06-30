using System;

namespace Arcane_Aegis.UI
{
    /// <summary>Client-side cache of the local player's survival meters (hunger/thirst), carried on S2C_StateUpdate
    /// (owner-only, like HP/mana). A HUD reads it; <see cref="OnChanged"/> fires on every update so bars can refresh.</summary>
    public static class SurvivalState
    {
        public static int Hunger { get; private set; }
        public static int MaxHunger { get; private set; } = 100;
        public static int Thirst { get; private set; }
        public static int MaxThirst { get; private set; } = 100;
        public static int Stamina { get; private set; } = 100;
        public static int MaxStamina { get; private set; } = 100;

        public static event Action OnChanged;

        public static float HungerFraction => MaxHunger > 0 ? (float)Hunger / MaxHunger : 0f;
        public static float ThirstFraction => MaxThirst > 0 ? (float)Thirst / MaxThirst : 0f;
        public static float StaminaFraction => MaxStamina > 0 ? (float)Stamina / MaxStamina : 0f;

        public static void Set(int hunger, int maxHunger, int thirst, int maxThirst, int stamina, int maxStamina)
        {
            Hunger = hunger; MaxHunger = maxHunger > 0 ? maxHunger : 100;
            Thirst = thirst; MaxThirst = maxThirst > 0 ? maxThirst : 100;
            Stamina = stamina; MaxStamina = maxStamina > 0 ? maxStamina : 100;
            OnChanged?.Invoke();
        }
    }
}
