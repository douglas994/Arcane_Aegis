using System.Collections.Generic;
using UnityEngine;

namespace Arcane_Aegis.UI
{
    /// <summary>Client-side cache of the local player's profession mastery (from S2C_ProfessionXp), so a HUD bar can read
    /// it. Keyed by the Profession byte. Names are pt-BR labels for toasts/UI.</summary>
    public static class ProfessionState
    {
        public struct Entry { public ushort Level; public uint Xp, XpToNext; }

        private static readonly Dictionary<byte, Entry> _byProf = new();

        public static void Set(byte prof, ushort level, uint xp, uint xpToNext) => _byProf[prof] = new Entry { Level = level, Xp = xp, XpToNext = xpToNext };
        public static bool TryGet(byte prof, out Entry e) => _byProf.TryGetValue(prof, out e);
        public static float Fraction(byte prof) => _byProf.TryGetValue(prof, out var e) && e.XpToNext > 0 ? Mathf.Clamp01((float)e.Xp / e.XpToNext) : 0f;

        public static string Name(byte prof) => prof switch
        {
            0 => "Lenhador", 1 => "Minerador", 2 => "Herborista", 3 => "Esfolador", 4 => "Pescador", 5 => "Fazendeiro", _ => "Profissão",
        };
    }
}
