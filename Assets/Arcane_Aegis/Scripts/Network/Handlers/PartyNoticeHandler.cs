using NetworkLibrary.Serialization;
using ArcaneShared.Enums;
using ArcaneShared.Protocol.ServerToClient;
using Arcane_Aegis.UI;

namespace Arcane_Aegis.Network.Handlers
{
    /// <summary>S2C_PartyNotice: a short party result/info code → pt-BR toast.</summary>
    public sealed class PartyNoticeHandler : IClientPacketHandler
    {
        public PacketId PacketId => PacketId.S2C_PartyNotice;

        public void Handle(ref BitBuffer reader)
        {
            var p = new S2C_PartyNotice();
            p.Deserialize(ref reader);
            string text = Text((PartyNoticeCode)p.Code);
            if (!string.IsNullOrEmpty(text) && Toast.Instance != null) Toast.Instance.Show(text);
        }

        private static string Text(PartyNoticeCode code) => code switch
        {
            PartyNoticeCode.PlayerNotFound   => "Jogador não encontrado (precisa estar online).",
            PartyNoticeCode.AlreadyInParty   => "Esse jogador já está em um grupo.",
            PartyNoticeCode.PartyFull        => "O grupo está cheio.",
            PartyNoticeCode.InviteSent       => "Convite enviado.",
            PartyNoticeCode.InviteDeclined   => "Seu convite foi recusado.",
            PartyNoticeCode.NotLeader        => "Só o líder pode fazer isso.",
            PartyNoticeCode.CannotInviteSelf => "Você não pode se convidar.",
            PartyNoticeCode.YouLeftParty     => "Você saiu do grupo.",
            PartyNoticeCode.YouWereKicked    => "Você foi removido do grupo.",
            PartyNoticeCode.PartyDisbanded   => "O grupo foi desfeito.",
            PartyNoticeCode.AlreadyInvited   => "Esse jogador já tem um convite pendente.",
            _ => "",
        };
    }
}
