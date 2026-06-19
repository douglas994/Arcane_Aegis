using System;

namespace Arcane_Aegis.UI
{
    /// <summary>Holds a PENDING friend request (from S2C_FriendRequest). The prompt UI subscribes; answering sends
    /// C2S_FriendResponse and clears this.</summary>
    public static class FriendRequestState
    {
        public static string RequesterName { get; private set; }
        public static bool HasPending { get; private set; }

        public static event Action OnChanged;

        public static void Show(string requesterName)
        {
            RequesterName = requesterName ?? string.Empty;
            HasPending = true;
            OnChanged?.Invoke();
        }

        public static void Clear()
        {
            HasPending = false;
            RequesterName = null;
            OnChanged?.Invoke();
        }
    }
}
