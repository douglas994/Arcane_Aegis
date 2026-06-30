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
            ClientSession.IsAdmin = p.IsAdmin; // server-authoritative admin flag → the GM panel uses it
            ClientSession.ClassId = p.ClassId ?? string.Empty; // local class → data-driven action bar (skills per class)
            ClientSession.RaceId = p.RaceId ?? string.Empty;
            var sp = new Vector3(p.SpawnPosition.X, p.SpawnPosition.Y, p.SpawnPosition.Z);
            var offset = new Vector3(p.ZoneOffsetX, 0f, p.ZoneOffsetZ);

            // Entering/leaving an instanced dungeon (DungeonDef changed) = a SCENE swap: load the dungeon scene (or back to
            // the open world) and spawn there once it's ready. NetClient buffers the packets that follow until then.
            if (p.DungeonDef != ClientSession.CurrentDungeonDef)
            {
                ClientSession.CurrentDungeonDef = p.DungeonDef;
                // Data-driven: each dungeon picks its own scene via a DungeonDefinitionSO (so the game can have many).
                // Fall back to a generic "Dungeon" scene if none is registered, and "World" for the open world (def 0).
                string scene = "World";
                if (p.DungeonDef > 0)
                {
                    var def = Content.ContentLibrary.Active != null ? Content.ContentLibrary.Active.GetDungeon(p.DungeonDef) : null;
                    scene = def != null && !string.IsNullOrEmpty(def.sceneName) ? def.sceneName : "Dungeon";
                }
                Debug.Log($"[NetClient] dungeon transition (def {p.DungeonDef}) → load scene '{scene}'");
                NetClient.Instance.SwitchSceneAndSpawn(scene, p.YourEntityId, _username, sp, p.RaceId, p.ClassId, p.GenderId, offset);
                return;
            }

            if (_entities != null && _entities.Local != null)
            {
                // Already in-world, SAME scene → a ZONE CHANGE (continent border handoff): re-home, don't re-create.
                Debug.Log($"[NetClient] zone change → id {p.YourEntityId} @ local ({sp.x:0},{sp.z:0}) offset ({offset.x:0},{offset.z:0})");
                _entities.RespawnLocal(p.YourEntityId, sp, offset);
                return;
            }

            _entities.SpawnLocal(p.YourEntityId, _username, sp, p.RaceId, p.ClassId, p.GenderId, offset); // first spawn → sets EntityManager.Local
        }
    }
}
