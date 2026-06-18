using UnityEngine;
using ArcaneShared.Enums;

namespace Arcane_Aegis.UI
{
    /// <summary>Single source of truth for an item rarity's display color — shared by the tooltip title and the bag-slot
    /// rarity frame so they never drift apart. Common is white; higher tiers warm/brighten (WoW-style ladder).</summary>
    public static class RarityColors
    {
        public static Color For(ItemRarity rarity) => rarity switch
        {
            ItemRarity.Uncommon  => new Color(0.49f, 0.99f, 0.49f), // green
            ItemRarity.Rare      => new Color(0.40f, 0.70f, 1.00f), // blue
            ItemRarity.Epic      => new Color(0.75f, 0.45f, 0.95f), // purple
            ItemRarity.Legendary => new Color(1.00f, 0.65f, 0.20f), // orange
            ItemRarity.Mythic    => new Color(1.00f, 0.35f, 0.35f), // red
            _                    => Color.white,                    // common
        };
    }
}
