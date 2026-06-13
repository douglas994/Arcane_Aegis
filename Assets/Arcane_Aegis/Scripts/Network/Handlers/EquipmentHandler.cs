using NetworkLibrary.Serialization;
using ArcaneShared.Enums;
using ArcaneShared.Protocol.ServerToClient;
using Arcane_Aegis.Entities;

namespace Arcane_Aegis.Network.Handlers
{
    /// <summary>S2C_Equipment: a (remote) entity's main-hand changed → update the replicated id so its WeaponVisual
    /// swaps the 3D model. The local player drives its own weapon from the inventory, so this mainly serves remotes.</summary>
    public sealed class EquipmentHandler : IClientPacketHandler
    {
        private readonly EntityManager _entities;
        public EquipmentHandler(EntityManager entities) => _entities = entities;

        public PacketId PacketId => PacketId.S2C_Equipment;

        public void Handle(ref BitBuffer reader)
        {
            var p = new S2C_Equipment();
            p.Deserialize(ref reader);
            if (_entities.TryGetView(p.EntityId, out var view)) view.EquippedMainHand = p.MainHandItemId ?? "";
        }
    }
}
