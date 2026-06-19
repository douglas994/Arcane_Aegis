using NetworkLibrary.Serialization;
using ArcaneShared.Enums;
using ArcaneShared.Protocol.ServerToClient;
using Arcane_Aegis.UI;

namespace Arcane_Aegis.Network.Handlers
{
    /// <summary>S2C_FriendRequest: someone wants to add us → raise the request prompt.</summary>
    public sealed class FriendRequestHandler : IClientPacketHandler
    {
        public PacketId PacketId => PacketId.S2C_FriendRequest;

        public void Handle(ref BitBuffer reader)
        {
            var p = new S2C_FriendRequest();
            p.Deserialize(ref reader);
            FriendRequestState.Show(p.RequesterName);
        }
    }
}
