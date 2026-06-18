using NetworkLibrary.Serialization;
using ArcaneShared.Enums;
using ArcaneShared.Protocol.ServerToClient;
using Arcane_Aegis.UI;

namespace Arcane_Aegis.Network.Handlers
{
    /// <summary>S2C_PartyState: the full party roster (empty = not in a party) → cache it for the party panel.</summary>
    public sealed class PartyStateHandler : IClientPacketHandler
    {
        public PacketId PacketId => PacketId.S2C_PartyState;

        public void Handle(ref BitBuffer reader)
        {
            var p = new S2C_PartyState();
            p.Deserialize(ref reader);
            PartyState.Set(p.LeaderCharacterId, p.Members);
        }
    }
}
