using NetworkLibrary.Serialization;
using ArcaneShared.Enums;
using ArcaneShared.Protocol.ServerToClient;
using Arcane_Aegis.Entities;

namespace Arcane_Aegis.Network.Handlers
{
    /// <summary>S2C_DespawnEntity: a remote entity left our AoI → destroy its view.</summary>
    public sealed class DespawnEntityHandler : IClientPacketHandler
    {
        private readonly EntityManager _entities;
        public DespawnEntityHandler(EntityManager entities) => _entities = entities;

        public PacketId PacketId => PacketId.S2C_DespawnEntity;

        public void Handle(ref BitBuffer reader)
        {
            var p = new S2C_DespawnEntity();
            p.Deserialize(ref reader);
            _entities.Despawn(p.EntityId);
        }
    }
}
