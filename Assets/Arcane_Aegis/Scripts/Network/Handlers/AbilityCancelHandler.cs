using NetworkLibrary.Serialization;
using ArcaneShared.Enums;
using ArcaneShared.Protocol.ServerToClient;
using Arcane_Aegis.Combat;
using Arcane_Aegis.Entities;
using Arcane_Aegis.UI;

namespace Arcane_Aegis.Network.Handlers
{
    /// <summary>S2C_AbilityCancel: a caster's wind-up was interrupted (took damage) → kill its telegraph, and if it's
    /// our own cast, hide the HUD cast bar. Purely cosmetic; the server already decided the cast won't resolve.</summary>
    public sealed class AbilityCancelHandler : IClientPacketHandler
    {
        private readonly EntityManager _entities;
        public AbilityCancelHandler(EntityManager entities) => _entities = entities;

        public PacketId PacketId => PacketId.S2C_AbilityCancel;

        public void Handle(ref BitBuffer reader)
        {
            var p = new S2C_AbilityCancel();
            p.Deserialize(ref reader);

            if (TelegraphManager.Instance != null) TelegraphManager.Instance.Despawn(p.CasterId);

            var local = _entities.Local;
            if (local != null && p.CasterId == local.Id && CastBar.Instance != null) CastBar.Instance.Cancel();
        }
    }
}
