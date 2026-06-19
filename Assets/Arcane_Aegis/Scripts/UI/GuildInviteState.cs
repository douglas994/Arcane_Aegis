using System;

namespace Arcane_Aegis.UI
{
    /// <summary>Holds a PENDING guild invite (from S2C_GuildInvite). The prompt UI subscribes; answering sends
    /// C2S_GuildResponse and clears this.</summary>
    public static class GuildInviteState
    {
        public static string InviterName { get; private set; }
        public static string GuildName { get; private set; }
        public static bool HasPending { get; private set; }

        public static event Action OnChanged;

        public static void Show(string inviterName, string guildName)
        {
            InviterName = inviterName ?? string.Empty;
            GuildName = guildName ?? string.Empty;
            HasPending = true;
            OnChanged?.Invoke();
        }

        public static void Clear()
        {
            HasPending = false;
            InviterName = null;
            GuildName = null;
            OnChanged?.Invoke();
        }
    }
}
