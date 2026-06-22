using NetworkLibrary.Serialization;
using ArcaneShared.Enums;
using ArcaneShared.Protocol.ServerToClient;
using Arcane_Aegis.Entities;
using Arcane_Aegis.Controllers;

namespace Arcane_Aegis.Network.Handlers
{
    /// <summary>S2C_MountState (owner-only): the local player got on / off a mount → hand it to <see cref="MountSession"/>,
    /// which spawns the controllable rig (own KCC + MountController), seats the player on it, and re-points the camera.
    /// Empty MountDefId = dismount. Remotes are NOT driven by this packet — they see the mount as a spawned entity.</summary>
    public sealed class MountStateHandler : IClientPacketHandler
    {
        private readonly EntityManager _entities;
        public MountStateHandler(EntityManager entities) => _entities = entities;

        public PacketId PacketId => PacketId.S2C_MountState;

        public void Handle(ref BitBuffer reader)
        {
            var p = new S2C_MountState();
            p.Deserialize(ref reader);

            var local = _entities.Local;
            if (local == null || p.EntityId != local.Id) return; // owner-only packet

            var session = MountSession.Instance;
            if (session == null) return;

            if (string.IsNullOrEmpty(p.MountDefId)) session.Dismount();
            else session.Mount(p.MountDefId);
        }
    }
}
