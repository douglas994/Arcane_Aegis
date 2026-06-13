using UnityEngine;
using NetworkLibrary.Serialization;
using ArcaneShared.Enums;
using ArcaneShared.Protocol.ServerToClient;
using Arcane_Aegis.Items;

namespace Arcane_Aegis.Network.Handlers
{
    /// <summary>S2C_InventoryState: the player's full inventory → the InventoryStore (the bag UI re-renders).</summary>
    public sealed class InventoryStateHandler : IClientPacketHandler
    {
        public PacketId PacketId => PacketId.S2C_InventoryState;

        public void Handle(ref BitBuffer reader)
        {
            var p = new S2C_InventoryState();
            p.Deserialize(ref reader);
            if (InventoryStore.Instance != null) InventoryStore.Instance.SetItems(p.Items);
            Debug.Log($"[Inventory] received {p.Items?.Length ?? 0} item(s)");
        }
    }
}
