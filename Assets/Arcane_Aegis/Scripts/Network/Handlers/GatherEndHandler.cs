using NetworkLibrary.Serialization;
using ArcaneShared.Enums;
using ArcaneShared.Protocol.ServerToClient;
using Arcane_Aegis.Entities;
using Arcane_Aegis.UI;

namespace Arcane_Aegis.Network.Handlers
{
    /// <summary>S2C_GatherEnd: the server ended a gather (done / interrupted / cancelled) → stop the gather anim on the
    /// gatherer; the local gatherer also closes the progress bar + unlocks movement. Authoritative, so no client timer.</summary>
    public sealed class GatherEndHandler : IClientPacketHandler
    {
        private readonly EntityManager _entities;
        public GatherEndHandler(EntityManager entities) => _entities = entities;

        public PacketId PacketId => PacketId.S2C_GatherEnd;

        public void Handle(ref BitBuffer reader)
        {
            var p = new S2C_GatherEnd();
            p.Deserialize(ref reader);

            if (_entities.TryGetView(p.GathererId, out var view)) view.StopGather();

            var local = _entities.Local;
            if (local != null && p.GathererId == local.Id && GatherProgress.Instance != null)
            {
                GatherProgress.Instance.End();
                if (p.Reason == 1 && Toast.Instance != null) Toast.Instance.Show("Coleta interrompida!");
            }
        }
    }
}
