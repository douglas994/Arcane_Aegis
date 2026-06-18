using NetworkLibrary.Serialization;
using ArcaneShared.Enums;
using ArcaneShared.Protocol.ServerToClient;
using Arcane_Aegis.UI;

namespace Arcane_Aegis.Network.Handlers
{
    /// <summary>S2C_PartyChat: a party message → append a formatted line to the chat log.</summary>
    public sealed class PartyChatHandler : IClientPacketHandler
    {
        public PacketId PacketId => PacketId.S2C_PartyChat;

        public void Handle(ref BitBuffer reader)
        {
            var p = new S2C_PartyChat();
            p.Deserialize(ref reader);
            ChatLog.AddLine($"<color=#7FB3FF>[Grupo]</color> {p.SenderName}: {p.Text}");
        }
    }
}
