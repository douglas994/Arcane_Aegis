using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Arcane_Aegis.Content;
using Arcane_Aegis.Entities;

namespace Arcane_Aegis.UI
{
    /// <summary>
    /// One action-bar slot. Auto-binds the Button's OnClick → <see cref="Cast"/> (leave OnClick empty in the
    /// inspector). Casts via the local player's combat (which gates the cooldown) and shows the cooldown as a
    /// radial fill. Set <see cref="cooldownOverlay"/> to an Image with Image Type = Filled, Fill Method = Radial 360.
    /// Data-driven: the icon + name come from the SkillDefinitionSO with this <see cref="abilityId"/> (via the
    /// ContentLibrary), so authoring a skill in the editor automatically dresses its button.
    /// </summary>
    public class AbilityButton : MonoBehaviour
    {
        [SerializeField] private int abilityId = 1;
        [SerializeField] private Image icon;            // optional: filled from the skill's icon
        [SerializeField] private TMP_Text nameLabel;    // optional: filled from the skill's displayName
        [SerializeField] private Image cooldownOverlay; // Filled/Radial360; fillAmount = remaining (1=just cast, 0=ready)
        [SerializeField] private TMP_Text cooldownText; // optional: seconds remaining

        private EntityManager _entities;
        private bool _dressed;

        private void Awake()
        {
            _entities = FindAnyObjectByType<EntityManager>();

            var button = GetComponent<Button>();
            // Auto-wire only if the inspector OnClick is empty, so we never double-fire.
            if (button != null && button.onClick.GetPersistentEventCount() == 0)
                button.onClick.AddListener(Cast);
            if (cooldownOverlay != null) cooldownOverlay.fillAmount = 0f;
            Dress();
        }

        /// <summary>Pulls the icon + name from the SkillDefinitionSO (once the ContentLibrary is loaded).</summary>
        private void Dress()
        {
            if (_dressed || ContentLibrary.Active == null) return;
            var so = ContentLibrary.Active.GetSkill(abilityId);
            if (so == null) return;
            if (icon != null && so.icon != null) icon.sprite = so.icon;
            if (nameLabel != null) nameLabel.text = so.displayName;
            _dressed = true;
        }

        /// <summary>Cast this slot's ability (server validates + enforces the real cooldown).</summary>
        public void Cast()
        {
            var local = _entities != null ? _entities.Local : null;
            if (local != null && local.Combat != null) local.Combat.TryCast((byte)abilityId);
        }

        private void Update()
        {
            if (!_dressed) Dress();

            var local = _entities != null ? _entities.Local : null;
            if (local == null || local.Combat == null) return;

            byte id = (byte)abilityId;
            if (cooldownOverlay != null) cooldownOverlay.fillAmount = local.Combat.GetCooldownRemaining01(id);
            if (cooldownText != null)
            {
                float rem = local.Combat.GetCooldownRemaining(id);
                cooldownText.text = rem > 0.05f ? rem.ToString("0.0") : string.Empty;
            }
        }
    }
}
