using System;
using System.Collections.Generic;
using UnityEngine;
using ArcaneShared.Enums;
using ArcaneShared.Models;
using Arcane_Aegis.Items;
using Arcane_Aegis.Content;
using Arcane_Aegis.Network;

namespace Arcane_Aegis.UI
{
    /// <summary>
    /// The equipment "doll": one FIXED <see cref="BagSlot"/> per <see cref="EquipSlot"/> (MainHand, Head, Chest…),
    /// assigned in the inspector. Fills the slot whose item is equipped there and clears the rest. Uses the
    /// click-to-pick → click-to-place flow (<see cref="InventoryCursor"/>): click an equipped item to pick it (then
    /// click a bag slot to unequip / the trash won't take it — unequip first), or, while holding a bag item, click any
    /// equip slot to equip it (the server forces the correct slot). Pairs with <see cref="BagUI"/>.
    /// </summary>
    public class EquipmentUI : MonoBehaviour
    {
        [Serializable]
        public struct SlotView
        {
            [Tooltip("Which equipment slot this view represents.")]
            public EquipSlot slot;
            [Tooltip("The BagSlot view (icon + label) for that slot.")]
            public BagSlot view;
        }

        [Tooltip("One entry per equip slot you want to show (MainHand, OffHand, Head, Chest, Hands, Feet, Ring, Necklace).")]
        [SerializeField] private List<SlotView> slots = new();
        [Tooltip("Resolves item id → ItemDefinitionSO for the icon/name.")]
        [SerializeField] private ContentLibrary library;

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

        private void Render()
        {
            var store = InventoryStore.Instance;
            if (store == null) return;
            var cursor = InventoryCursor.Instance;

            foreach (var sv in slots)
            {
                if (sv.view == null) continue;

                ItemInstance equipped = default;
                bool found = false;
                foreach (var it in store.Items)
                    if (it.Container == ItemContainer.Equipped && (EquipSlot)it.Slot == sv.slot) { equipped = it; found = true; break; }

                EquipSlot slot = sv.slot;
                if (found)
                {
                    ItemDefinitionSO so = library != null ? library.GetItem(equipped.TemplateId) : null;
                    Sprite icon = so != null ? so.icon : null;
                    string text = so != null && !string.IsNullOrEmpty(so.displayName) ? so.displayName
                                : (store.TryGetTemplate(equipped.TemplateId, out var t) ? t.Name : equipped.TemplateId);
                    if (equipped.RefineLevel > 0) text += $" +{equipped.RefineLevel}";
                    uint id = equipped.InstanceId;
                    ItemInstance captured = equipped;
                    sv.view.Bind(icon, text, () => OnEquipClicked(slot, id), null, on => HoverTooltip(on, captured, so));
                    sv.view.SetPicked(cursor != null && cursor.PickedInstanceId == id);
                }
                else
                {
                    sv.view.Bind(null, "", () => OnEquipClicked(slot, 0));
                    sv.view.SetPicked(false);
                }
            }
        }

        // Nothing held → clicking an equipped item picks it (click a bag slot next to unequip). Holding a bag item →
        // clicking any equip slot equips it (server forces the correct slot). Re-click the held item = cancel.
        private static void OnEquipClicked(EquipSlot slot, uint instanceId)
        {
            var cursor = InventoryCursor.Instance;
            if (cursor == null) return;

            if (!cursor.HasPicked)
            {
                if (instanceId != 0) cursor.Pick(instanceId);
                return;
            }
            if (instanceId != 0 && instanceId == cursor.PickedInstanceId) { cursor.Clear(); return; } // re-click = cancel

            if (NetClient.Instance != null) NetClient.Instance.SendMoveItem(cursor.PickedInstanceId, (byte)ItemContainer.Equipped, 0);
            cursor.Clear();
        }

        private static void HoverTooltip(bool on, ItemInstance item, ItemDefinitionSO so)
        {
            var tip = ItemTooltip.Instance;
            if (tip == null) return;
            if (on) tip.Show(item, so); else tip.Hide();
        }
    }
}
