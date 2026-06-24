using NetworkLibrary.Serialization;
using ArcaneShared.Enums;
using ArcaneShared.Protocol.ServerToClient;
using Arcane_Aegis.UI;

namespace Arcane_Aegis.Network.Handlers
{
    /// <summary>S2C_PetState (owner-only): the active pet's identity + level/xp → the pet HUD cache. EntityId 0 hides it.</summary>
    public sealed class PetStateHandler : IClientPacketHandler
    {
        public PacketId PacketId => PacketId.S2C_PetState;

        public void Handle(ref BitBuffer reader)
        {
            var p = new S2C_PetState();
            p.Deserialize(ref reader);
            PetState.Set(p.EntityId, p.Name, p.Level, p.Xp, p.XpToNext, p.LeveledUp != 0);
        }
    }
}
