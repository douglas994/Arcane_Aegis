using NetworkLibrary.Serialization;
using ArcaneShared.Enums;
using ArcaneShared.Protocol.ServerToClient;
using Arcane_Aegis.Entities;

namespace Arcane_Aegis.Network.Handlers
{
    /// <summary>S2C_SpawnEntity: a remote entity entered our AoI → create its view.</summary>
    public sealed class SpawnEntityHandler : IClientPacketHandler
    {
        private readonly EntityManager _entities;
        public SpawnEntityHandler(EntityManager entities) => _entities = entities;

        public PacketId PacketId => PacketId.S2C_SpawnEntity;

        public void Handle(ref BitBuffer reader)
        {
            var p = new S2C_SpawnEntity();
            p.Deserialize(ref reader);
            _entities.SpawnRemote(p);
        }
    }
}
