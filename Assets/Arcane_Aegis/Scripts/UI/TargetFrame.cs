using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Arcane_Aegis.Entities;
using Arcane_Aegis.Combat;

namespace Arcane_Aegis.UI
{
    /// <summary>
    /// HUD frame for the player's current target (the id resolved by <see cref="TargetingSystem"/>): shows its name
    /// + HP bar, hidden when there's no target. Build the visual by hand and assign the refs. Set <see cref="fill"/>
    /// to an Image with Image Type = Filled (Horizontal).
    /// </summary>
    public sealed class TargetFrame : MonoBehaviour
    {
        [SerializeField] private GameObject root;     // panel toggled on/off with a target
        [SerializeField] private Image fill;          // Image Type = Filled (Horizontal); fillAmount 0..1
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TargetingSystem targeting;
        [SerializeField] private float smoothSpeed = 8f; // bar units/sec (0 = instant)

        private EntityManager _entities;
        private float _shown = 1f;
        private ushort _lastId;

        private void Awake()
        {
            _entities = FindAnyObjectByType<EntityManager>();
            if (targeting == null) targeting = FindAnyObjectByType<TargetingSystem>();
        }

        private void Update()
        {
            ushort id = targeting != null ? targeting.CurrentTargetId : (ushort)0;
            if (id == 0 || _entities == null || !_entities.TryGetView(id, out var view))
            {
                if (root != null && root.activeSelf) root.SetActive(false);
                _lastId = 0;
                return;
            }

            if (root != null && !root.activeSelf) root.SetActive(true);

            float target = view.SnapHp01;
            if (id != _lastId) { _shown = target; _lastId = id; } // new target → snap (don't slide from the old one's HP)
            _shown = smoothSpeed > 0f ? Mathf.MoveTowards(_shown, target, smoothSpeed * Time.deltaTime) : target;

            if (fill != null) fill.fillAmount = _shown;
            if (nameLabel != null) nameLabel.text = view.DisplayName;
        }
    }
}
