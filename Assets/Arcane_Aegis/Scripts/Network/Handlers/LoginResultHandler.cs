using UnityEngine;
using NetworkLibrary.Serialization;
using ArcaneShared.Enums;
using ArcaneShared.Protocol.ServerToClient;
using Arcane_Aegis.Entities;

namespace Arcane_Aegis.Network.Handlers
{
    /// <summary>S2C_LoginResult: spawns the local player at the server spawn (EntityManager records it as Local).</summary>
    public sealed class LoginResultHandler : IClientPacketHandler
    {
        private readonly EntityManager _entities;
        private readonly string _username;
        private bool _spawned;

        public LoginResultHandler(EntityManager entities, string username)
        {
            _entities = entities;
            _username = username;
        }

        public PacketId PacketId => PacketId.S2C_LoginResult;

        public void Handle(ref BitBuffer reader)
        {
            var p = new S2C_LoginResult();
            p.Deserialize(ref reader);
            if (!p.Success) return;
            var sp = new Vector3(p.SpawnPosition.X, p.SpawnPosition.Y, p.SpawnPosition.Z);
            var offset = new Vector3(p.ZoneOffsetX, 0f, p.ZoneOffsetZ);

            if (_spawned)
            {
                // Already in-world → this LoginResult is a ZONE CHANGE (border handoff): re-home, don't re-create.
                Debug.Log($"[NetClient] zone change → id {p.YourEntityId} @ local ({sp.x:0},{sp.z:0}) offset ({offset.x:0},{offset.z:0})");
                _entities.RespawnLocal(p.YourEntityId, sp, offset);
                return;
            }

            _spawned = true;
            _entities.SpawnLocal(p.YourEntityId, _username, sp, p.RaceId, p.ClassId, p.GenderId, offset); // sets EntityManager.Local
        }
    }
}
