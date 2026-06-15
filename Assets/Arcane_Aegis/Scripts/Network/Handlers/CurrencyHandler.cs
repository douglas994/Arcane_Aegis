using NetworkLibrary.Serialization;
using ArcaneShared.Enums;
using ArcaneShared.Protocol.ServerToClient;
using Arcane_Aegis.UI;

namespace Arcane_Aegis.Network.Handlers
{
    /// <summary>S2C_Currency (owner-only): the player's full wallet → cache it for the HUD.</summary>
    public sealed class CurrencyHandler : IClientPacketHandler
    {
        public PacketId PacketId => PacketId.S2C_Currency;

        public void Handle(ref BitBuffer reader)
        {
            var p = new S2C_Currency();
            p.Deserialize(ref reader);
            var list = new (string, long)[p.Wallet?.Length ?? 0];
            for (int i = 0; i < list.Length; i++) list[i] = (p.Wallet[i].CurrencyId, p.Wallet[i].Amount);
            CurrencyState.SetAll(list);
        }
    }
}
