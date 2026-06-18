using System;

namespace Arcane_Aegis.UI
{
    /// <summary>Client-side holder for a PENDING party invite (from S2C_PartyInvite). The invite prompt UI subscribes to
    /// <see cref="OnChanged"/>; answering sends C2S_PartyResponse and clears this.</summary>
    public static class PartyInviteState
    {
        public static string InviterName { get; private set; }
        public static bool HasPending { get; private set; }

        public static event Action OnChanged;

        public static void Show(string inviterName)
        {
            InviterName = inviterName ?? string.Empty;
            HasPending = true;
            OnChanged?.Invoke();
        }

        public static void Clear()
        {
            HasPending = false;
            InviterName = null;
            OnChanged?.Invoke();
        }
    }
}
