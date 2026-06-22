using NetworkLibrary.Serialization;
using ArcaneShared.Enums;
using ArcaneShared.Protocol.ServerToClient;
using Arcane_Aegis.UI;

namespace Arcane_Aegis.Network.Handlers
{
    /// <summary>S2C_OwnedCompanions: the player's owned pets+mounts → cache for the collection panel.</summary>
    public sealed class OwnedCompanionsHandler : IClientPacketHandler
    {
        public PacketId PacketId => PacketId.S2C_OwnedCompanions;

        public void Handle(ref BitBuffer reader)
        {
            var p = new S2C_OwnedCompanions();
            p.Deserialize(ref reader);
            CompanionState.Set(p.Companions);
        }
    }
}
