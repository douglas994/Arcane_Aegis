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

            // MY OWN projectile: spawn it from my LIVE transform (no ~66ms spawn lag), so it leaves the muzzle aligned
            // with where I'm actually facing/moving — not the stale position the server knew. The server is still
            // authoritative on the hit (a despawn arrives). Remote casters use the server's start/dir as-is.
            var local = _entities.Local;
            if (local != null && p.CasterId == local.Id)
            {
                // My own projectile: fly along the SERVER's direction (already the camera-aim — the server faced me toward
                // the aim I sent), but spawn from my LIVE muzzle (the standard CastOrigin + skill offset) so it leaves my hands.
                Vector3 dir = new Vector3(p.DirX, 0f, p.DirZ);
                dir = dir.sqrMagnitude > 1e-4f ? dir.normalized : local.transform.forward;
                Vector3 muzzle = SkillOrigin.ResolveFor(local.transform, p.AbilityId);
                ProjectileManager.Instance.Spawn(p.ProjId, p.AbilityId, muzzle, dir, p.Speed, p.Range);
                return;
            }

            Vector3 start = new Vector3(p.Start.X, p.Start.Y, p.Start.Z) + _entities.ZoneOffset;
            ProjectileManager.Instance.Spawn(p.ProjId, p.AbilityId, start, new Vector3(p.DirX, 0f, p.DirZ), p.Speed, p.Range);
        }
    }
}
