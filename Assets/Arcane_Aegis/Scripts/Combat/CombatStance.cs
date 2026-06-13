using System.Collections.Generic;
using UnityEngine;

namespace Arcane_Aegis.Combat
{
    /// <summary>
    /// Client-side "in combat?" tracker, derived purely from the combat packets the client already receives
    /// (a cast or a damage/heal event marks the involved entities as in-combat for a short window). Cosmetic only —
    /// drives weapon sheathing (hand ↔ back). No server/protocol cost; the stance matches what the player sees.
    /// </summary>
    public static class CombatStance
    {
        /// <summary>Seconds an entity stays "in combat" after the last combat event involving it.</summary>
        public static float Window = 4f;

        private static readonly Dictionary<ushort, float> _last = new();

        /// <summary>Mark an entity as in-combat now (call from cast/combat-event handlers).</summary>
        public static void Mark(ushort id) { if (id != 0) _last[id] = Time.time; }

        /// <summary>True if the entity had a combat event within the window.</summary>
        public static bool InCombat(ushort id) => _last.TryGetValue(id, out var t) && Time.time - t < Window;
    }
}
