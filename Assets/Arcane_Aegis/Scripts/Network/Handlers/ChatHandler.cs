using NetworkLibrary.Serialization;
using ArcaneShared.Enums;
using ArcaneShared.Protocol.ServerToClient;
using Arcane_Aegis.UI;

namespace Arcane_Aegis.Network.Handlers
{
    /// <summary>S2C_Chat: a chat message on a channel → append a formatted (colored) line to the chat log.</summary>
    public sealed class ChatHandler : IClientPacketHandler
    {
        public PacketId PacketId => PacketId.S2C_Chat;

        public void Handle(ref BitBuffer reader)
        {
            var p = new S2C_Chat();
            p.Deserialize(ref reader);
            ChatLog.AddLine(Format((ChatChannel)p.Channel, p.Outgoing, p.SenderName, p.Text));
        }

        private static string Format(ChatChannel ch, bool outgoing, string sender, string text) => ch switch
        {
            ChatChannel.Global  => $"<color=#DDDDDD>[Global]</color> {sender}: {text}",
            ChatChannel.Zone    => $"<color=#9AD0A0>[Zona]</color> {sender}: {text}",
            ChatChannel.Party   => $"<color=#7FB3FF>[Grupo]</color> {sender}: {text}",
            ChatChannel.Guild   => $"<color=#7CE0C0>[Guilda]</color> {sender}: {text}",
            ChatChannel.Whisper => outgoing
                ? $"<color=#D98BE0>[Para {sender}]</color> {text}"
                : $"<color=#D98BE0>[De {sender}]</color> {text}",
            _ => $"{sender}: {text}",
        };
    }
}
