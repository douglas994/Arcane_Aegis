using UnityEngine;
using NetworkLibrary.Serialization;
using ArcaneShared.Enums;
using ArcaneShared.Protocol.ServerToClient;
using Arcane_Aegis.Entities;

namespace Arcane_Aegis.Network.Handlers
{
    /// <summary>S2C_StateUpdate (own player only): exact vitals → the local view; + an optional position correction.</summary>
    public sealed class StateUpdateHandler : IClientPacketHandler
    {
        private readonly EntityManager _entities;
        public StateUpdateHandler(EntityManager entities) => _entities = entities;

        public PacketId PacketId => PacketId.S2C_StateUpdate;

        public void Handle(ref BitBuffer reader)
        {
            var p = new S2C_StateUpdate();
            p.Deserialize(ref reader);

            var local = _entities.Local;
            if (local == null) return;

            local.SetVitals(p.Hp, p.MaxHp, p.Mana, p.MaxMana);

            if (p.HasCorrection && local.Motor != null)
            {
                var cp = new Vector3(p.CorrectedPosition.X, p.CorrectedPosition.Y, p.CorrectedPosition.Z);
                local.Motor.SetPosition(cp);
                Debug.Log($"[NetClient] position corrected by server → {cp}");
            }
        }
    }
}
