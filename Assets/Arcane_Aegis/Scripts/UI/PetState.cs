using System;

namespace Arcane_Aegis.UI
{
    /// <summary>Client-side cache of the OWNER's active pet (from S2C_PetState). <see cref="HasPet"/> is false when no
    /// pet is out (the HUD frame should hide). The pet HUD rebuilds on <see cref="OnChanged"/>.</summary>
    public static class PetState
    {
        public static bool HasPet;       // a pet is currently out
        public static ushort EntityId;   // the pet's world entity id (to read live HP from its view, if wanted)
        public static string Name = "";
        public static ushort Level = 1;
        public static uint Xp;
        public static uint XpToNext = 1;

        /// <summary>Fill ratio for the XP bar (0..1).</summary>
        public static float XpFill => XpToNext == 0 ? 0f : System.Math.Min(1f, (float)Xp / XpToNext);

        public static event Action OnChanged;       // any change (show/hide/level/xp)
        public static event Action<ushort> OnLevelUp; // pet just crossed a level (arg = new level)

        public static void Set(ushort entityId, string name, ushort level, uint xp, uint xpToNext, bool leveledUp)
        {
            HasPet = entityId != 0;
            EntityId = entityId;
            Name = name ?? "";
            Level = level;
            Xp = xp;
            XpToNext = xpToNext == 0 ? 1u : xpToNext;
            OnChanged?.Invoke();
            if (leveledUp) OnLevelUp?.Invoke(level);
        }
    }
}
