using NetworkLibrary.Serialization;
using ArcaneShared.Enums;
using ArcaneShared.Protocol.ServerToClient;
using Arcane_Aegis.Entities;
using Arcane_Aegis.UI;

namespace Arcane_Aegis.Network.Handlers
{
    /// <summary>S2C_ControlState (owner-only): the server's authoritative CC state → lock the local input (stun/root)
    /// and scale speed (slow), so the player stops moving instead of being snapped back (no rubber-band).</summary>
    public sealed class ControlStateHandler : IClientPacketHandler
    {
        private readonly EntityManager _entities;
        private bool _wasCC;
        public ControlStateHandler(EntityManager entities) => _entities = entities;

        public PacketId PacketId => PacketId.S2C_ControlState;

        public void Handle(ref BitBuffer reader)
        {
            var p = new S2C_ControlState();
            p.Deserialize(ref reader);

            var local = _entities.Local;
            if (local == null) return;
            if (local.Locomotion != null) local.Locomotion.SetControl(p.Stunned, p.Rooted, p.MoveSpeedMult);
            if (local.Combat != null) local.Combat.SetStunned(p.Stunned);

            bool cc = p.Stunned || p.Rooted;
            if (cc && !_wasCC && Toast.Instance != null) Toast.Instance.Show(p.Stunned ? "Atordoado!" : "Enraizado!");
            _wasCC = cc;
        }
    }
}
