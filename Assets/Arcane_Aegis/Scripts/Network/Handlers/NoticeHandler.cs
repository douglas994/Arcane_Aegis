using NetworkLibrary.Serialization;
using ArcaneShared.Enums;
using ArcaneShared.Protocol.ServerToClient;
using Arcane_Aegis.UI;

namespace Arcane_Aegis.Network.Handlers
{
    /// <summary>S2C_Notice: a short reason code from the server → mapped to localized text and shown as a toast.</summary>
    public sealed class NoticeHandler : IClientPacketHandler
    {
        public PacketId PacketId => PacketId.S2C_Notice;

        public void Handle(ref BitBuffer reader)
        {
            var p = new S2C_Notice();
            p.Deserialize(ref reader);
            string text = Text((NoticeCode)p.Code);
            if (!string.IsNullOrEmpty(text) && Toast.Instance != null) Toast.Instance.Show(text);
        }

        private static string Text(NoticeCode code) => code switch
        {
            NoticeCode.EquipLevelTooLow    => "Nível insuficiente para equipar.",
            NoticeCode.EquipWrongClass     => "Sua classe não pode equipar isso.",
            NoticeCode.EquipBroken         => "Item quebrado — repare antes de equipar.",
            NoticeCode.TwoHandBlocksOffHand=> "Arma de duas mãos ocupa a off-hand.",
            NoticeCode.BagFull             => "Mochila cheia.",
            NoticeCode.NeedWeapon          => "Você precisa da arma certa equipada.",
            NoticeCode.ClassCannotCast     => "Sua classe não conhece essa skill.",
            NoticeCode.DiedDroppedItem     => "Você perdeu um item ao morrer!",
            NoticeCode.TooFar              => "Muito longe.",
            NoticeCode.ProfessionTooLow    => "Nível de profissão insuficiente.",
            NoticeCode.NeedCampfire        => "Você precisa estar perto de uma fogueira para cozinhar.",
            NoticeCode.BuildLimitReached   => "Você atingiu o limite de construções.",
            NoticeCode.BuildBlocked        => "Não dá pra construir aqui.",
            NoticeCode.NotYourBuilding     => "Você só pode demolir o que é seu.",
            NoticeCode.NotEnoughCurrency   => "Moeda insuficiente.",
            NoticeCode.VendorNoBuy         => "O vendedor não vende isso.",
            NoticeCode.ItemNotSellable     => "Item não pode ser vendido.",
            NoticeCode.GearBroke           => "Um equipamento quebrou! Conserte no vendedor.",
            NoticeCode.NothingToRepair     => "Nada para consertar.",
            NoticeCode.SealLevelTooLow     => "Nível insuficiente para quebrar o selo.",
            NoticeCode.SealMissingItems    => "Faltam os itens-chave para quebrar o selo.",
            NoticeCode.SealBroken          => "O selo se quebra... o chefe desperta!",
            _ => "",
        };
    }
}
