using NetworkLibrary.Serialization;
using ArcaneShared.Enums;
using ArcaneShared.Protocol.ServerToClient;
using Arcane_Aegis.UI;

namespace Arcane_Aegis.Network.Handlers
{
    /// <summary>S2C_FriendList: the full friend list (with live online/zone) → cache it for the friends panel.</summary>
    public sealed class FriendListHandler : IClientPacketHandler
    {
        public PacketId PacketId => PacketId.S2C_FriendList;

        public void Handle(ref BitBuffer reader)
        {
            var p = new S2C_FriendList();
            p.Deserialize(ref reader);
            FriendState.Set(p.Friends);
        }
    }
}
