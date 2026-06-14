using NetworkLibrary.Serialization;
using ArcaneShared.Enums;
using ArcaneShared.Protocol.ServerToClient;
using Arcane_Aegis.UI;

namespace Arcane_Aegis.Network.Handlers
{
    /// <summary>S2C_ProfessionXp (owner-only): cache the mastery for the HUD + toast on level-up.</summary>
    public sealed class ProfessionXpHandler : IClientPacketHandler
    {
        public PacketId PacketId => PacketId.S2C_ProfessionXp;

        public void Handle(ref BitBuffer reader)
        {
            var p = new S2C_ProfessionXp();
            p.Deserialize(ref reader);
            ProfessionState.Set(p.Profession, p.Level, p.Xp, p.XpToNext);
            if (p.LeveledUp != 0 && Toast.Instance != null)
                Toast.Instance.Show($"{ProfessionState.Name(p.Profession)} nível {p.Level}!");
        }
    }
}
