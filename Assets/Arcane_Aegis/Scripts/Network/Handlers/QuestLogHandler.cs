using NetworkLibrary.Serialization;
using ArcaneShared.Enums;
using ArcaneShared.Protocol.ServerToClient;
using Arcane_Aegis.UI;

namespace Arcane_Aegis.Network.Handlers
{
    /// <summary>S2C_QuestLog (owner-only): the player's full quest log → cache it for the quest panel + log HUD.</summary>
    public sealed class QuestLogHandler : IClientPacketHandler
    {
        public PacketId PacketId => PacketId.S2C_QuestLog;

        public void Handle(ref BitBuffer reader)
        {
            var p = new S2C_QuestLog();
            p.Deserialize(ref reader);
            QuestState.Set(p.Quests);
        }
    }
}
