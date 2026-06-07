using NetworkLibrary.Serialization;
using ArcaneShared.Enums;
using ArcaneShared.Protocol.ServerToClient;
using Arcane_Aegis.Entities;

namespace Arcane_Aegis.Network.Handlers
{
    /// <summary>S2C_AbilityCast: our own cast → set the authoritative cooldown; a remote cast → play its anim.</summary>
    public sealed class AbilityCastHandler : IClientPacketHandler
    {
        private readonly EntityManager _entities;
        public AbilityCastHandler(EntityManager entities) => _entities = entities;

        public PacketId PacketId => PacketId.S2C_AbilityCast;

        public void Handle(ref BitBuffer reader)
        {
            var p = new S2C_AbilityCast();
            p.Deserialize(ref reader);

            var local = _entities.Local;
            if (local != null && p.CasterId == local.Id)
            {
                // own cast confirmed → authoritative cooldown (we already predicted the anim locally)
                if (local.Combat != null) local.Combat.OnServerCooldown(p.AbilityId, p.CooldownMs / 1000f);
            }
            else _entities.PlayAttack(p.CasterId);
        }
    }
}
