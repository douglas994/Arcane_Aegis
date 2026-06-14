using NetworkLibrary.Serialization;
using ArcaneShared.Enums;
using ArcaneShared.Protocol.ServerToClient;
using Arcane_Aegis.Content;
using Arcane_Aegis.Items;
using Arcane_Aegis.UI;

namespace Arcane_Aegis.Network.Handlers
{
    /// <summary>S2C_LootGained (owner-only): an item dropped into the bag → show a "+N Item" popup. The inventory itself
    /// updates via S2C_InventoryState; this is just the feedback line. Resolves the display name from the content.</summary>
    public sealed class LootGainedHandler : IClientPacketHandler
    {
        public PacketId PacketId => PacketId.S2C_LootGained;

        public void Handle(ref BitBuffer reader)
        {
            var p = new S2C_LootGained();
            p.Deserialize(ref reader);
            if (Toast.Instance == null) return;

            string name = p.ItemId;
            var so = ContentLibrary.Active != null ? ContentLibrary.Active.GetItem(p.ItemId) : null;
            if (so != null && !string.IsNullOrEmpty(so.displayName)) name = so.displayName;
            else if (InventoryStore.Instance != null && InventoryStore.Instance.TryGetTemplate(p.ItemId, out var t)) name = t.Name;

            Toast.Instance.Show(p.Quantity > 1 ? $"+{p.Quantity} {name}" : $"+{name}");
        }
    }
}
