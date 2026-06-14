using NetworkLibrary.Serialization;
using ArcaneShared.Enums;
using ArcaneShared.Protocol.ServerToClient;
using Arcane_Aegis.Entities;
using Arcane_Aegis.UI;

namespace Arcane_Aegis.Network.Handlers
{
    /// <summary>S2C_GatherStart: someone began harvesting → play the gather anim on them; the local gatherer also gets
    /// the progress bar + movement lock for the duration.</summary>
    public sealed class GatherStartHandler : IClientPacketHandler
    {
        private readonly EntityManager _entities;
        public GatherStartHandler(EntityManager entities) => _entities = entities;

        public PacketId PacketId => PacketId.S2C_GatherStart;

        public void Handle(ref BitBuffer reader)
        {
            var p = new S2C_GatherStart();
            p.Deserialize(ref reader);

            if (_entities.TryGetView(p.GathererId, out var view)) view.PlayGather(p.Profession, p.DurationMs / 1000f);

            var local = _entities.Local;
            if (local != null && p.GathererId == local.Id)
            {
                if (local.Locomotion != null && _entities.TryGetView(p.NodeId, out var nodeView))
                    local.Locomotion.FaceWorldPoint(nodeView.transform.position); // encara o nó
                if (GatherProgress.Instance != null)
                    GatherProgress.Instance.Begin(p.DurationMs / 1000f, local.Locomotion);
            }
        }
    }
}
