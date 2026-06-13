using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using ArcaneShared.Enums;
using ArcaneShared.Models;
using Arcane_Aegis.Content;

namespace Arcane_Aegis.UI
{
    /// <summary>
    /// The item tooltip: a singleton panel that shows an item's info while the cursor hovers a slot (BagUI/EquipmentUI
    /// call <see cref="Show"/>/<see cref="Hide"/>). Put this on a tooltip panel with a title + body TMP_Text and assign
    /// them. The panel follows the mouse. Content comes from the item's <see cref="ItemDefinitionSO"/> (gameplay + art).
    /// </summary>
    public sealed class ItemTooltip : MonoBehaviour
    {
        public static ItemTooltip Instance { get; private set; }

        [Tooltip("The panel root toggled on/off (usually this object's child holding the visuals).")]
        [SerializeField] private GameObject panel;
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text body;
        [Tooltip("Offset from the cursor, in pixels.")]
        [SerializeField] private Vector2 cursorOffset = new Vector2(18f, -18f);

        private RectTransform _rect;

        private void Awake()
        {
            Instance = this;
            _rect = (panel != null ? panel.transform : transform) as RectTransform;
            HideNow();
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        public void Show(in ItemInstance item, ItemDefinitionSO so)
        {
            if (panel != null) panel.SetActive(true);

            string name = so != null && !string.IsNullOrEmpty(so.displayName) ? so.displayName : item.TemplateId;
            if (title != null)
            {
                title.text = name;
                title.color = RarityColor(so != null ? so.rarity : ItemRarity.Common);
            }
            if (body != null) body.text = BuildBody(item, so);
            Follow();
        }

        public void Hide() => HideNow();

        private void HideNow() { if (panel != null) panel.SetActive(false); }

        private void Update()
        {
            if (panel != null && panel.activeSelf) Follow();
        }

        private void Follow()
        {
            if (_rect == null || Mouse.current == null) return;
            Vector2 m = Mouse.current.position.ReadValue();
            _rect.position = new Vector3(m.x + cursorOffset.x, m.y + cursorOffset.y, 0f);
        }

        private static string BuildBody(in ItemInstance item, ItemDefinitionSO so)
        {
            var sb = new StringBuilder();
            if (so != null)
            {
                string line = so.type.ToString();
                if (so.slot != EquipSlot.None) line += $" · {so.slot}";
                if (so.twoHanded) line += " · Duas mãos";
                sb.AppendLine($"<color=#AAAAAA>{line}</color>");

                if (so.levelReq > 1) sb.AppendLine($"Requer Nível {so.levelReq}");
                if (!string.IsNullOrEmpty(so.classReq)) sb.AppendLine($"Classe: {so.classReq}");

                if (so.statsBase != null)
                    foreach (var s in so.statsBase)
                        if (s.value != 0) sb.AppendLine($"<color=#7CFC7C>+{s.value} {s.statId}</color>");

                if (so.effects != null)
                    foreach (var e in so.effects)
                        if (e.kind != ConsumableEffectKind.None) sb.AppendLine($"<color=#7CC8FF>{FormatEffect(e)}</color>");

                if (so.durabilityMax > 0) sb.AppendLine($"Durabilidade: {item.DurabilityNow}/{so.durabilityMax}");
            }

            // Instance bits (server-driven).
            if (item.Quantity > 1) sb.AppendLine($"Quantidade: {item.Quantity}");
            if (item.RefineLevel > 0) sb.AppendLine($"Refino +{item.RefineLevel}");
            if (item.Bound) sb.AppendLine("<color=#CC6666>Vinculado</color>");
            if (item.Rolls != null)
                foreach (var r in item.Rolls)
                    if (r.Value != 0) sb.AppendLine($"<color=#6FB7FF>+{r.Value} {r.StatId}</color>");

            if (so != null && !string.IsNullOrEmpty(so.description))
                sb.AppendLine($"\n<color=#999999><i>{so.description}</i></color>");

            return sb.ToString().TrimEnd();
        }

        private static string FormatEffect(ItemDefinitionSO.Effect e)
        {
            string dur = e.durationSeconds > 0 ? $" por {FormatDuration(e.durationSeconds)}" : "";
            return e.kind switch
            {
                ConsumableEffectKind.RestoreHp          => $"Recupera {e.amount} HP",
                ConsumableEffectKind.RestoreMana        => $"Recupera {e.amount} Mana",
                ConsumableEffectKind.RestoreHpPercent   => $"Recupera {e.amount}% HP",
                ConsumableEffectKind.RestoreManaPercent => $"Recupera {e.amount}% Mana",
                ConsumableEffectKind.BuffStat           => $"+{e.amount} {e.statId}{dur}",
                ConsumableEffectKind.BuffXpRate         => $"+{e.amount}% XP{dur}",
                ConsumableEffectKind.BuffDropRate       => $"+{e.amount}% Drop{dur}",
                ConsumableEffectKind.BuffMoveSpeed      => $"+{e.amount}% Velocidade{dur}",
                _ => "",
            };
        }

        private static string FormatDuration(int s)
        {
            if (s >= 3600) return $"{s / 3600}h";
            if (s >= 60) return $"{s / 60}min";
            return $"{s}s";
        }

        private static Color RarityColor(ItemRarity rarity) => rarity switch
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
