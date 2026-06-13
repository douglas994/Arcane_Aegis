using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

namespace Arcane_Aegis.UI
{
    /// <summary>
    /// One inventory/equipment slot's view: an icon Image + a label, plus clicks/hover that fire back to the owning
    /// panel (BagUI/EquipmentUI). LEFT click drives the click-to-pick → click-to-place flow; RIGHT click is the
    /// "alt" action (split a stack); hover shows the item tooltip. Every slot (even empty) is a valid LEFT-click target
    /// so it can receive a placed item — needs a raycast-target Graphic (a background Image) for the click to register.
    /// </summary>
    public sealed class BagSlot : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text label;
        [Tooltip("Optional: a frame/glow shown while this slot's item is picked (held by the cursor).")]
        [SerializeField] private GameObject pickedHighlight;

        private Action _onClick;       // left click
        private Action _onAltClick;    // right click (split)
        private Action<bool> _onHover; // enter (true) / exit (false)

        /// <summary>Fills the slot and wires its interactions. Empty slots pass a null sprite + empty text but still get a left-click.</summary>
        public void Bind(Sprite sprite, string text, Action onClick, Action onAltClick = null, Action<bool> onHover = null)
        {
            _onClick = onClick;
            _onAltClick = onAltClick;
            _onHover = onHover;
            if (icon != null)
            {
                icon.sprite = sprite;
                icon.enabled = sprite != null; // hide the icon graphic when the slot is empty
            }
            if (label != null) label.text = text;
        }

        public void SetPicked(bool on)
        {
            if (pickedHighlight != null) pickedHighlight.SetActive(on);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Right) _onAltClick?.Invoke();
            else _onClick?.Invoke();
        }

        public void OnPointerEnter(PointerEventData eventData) => _onHover?.Invoke(true);
        public void OnPointerExit(PointerEventData eventData) => _onHover?.Invoke(false);
    }
}
