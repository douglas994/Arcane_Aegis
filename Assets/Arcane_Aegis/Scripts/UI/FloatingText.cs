using UnityEngine;

namespace Arcane_Aegis.UI
{
    /// <summary>Rises, fades and self-destroys; billboards to the camera. Attached to a runtime TextMesh.</summary>
    public class FloatingText : MonoBehaviour
    {
        [SerializeField] private float lifetime = 0.9f;
        [SerializeField] private float riseSpeed = 1.6f;

        private float _t;
        private TextMesh _text;
        private Camera _cam;

        private void Awake() => _text = GetComponent<TextMesh>();

        private void Update()
        {
            _t += Time.deltaTime;
            transform.position += Vector3.up * (riseSpeed * Time.deltaTime);

            if (_cam == null) _cam = Camera.main;
            if (_cam != null) transform.forward = _cam.transform.forward;

            if (_text != null)
            {
                Color c = _text.color;
                c.a = Mathf.Clamp01(1f - _t / lifetime);
                _text.color = c;
            }

            if (_t >= lifetime) Destroy(gameObject);
        }
    }
}
