using System;
using ArcaneShared.Models;

namespace Arcane_Aegis.UI
{
    /// <summary>Client-side cache of the local player's friend list (from S2C_FriendList, with live online/zone). Raises
    /// <see cref="OnChanged"/> so the friends panel refreshes live.</summary>
    public static class FriendState
    {
        private static FriendEntry[] _friends = Array.Empty<FriendEntry>();

        public static event Action OnChanged;
        public static FriendEntry[] Friends => _friends;

        public static void Set(FriendEntry[] friends)
        {
            _friends = friends ?? Array.Empty<FriendEntry>();
            OnChanged?.Invoke();
        }
    }
}
