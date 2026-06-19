using NetworkLibrary.Serialization;
using ArcaneShared.Enums;
using ArcaneShared.Protocol.ServerToClient;
using Arcane_Aegis.UI;

namespace Arcane_Aegis.Network.Handlers
{
    /// <summary>S2C_FriendNotice: a short friend result/info code → pt-BR toast.</summary>
    public sealed class FriendNoticeHandler : IClientPacketHandler
    {
        public PacketId PacketId => PacketId.S2C_FriendNotice;

        public void Handle(ref BitBuffer reader)
        {
            var p = new S2C_FriendNotice();
            p.Deserialize(ref reader);
            string text = Text((FriendNoticeCode)p.Code);
            if (!string.IsNullOrEmpty(text) && Toast.Instance != null) Toast.Instance.Show(text);
        }

        private static string Text(FriendNoticeCode code) => code switch
        {
            FriendNoticeCode.PlayerNotFound  => "Jogador não encontrado (precisa estar online).",
            FriendNoticeCode.AlreadyFriends  => "Vocês já são amigos.",
            FriendNoticeCode.RequestSent     => "Pedido de amizade enviado.",
            FriendNoticeCode.RequestDeclined => "Seu pedido foi recusado.",
            FriendNoticeCode.CannotAddSelf   => "Você não pode se adicionar.",
            FriendNoticeCode.FriendRemoved   => "Amigo removido.",
            FriendNoticeCode.AlreadyRequested=> "Já existe um pedido pendente.",
            FriendNoticeCode.FriendAdded     => "Novo amigo adicionado!",
            _ => "",
        };
    }
}
