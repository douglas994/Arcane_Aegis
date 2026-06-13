using UnityEngine;
using UnityEngine.EventSystems;
using Arcane_Aegis.Items;
using Arcane_Aegis.Network;

namespace Arcane_Aegis.UI
{
    /// <summary>
    /// The trash: click it while holding a bag item (picked via <see cref="InventoryCursor"/>) to destroy it. The
    /// server validates (must be a bag item the player owns, not a quest item). Put this on a raycast-target Image/Button;
    /// it's part of the same click-to-pick → click-to-place flow as the bag/equipment slots. Discarding is destructive,
    /// so it only fires when something is actually held (the deliberate two-click pick → trash). Also callable from a
    /// Button's onClick via <see cref="Discard"/>.
    /// </summary>
    public sealed class TrashSlot : MonoBehaviour, IPointerClickHandler
    {
        public void OnPointerClick(PointerEventData eventData) => Discard();

        public void Discard()
        {
            var cursor = InventoryCursor.Instance;
            if (cursor == null || !cursor.HasPicked) return;

            if (NetClient.Instance != null) NetClient.Instance.SendDiscardItem(cursor.PickedInstanceId);
            cursor.Clear();
        }
    }
}
