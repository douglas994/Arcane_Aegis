using NetworkLibrary.Serialization;
using ArcaneShared.Enums;
using ArcaneShared.Protocol.ServerToClient;
using Arcane_Aegis.UI;

namespace Arcane_Aegis.Network.Handlers
{
    /// <summary>S2C_CraftEnd (owner-only): the server ended a craft → close the bar; toast on failure. The produced item
    /// arrives via S2C_InventoryState (+ S2C_LootGained popup) and XP via S2C_ProfessionXp.</summary>
    public sealed class CraftEndHandler : IClientPacketHandler
    {
        public PacketId PacketId => PacketId.S2C_CraftEnd;

        public void Handle(ref BitBuffer reader)
        {
            var p = new S2C_CraftEnd();
            p.Deserialize(ref reader);
            if (CraftProgress.Instance != null) CraftProgress.Instance.End();
            if (p.Reason == 1 && Toast.Instance != null) Toast.Instance.Show("Falha ao criar.");
        }
    }
}
