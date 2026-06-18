using NetworkLibrary.Serialization;
using ArcaneShared.Enums;
using ArcaneShared.Protocol.ServerToClient;
using Arcane_Aegis.UI;

namespace Arcane_Aegis.Network.Handlers
{
    /// <summary>S2C_PartyInvite: someone invited us → raise the invite prompt.</summary>
    public sealed class PartyInviteHandler : IClientPacketHandler
    {
        public PacketId PacketId => PacketId.S2C_PartyInvite;

        public void Handle(ref BitBuffer reader)
        {
            var p = new S2C_PartyInvite();
            p.Deserialize(ref reader);
            PartyInviteState.Show(p.InviterName);
        }
    }
}
