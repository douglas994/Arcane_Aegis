using NetworkLibrary.Serialization;
using ArcaneShared.Enums;
using ArcaneShared.Protocol.ServerToClient;
using Arcane_Aegis.Items;

namespace Arcane_Aegis.Network.Handlers
{
    /// <summary>S2C_ItemTemplates: the item moulds → the InventoryStore catalog (names/icons/stats for the bag UI).</summary>
    public sealed class ItemTemplatesHandler : IClientPacketHandler
    {
        public PacketId PacketId => PacketId.S2C_ItemTemplates;

        public void Handle(ref BitBuffer reader)
        {
            var p = new S2C_ItemTemplates();
            p.Deserialize(ref reader);
            if (InventoryStore.Instance != null) InventoryStore.Instance.SetCatalog(p.Templates);
        }
    }
}
