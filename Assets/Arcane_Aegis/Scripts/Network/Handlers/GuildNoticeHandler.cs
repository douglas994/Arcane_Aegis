using NetworkLibrary.Serialization;
using ArcaneShared.Enums;
using ArcaneShared.Protocol.ServerToClient;
using Arcane_Aegis.UI;

namespace Arcane_Aegis.Network.Handlers
{
    /// <summary>S2C_GuildNotice: a short guild result/info code → pt-BR toast.</summary>
    public sealed class GuildNoticeHandler : IClientPacketHandler
    {
        public PacketId PacketId => PacketId.S2C_GuildNotice;

        public void Handle(ref BitBuffer reader)
        {
            var p = new S2C_GuildNotice();
            p.Deserialize(ref reader);
            string text = Text((GuildNoticeCode)p.Code);
            if (!string.IsNullOrEmpty(text) && Toast.Instance != null) Toast.Instance.Show(text);
        }

        private static string Text(GuildNoticeCode code) => code switch
        {
            GuildNoticeCode.NameTaken        => "Já existe uma guilda com esse nome.",
            GuildNoticeCode.PlayerNotFound   => "Jogador não encontrado (precisa estar online).",
            GuildNoticeCode.AlreadyInGuild   => "Já está em uma guilda.",
            GuildNoticeCode.NotInGuild       => "Você não está em uma guilda.",
            GuildNoticeCode.AlreadyInvited   => "Esse jogador já tem um convite pendente.",
            GuildNoticeCode.InviteSent       => "Convite enviado.",
            GuildNoticeCode.InviteDeclined   => "Seu convite foi recusado.",
            GuildNoticeCode.NoPermission     => "Você não tem permissão pra isso.",
            GuildNoticeCode.Created          => "Guilda criada!",
            GuildNoticeCode.YouLeft          => "Você saiu da guilda.",
            GuildNoticeCode.Kicked           => "Você foi removido da guilda.",
            GuildNoticeCode.Disbanded        => "A guilda foi desfeita.",
            GuildNoticeCode.RankChanged      => "Seu cargo mudou.",
            GuildNoticeCode.CannotInviteSelf => "Você não pode se convidar.",
            GuildNoticeCode.GuildFull        => "A guilda está cheia.",
            _ => "",
        };
    }
}
