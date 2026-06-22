using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Arcane_Aegis.UI
{
    /// <summary>One row in the <see cref="CollectionPanel"/>: a pet/mount the player owns — icon, name, an "active" badge,
    /// and a "Set active" button. Pooled/cloned by CollectionPanel; wire the refs on the template.</summary>
    public sealed class CollectionRow : MonoBehaviour
    {
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text label;
        [SerializeField] private GameObject activeBadge;
        [SerializeField] private Button setActiveBtn;

        private byte _kind;
        private string _defId;
        private Action<byte, string> _onSetActive;

        private void Awake()
        {
            if (setActiveBtn != null) setActiveBtn.onClick.AddListener(() => _onSetActive?.Invoke(_kind, _defId));
        }

        public void Bind(byte kind, string defId, string name, Sprite sprite, bool active, Action<byte, string> onSetActive)
        {
            _kind = kind; _defId = defId; _onSetActive = onSetActive;
            if (icon != null) { icon.sprite = sprite; icon.enabled = sprite != null; }
            if (label != null) label.text = name;
            if (activeBadge != null) activeBadge.SetActive(active);
            if (setActiveBtn != null) setActiveBtn.interactable = !active; // can't "activate" the already-active one
        }
    }
}
