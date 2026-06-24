using UnityEngine;
using ArcaneShared.Enums;
using Arcane_Aegis.Content;

namespace Arcane_Aegis.Entities
{
    /// <summary>View for a ground-loot drop. Built to sit on a hand-made prefab (e.g. a chest): wire the refs and it
    /// fills the item name (rarity-colored) and lights the VFX matching the item's RARITY. The name reveals only when
    /// the camera is near (declutter). With no prefab/model it falls back to a billboard icon so loot is still visible.
    /// The root is positioned by the snapshot — bob/billboard run on child visuals so they never fight interpolation.</summary>
    public sealed class ItemDropView : EntityView
    {
        [Header("Refs (opcionais — auto-fallback se vazios)")]
        [Tooltip("Ícone do item (billboard). Só usado no fallback sem prefab/modelo.")]
        [SerializeField] private SpriteRenderer iconRenderer;
        [Tooltip("Nome do item. A cor é definida pela raridade.")]
        [SerializeField] private TextMesh label;
        [Tooltip("Altura (m) do nome acima da raiz do drop.")]
        [SerializeField] private float labelHeight = 1.1f;
        [Tooltip("Mostra o nome só quando a câmera está a esta distância (m). 0 = sempre.")]
        [SerializeField] private float nameRevealRange = 9f;
        [Tooltip("Balançar suavemente os visuais (ícone/nome).")]
        [SerializeField] private bool bob = true;

        [Header("VFX por raridade")]
        [Tooltip("UM objeto por raridade, na ordem: Comum, Incomum, Raro, Épico, Lendário, Mítico. Liga o da raridade do item e desliga os demais. Pode deixar buracos (null).")]
        [SerializeField] private GameObject[] rarityVfx;
        [Tooltip("Opcional: 1 ParticleSystem único cuja cor é tingida pela raridade (alternativa ao array acima).")]
        [SerializeField] private ParticleSystem tintedVfx;

        private Transform _cam;
        private float _bobBaseY;
        private bool _hasBob;

        /// <summary>Fills the name + rarity color/VFX from the item template (null = unknown id → name fallback).
        /// <paramref name="showIcon"/> builds a floating sprite icon — only for the bare fallback (no prefab/model);
        /// a chest/box prefab shows the name (proximity-revealed) over the prefab instead.</summary>
        public void Configure(ItemDefinitionSO def, string fallbackName, bool showIcon = false)
        {
            ItemRarity rarity = def != null ? def.rarity : ItemRarity.Common;
            Color color = RarityColor(rarity);

            // ── Sprite icon: only the bare fallback (it's the only visual) ──
            if (showIcon)
            {
                if (iconRenderer == null) iconRenderer = GetComponentInChildren<SpriteRenderer>();
                if (iconRenderer == null)
                {
                    var iconGo = new GameObject("Icon");
                    iconGo.transform.SetParent(transform, false);
                    iconGo.transform.localPosition = new Vector3(0f, 0.6f, 0f);
                    iconRenderer = iconGo.AddComponent<SpriteRenderer>();
                }
                if (def != null && def.icon != null) iconRenderer.sprite = def.icon;
                iconRenderer.color = Color.white;
            }

            // ── Name label (clean, small; proximity-revealed in LateUpdate) ──
            if (label == null) label = GetComponentInChildren<TextMesh>();
            if (label == null)
            {
                var labelGo = new GameObject("Label");
                labelGo.transform.SetParent(transform, false);
                labelGo.transform.localPosition = new Vector3(0f, labelHeight, 0f);
                label = labelGo.AddComponent<TextMesh>();
                label.anchor = TextAnchor.MiddleCenter;
                label.alignment = TextAlignment.Center;
                label.characterSize = 0.06f;
                label.fontSize = 90;       // big font + tiny characterSize = crisp, small on screen
            }
            label.text = def != null ? def.displayName : (fallbackName ?? "Item");
            label.color = color;

            // ── Rarity VFX: enable the matching one, disable the rest ──
            if (rarityVfx != null)
                for (int i = 0; i < rarityVfx.Length; i++)
                    if (rarityVfx[i] != null) rarityVfx[i].SetActive(i == (int)rarity);
            if (tintedVfx != null) { var main = tintedVfx.main; main.startColor = color; }

            if (iconRenderer != null) { _hasBob = true; _bobBaseY = iconRenderer.transform.localPosition.y; }
        }

        private void LateUpdate()
        {
            if (_cam == null) { var c = Camera.main; if (c != null) _cam = c.transform; }

            // Proximity reveal: show the name only when the camera is close (keeps the world uncluttered).
            if (label != null && _cam != null && nameRevealRange > 0f)
            {
                float dx = label.transform.position.x - _cam.position.x;
                float dz = label.transform.position.z - _cam.position.z;
                bool near = dx * dx + dz * dz <= nameRevealRange * nameRevealRange;
                if (label.gameObject.activeSelf != near) label.gameObject.SetActive(near);
            }

            if (_cam != null)
            {
                if (iconRenderer != null) iconRenderer.transform.forward = _cam.forward;
                if (label != null && label.gameObject.activeSelf) label.transform.forward = _cam.forward;
            }

            // Bob only the sprite-icon fallback (the chest prefab stays put).
            if (bob && _hasBob && iconRenderer != null)
                iconRenderer.transform.localPosition = new Vector3(iconRenderer.transform.localPosition.x, _bobBaseY + Mathf.Sin(Time.time * 2f) * 0.08f, iconRenderer.transform.localPosition.z);
        }

        private static Color RarityColor(ItemRarity r) => r switch
        {
            ItemRarity.Uncommon => new Color(0.4f, 0.85f, 0.4f),  // green
            ItemRarity.Rare => new Color(0.35f, 0.6f, 1f),        // blue
            ItemRarity.Epic => new Color(0.7f, 0.45f, 0.95f),     // purple
            ItemRarity.Legendary => new Color(1f, 0.6f, 0.2f),    // orange
            ItemRarity.Mythic => new Color(1f, 0.3f, 0.3f),       // red/gold
            _ => Color.white,                                      // common
        };
    }
}
