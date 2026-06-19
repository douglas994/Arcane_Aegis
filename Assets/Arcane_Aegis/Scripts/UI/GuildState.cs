using System;
using ArcaneShared.Models;
using Arcane_Aegis.Network;

namespace Arcane_Aegis.UI
{
    /// <summary>Client-side cache of the local player's guild (from S2C_GuildRoster). GuildId 0 = not in a guild. Raises
    /// <see cref="OnChanged"/> so the guild panel refreshes live. Rank/leader checks use <see cref="ClientSession.CharacterId"/>.</summary>
    public static class GuildState
    {
        private static GuildMemberEntry[] _members = Array.Empty<GuildMemberEntry>();

        public static event Action OnChanged;

        public static uint GuildId { get; private set; }
        public static string GuildName { get; private set; } = string.Empty;
        public static uint LeaderCharacterId { get; private set; }
        public static GuildMemberEntry[] Members => _members;
        public static bool InGuild => GuildId != 0;
        public static uint MyCharacterId => ClientSession.CharacterId;
        public static bool AmLeader => InGuild && LeaderCharacterId == ClientSession.CharacterId;

        public static byte MyRank
        {
            get
            {
                for (int i = 0; i < _members.Length; i++) if (_members[i].CharacterId == ClientSession.CharacterId) return _members[i].Rank;
                return 0;
            }
        }

        public static void Set(uint guildId, string name, uint leader, GuildMemberEntry[] members)
        {
            GuildId = guildId;
            GuildName = name ?? string.Empty;
            LeaderCharacterId = leader;
            _members = members ?? Array.Empty<GuildMemberEntry>();
            OnChanged?.Invoke();
        }
    }
}
