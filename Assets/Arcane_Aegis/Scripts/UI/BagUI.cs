using UnityEngine;
using TMPro;
using ArcaneShared.Constants;
using ArcaneShared.Enums;
using ArcaneShared.Models;
using Arcane_Aegis.Items;
using Arcane_Aegis.Content;
using Arcane_Aegis.Network;

namespace Arcane_Aegis.UI
{
    /// <summary>
    /// Bag renderer: a FIXED grid of <see cref="GameplayConstants.BagCapacity"/> slots (created once, reused), each
    /// bound to its slot index. Items render in the slot whose <c>Slot</c> matches; the rest are empty. Uses the
    /// click-to-pick → click-to-place flow (<see cref="InventoryCursor"/>): click an item to pick it, click any bag
    /// slot to move/swap it there (server-authoritative), click the same item to cancel. Equipping/unequipping is the
    /// <see cref="EquipmentUI"/>; discarding is the trash (<see cref="TrashSlot"/>).
    /// </summary>
    public class BagUI : MonoBehaviour
    {
        [Tooltip("Slot prefab — must have a BagSlot component (icon Image + TMP_Text label).")]
        [SerializeField] private GameObject slotPrefab;
        [Tooltip("Parent (e.g. a Grid Layout Group) the fixed slots are created under.")]
        [SerializeField] private Transform gridParent;
        [Tooltip("Resolves item id → ItemDefinitionSO for the icon/name.")]
        [SerializeField] private ContentLibrary library;

        private BagSlot[] _slots; // fixed grid, index = bag slot

        private void OnEnable()
        {
            if (InventoryStore.Instance != null) InventoryStore.Instance.OnChanged += Render;
            if (InventoryCursor.Instance != null) InventoryCursor.Instance.OnChanged += Render;
            Render();
        }

        private void OnDisable()
        {
            if (InventoryStore.Instance != null) InventoryStore.Instance.OnChanged -= Render;
            if (InventoryCursor.Instance != null) InventoryCursor.Instance.OnChanged -= Render;
        }

        private void EnsureSlots()
        {
            if (_slots != null || gridParent == null || slotPrefab == null) return;

            for (int i = gridParent.childCount - 1; i >= 0; i--) Destroy(gridParent.GetChild(i).gameObject); // clear placeholders

            _slots = new BagSlot[GameplayConstants.BagCapacity];
            for (int i = 0; i < _slots.Length; i++)
            {
                GameObject go = Instantiate(slotPrefab, gridParent);
                go.SetActive(true);
                _slots[i] = go.GetComponent<BagSlot>();
            }
        }

        private void Render()
        {
            EnsureSlots();
            if (_slots == null) return;
            var store = InventoryStore.Instance;
            if (store == null) return;
            var cursor = InventoryCursor.Instance;

            for (int i = 0; i < _slots.Length; i++)
            {
                var slot = _slots[i];
                if (slot == null) continue;

                // Find the item sitting in bag slot i (if any).
                ItemInstance item = default;
                bool filled = false;
                foreach (var it in store.Items)
                    if (it.Container == ItemContainer.Bag && it.Slot == i) { item = it; filled = true; break; }

                if (filled)
                {
                    ItemDefinitionSO so = library != null ? library.GetItem(item.TemplateId) : null;
                    Sprite icon = so != null ? so.icon : null;
                    uint id = item.InstanceId;
                    ItemInstance captured = item; // for the split/hover closures
                    slot.Bind(icon, Describe(item, so, store),
                        () => OnSlotClicked(i, id),         // left: pick/place
                        () => AltAction(captured, so),      // right: use (consumable) or split (other stack)
                        on => HoverTooltip(on, captured, so));
                    slot.SetPicked(cursor != null && cursor.PickedInstanceId == id);
                }
                else
                {
                    int idx = i;
                    slot.Bind(null, "", () => OnSlotClicked(idx, 0));
                    slot.SetPicked(false);
                }
            }
        }

        // Pick-and-place: nothing held → clicking a filled slot picks it; something held → clicking any slot places it
        // there (server validates/swaps); clicking the held item again cancels.
        private static void OnSlotClicked(int slotIndex, uint instanceId)
        {
            var cursor = InventoryCursor.Instance;
            if (cursor == null) return;

            if (!cursor.HasPicked)
            {
                if (instanceId != 0) cursor.Pick(instanceId);
                return;
            }
            if (instanceId != 0 && instanceId == cursor.PickedInstanceId) { cursor.Clear(); return; } // re-click = cancel

            if (NetClient.Instance != null) NetClient.Instance.SendMoveItem(cursor.PickedInstanceId, (byte)ItemContainer.Bag, (ushort)slotIndex);
            cursor.Clear();
        }

        // Right-click = the item's natural action: a consumable is USED; any other stack is SPLIT in half.
        private static void AltAction(ItemInstance item, ItemDefinitionSO so)
        {
            if (so != null && so.type == ItemType.Consumable)
            {
                if (NetClient.Instance != null) NetClient.Instance.SendUseItem(item.InstanceId);
                return;
            }
            SplitHalf(item);
        }

        // Split a stack in half into a free slot (server validates it's stackable + has room).
        private static void SplitHalf(ItemInstance item)
        {
            if (item.Quantity <= 1) return;
            ushort half = (ushort)(item.Quantity / 2);
            if (half >= 1 && NetClient.Instance != null) NetClient.Instance.SendSplitStack(item.InstanceId, half);
        }

        private static void HoverTooltip(bool on, ItemInstance item, ItemDefinitionSO so)
        {
            var tip = ItemTooltip.Instance;
            if (tip == null) return;
            if (on) tip.Show(item, so); else tip.Hide();
        }

        private static string Describe(in ItemInstance it, ItemDefinitionSO so, InventoryStore store)
        {
            string name = so != null && !string.IsNullOrEmpty(so.displayName) ? so.displayName
                        : (store.TryGetTemplate(it.TemplateId, out var t) && !string.IsNullOrEmpty(t.Name) ? t.Name : it.TemplateId);
            if (it.Quantity > 1) name += $" x{it.Quantity}";
            if (it.RefineLevel > 0) name += $" +{it.RefineLevel}";
            return name;
        }
    }
}
