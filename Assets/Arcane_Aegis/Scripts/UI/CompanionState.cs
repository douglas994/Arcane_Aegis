using System;
using ArcaneShared.Models;

namespace Arcane_Aegis.UI
{
    /// <summary>Client-side cache of the player's owned pets+mounts (from S2C_OwnedCompanions). The collection panel
    /// rebuilds on <see cref="OnChanged"/>.</summary>
    public static class CompanionState
    {
        public static OwnedCompanion[] Companions = Array.Empty<OwnedCompanion>();
        public static event Action OnChanged;

        public static void Set(OwnedCompanion[] companions)
        {
            Companions = companions ?? Array.Empty<OwnedCompanion>();
            OnChanged?.Invoke();
        }
    }
}
