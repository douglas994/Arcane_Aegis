using NetworkLibrary.Serialization;
using ArcaneShared.Enums;
using ArcaneShared.Protocol.ServerToClient;
using Arcane_Aegis.UI;

namespace Arcane_Aegis.Network.Handlers
{
    /// <summary>S2C_CraftStart (owner-only): a craft began → show the progress bar for the server-sent duration.</summary>
    public sealed class CraftStartHandler : IClientPacketHandler
    {
        public PacketId PacketId => PacketId.S2C_CraftStart;

        public void Handle(ref BitBuffer reader)
        {
            var p = new S2C_CraftStart();
            p.Deserialize(ref reader);
            if (CraftProgress.Instance != null) CraftProgress.Instance.Begin(p.DurationMs / 1000f);
        }
    }
}
