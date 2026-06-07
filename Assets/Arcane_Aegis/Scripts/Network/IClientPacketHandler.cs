using NetworkLibrary.Serialization;
using ArcaneShared.Enums;

namespace Arcane_Aegis.Network
{
    /// <summary>
    /// Handles one S2C packet type. Mirrors the server's IPacketHandler (minus the peer — the client has a
    /// single server). The router reads the PacketId, then calls Handle to deserialize + act on the payload.
    /// </summary>
    public interface IClientPacketHandler
    {
        PacketId PacketId { get; }
        void Handle(ref BitBuffer reader);
    }
}
