using NetworkLibrary.Serialization;
using ArcaneShared.Enums;
using ArcaneShared.Protocol.ServerToClient;
using Arcane_Aegis.Combat;

namespace Arcane_Aegis.Network.Handlers
{
    /// <summary>S2C_ProjectileDespawn: a projectile ended → remove its visual (+ impact VFX on a hit).</summary>
    public sealed class ProjectileDespawnHandler : IClientPacketHandler
    {
        public PacketId PacketId => PacketId.S2C_ProjectileDespawn;

        public void Handle(ref BitBuffer reader)
        {
            var p = new S2C_ProjectileDespawn();
            p.Deserialize(ref reader);
            if (ProjectileManager.Instance != null) ProjectileManager.Instance.Despawn(p.ProjId, p.Hit != 0);
        }
    }
}
