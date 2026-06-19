using NetworkLibrary.Serialization;
using ArcaneShared.Enums;
using ArcaneShared.Protocol.ServerToClient;
using Arcane_Aegis.UI;

namespace Arcane_Aegis.Network.Handlers
{
    /// <summary>S2C_GuildRoster: the guild roster (empty/GuildId 0 = not in a guild) → cache it for the guild panel.</summary>
    public sealed class GuildRosterHandler : IClientPacketHandler
    {
        public PacketId PacketId => PacketId.S2C_GuildRoster;

        public void Handle(ref BitBuffer reader)
        {
            var p = new S2C_GuildRoster();
            p.Deserialize(ref reader);
            GuildState.Set(p.GuildId, p.Name, p.LeaderCharacterId, p.Members);
        }
    }
}
