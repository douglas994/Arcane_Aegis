using UnityEngine;
using NetworkLibrary.Serialization;
using ArcaneShared.Enums;
using ArcaneShared.Protocol.ServerToClient;
using Arcane_Aegis.Entities;
using Arcane_Aegis.Combat;

namespace Arcane_Aegis.Network.Handlers
{
    /// <summary>S2C_ProjectileSpawn: a skillshot was launched → fly its visual (server coords → global via ZoneOffset).</summary>
    public sealed class ProjectileSpawnHandler : IClientPacketHandler
    {
        private readonly EntityManager _entities;
        public ProjectileSpawnHandler(EntityManager entities) => _entities = entities;

        public PacketId PacketId => PacketId.S2C_ProjectileSpawn;

        public void Handle(ref BitBuffer reader)
        {
            var p = new S2C_ProjectileSpawn();
            p.Deserialize(ref reader);
            if (ProjectileManager.Instance == null) return;
            Vector3 start = new Vector3(p.Start.X, p.Start.Y, p.Start.Z) + _entities.ZoneOffset;
            ProjectileManager.Instance.Spawn(p.ProjId, p.AbilityId, start, new Vector3(p.DirX, 0f, p.DirZ), p.Speed, p.Range);
        }
    }
}
