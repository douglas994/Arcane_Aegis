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
            Debug.Log($"[NetClient] login {(p.Success ? "OK" : "FAIL")} — my id = {p.YourEntityId}");
            if (!p.Success || _spawned) return;

            _spawned = true;
            var sp = new Vector3(p.SpawnPosition.X, p.SpawnPosition.Y, p.SpawnPosition.Z);
            _entities.SpawnLocal(p.YourEntityId, _username, sp, p.RaceId, p.ClassId, p.GenderId); // sets EntityManager.Local
        }
    }
}
