using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Arcane_Aegis.UI
{
    /// <summary>
    /// Shows a 3D character model in a preview spot (character creation/selection), for a real 3D scene (no
    /// RenderTexture needed): assign an <see cref="anchor"/> Transform where the model should stand, frame it with
    /// the scene camera, and overlay the UI. Drag with the left mouse to rotate the model; optional idle spin.
    /// Spawned models are made VISUAL-ONLY (gameplay scripts + colliders disabled) so clicking the screen never
    /// triggers movement/combat. The lobby calls <see cref="Show"/> when the chosen template/gender changes.
    /// </summary>
    public class CharacterPreview : MonoBehaviour
    {
        [Tooltip("Where the model spawns (child of this, local pose reset). Place it where the character should stand.")]
        [SerializeField] private Transform anchor;
        [Tooltip("Idle Y-spin (deg/s) when not dragging. 0 = static.")]
        [SerializeField] private float spinSpeed = 0f;
        [Tooltip("Mouse-drag rotation sensitivity (degrees per pixel).")]
        [SerializeField] private float dragSensitivity = 0.25f;
        [Tooltip("Disable gameplay scripts/colliders on the spawned model so it's a pure visual (no click-to-attack).")]
        [SerializeField] private bool stripScripts = true;

        private GameObject _current;
        private GameObject _shownPrefab;

        private void Reset() => anchor = transform;

        /// <summary>Swap the displayed model (null clears it). Re-showing the same prefab is a no-op (no flicker).</summary>
        public void Show(GameObject prefab)
        {
            if (prefab == _shownPrefab) return;
            _shownPrefab = prefab;
            Clear();
            if (prefab == null) return;

            Transform parent = anchor != null ? anchor : transform;
            _current = Instantiate(prefab, parent);
            _current.transform.localPosition = Vector3.zero;
            _current.transform.localRotation = Quaternion.identity;
            if (stripScripts) MakeVisualOnly(_current);
        }

        public void Clear()
        {
            if (_current != null) Destroy(_current);
            _current = null;
        }

        /// <summary>Disable gameplay scripts + physics so the model only renders/animates. The Animator is a Behaviour
        /// (not a MonoBehaviour) so it stays on and idle anims keep playing.</summary>
        private static void MakeVisualOnly(GameObject go)
        {
            foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true)) mb.enabled = false;
            foreach (var rb in go.GetComponentsInChildren<Rigidbody>(true)) rb.isKinematic = true;
            foreach (var col in go.GetComponentsInChildren<Collider>(true)) col.enabled = false;
        }

        private void Update()
        {
            if (_current == null) return;
            var mouse = Mouse.current;
            bool dragging = mouse != null && mouse.leftButton.isPressed && !IsPointerOverUI();
            if (dragging)
            {
                float dx = mouse.delta.ReadValue().x;
                _current.transform.Rotate(0f, -dx * dragSensitivity, 0f, Space.World);
            }
            else if (spinSpeed != 0f)
            {
                _current.transform.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.World);
            }
        }

        private static bool IsPointerOverUI()
            => EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }
}
