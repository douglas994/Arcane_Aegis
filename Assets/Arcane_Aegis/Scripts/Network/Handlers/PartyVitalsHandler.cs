using NetworkLibrary.Serialization;
using ArcaneShared.Enums;
using ArcaneShared.Protocol.ServerToClient;
using Arcane_Aegis.UI;

namespace Arcane_Aegis.Network.Handlers
{
    /// <summary>S2C_PartyVitals: a party member's live HP% changed → patch just their bar.</summary>
    public sealed class PartyVitalsHandler : IClientPacketHandler
    {
        public PacketId PacketId => PacketId.S2C_PartyVitals;

        public void Handle(ref BitBuffer reader)
        {
            var p = new S2C_PartyVitals();
            p.Deserialize(ref reader);
            PartyState.SetVitals(p.CharacterId, p.HpPercent);
        }
    }
}
