using NetworkLibrary.Serialization;
using ArcaneShared.Enums;
using ArcaneShared.Protocol.ServerToClient;
using Arcane_Aegis.UI;

namespace Arcane_Aegis.Network.Handlers
{
    /// <summary>S2C_GuildInvite: someone invited us to a guild → raise the invite prompt.</summary>
    public sealed class GuildInviteHandler : IClientPacketHandler
    {
        public PacketId PacketId => PacketId.S2C_GuildInvite;

        public void Handle(ref BitBuffer reader)
        {
            var p = new S2C_GuildInvite();
            p.Deserialize(ref reader);
            GuildInviteState.Show(p.InviterName, p.GuildName);
        }
    }
}
