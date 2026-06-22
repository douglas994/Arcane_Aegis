using NetworkLibrary.Serialization;
using ArcaneShared.Enums;
using ArcaneShared.Protocol.ServerToClient;
using Arcane_Aegis.Entities;
using Arcane_Aegis.UI;

namespace Arcane_Aegis.Network.Handlers
{
    /// <summary>S2C_StatusEffects (owner-only): the player's full active buff/debuff set → mirror into the status icon bar.</summary>
    public sealed class StatusEffectsHandler : IClientPacketHandler
    {
        private readonly EntityManager _entities;
        public StatusEffectsHandler(EntityManager entities) => _entities = entities;

        public PacketId PacketId => PacketId.S2C_StatusEffects;

        public void Handle(ref BitBuffer reader)
        {
            var p = new S2C_StatusEffects();
            p.Deserialize(ref reader);

            var local = _entities.Local;
            if (local != null && p.EntityId == local.Id && StatusBar.Instance != null)
                StatusBar.Instance.Set(p.Effects);
        }
    }
}
