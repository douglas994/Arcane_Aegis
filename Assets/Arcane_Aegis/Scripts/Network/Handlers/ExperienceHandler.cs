using NetworkLibrary.Serialization;
using ArcaneShared.Enums;
using ArcaneShared.Protocol.ServerToClient;
using Arcane_Aegis.Entities;
using Arcane_Aegis.UI;

namespace Arcane_Aegis.Network.Handlers
{
    /// <summary>S2C_Experience (owner-only): XP/level → the local view (drives the HUD XP bar; toast on level-up).</summary>
    public sealed class ExperienceHandler : IClientPacketHandler
    {
        private readonly EntityManager _entities;
        public ExperienceHandler(EntityManager entities) => _entities = entities;

        public PacketId PacketId => PacketId.S2C_Experience;

        public void Handle(ref BitBuffer reader)
        {
            var p = new S2C_Experience();
            p.Deserialize(ref reader);

            var local = _entities.Local;
            if (local == null) return;
            local.SetExperience(p.Level, p.Xp, p.XpToNext);
            if (p.LeveledUp != 0 && Toast.Instance != null) Toast.Instance.Show($"Subiu para o nível {p.Level}!");
        }
    }
}
