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

            // Resolve the gatherer's view INCLUDING the local player — the local player is NOT in _views (only Local),
            // so a plain TryGetView misses it and the local gather anim never plays (remotes worked, you didn't).
            var local = _entities.Local;
            EntityView gatherer = (local != null && p.GathererId == local.Id) ? local
                                : _entities.TryGetView(p.GathererId, out var v) ? v : null;
            if (gatherer != null) gatherer.PlayGather(p.Profession, p.DurationMs / 1000f);

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
