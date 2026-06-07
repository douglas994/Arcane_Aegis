using NetworkLibrary.Serialization;
using ArcaneShared.Enums;
using ArcaneShared.Models;
using ArcaneShared.Protocol;
using Arcane_Aegis.Entities;

namespace Arcane_Aegis.Network.Handlers
{
    /// <summary>S2C_Snapshot: a batch of entity states for this tick → apply each to its view (interpolated).</summary>
    public sealed class SnapshotHandler : IClientPacketHandler
    {
        private readonly EntityManager _entities;
        public SnapshotHandler(EntityManager entities) => _entities = entities;

        public PacketId PacketId => PacketId.S2C_Snapshot;

        public void Handle(ref BitBuffer reader)
        {
            SnapshotPacket.ReadHeader(ref reader, out int count);
            for (int i = 0; i < count; i++)
                _entities.ApplySnapshot(SnapshotEntry.Read(ref reader));
        }
    }
}
