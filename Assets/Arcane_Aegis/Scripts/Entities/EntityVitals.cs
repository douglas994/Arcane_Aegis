using UnityEngine;

namespace Arcane_Aegis.Entities
{
    /// <summary>
    /// World-space health bar for OTHER players / mobs (the local player's HP goes on the HUD).
    /// IMPORTANT: the billboard rotates only the assigned <see cref="bar"/> child — NEVER the object this
    /// component sits on — so putting EntityVitals on the player root can't tilt the player.
    /// Assign <see cref="bar"/> (the visual that should face the camera), <see cref="fill"/> and
    /// <see cref="root"/>. If <see cref="bar"/> is null, nothing is rotated.
    /// </summary>
    public class EntityVitals : MonoBehaviour
    {
        [SerializeField] private Transform bar;   // bar visual child to face the camera (NEVER the player root)
        [SerializeField] private Transform fill;  // child whose localScale.x is set 0..1
        [SerializeField] private GameObject root; // bar root, toggled by SetVisible

        private Camera _cam;

        public void SetHp01(float value)
        {
            if (fill == null) return;
            Vector3 s = fill.localScale;
            s.x = Mathf.Clamp01(value);
            fill.localScale = s;
        }

        public void SetVisible(bool show)
        {
            if (root != null) root.SetActive(show);
        }

        private void LateUpdate()
        {
            if (bar == null) return;                 // nothing to billboard → never rotates the player
            if (_cam == null) _cam = Camera.main;
            if (_cam != null) bar.forward = _cam.transform.forward;
        }
    }
}
