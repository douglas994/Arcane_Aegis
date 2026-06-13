using UnityEngine;
using TMPro;

namespace Arcane_Aegis.UI
{
    /// <summary>
    /// A transient on-screen message (e.g. "Requer Nível 50"). Singleton: handlers call <see cref="Show"/>. Put this on
    /// an ALWAYS-ACTIVE root (so Update can re-hide it), assign a <see cref="panel"/> child it toggles + a label.
    /// </summary>
    public sealed class Toast : MonoBehaviour
    {
        public static Toast Instance { get; private set; }

        [Tooltip("The child panel toggled on/off (keep THIS root object always active).")]
        [SerializeField] private GameObject panel;
        [SerializeField] private TMP_Text label;
        [Tooltip("Seconds the message stays up.")]
        [SerializeField] private float duration = 2.5f;

        private float _hideAt;

        private void Awake()
        {
            Instance = this;
            if (panel != null) panel.SetActive(false);
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        public void Show(string message)
        {
            if (label != null) label.text = message;
            if (panel != null) panel.SetActive(true);
            _hideAt = Time.unscaledTime + duration;
        }

        private void Update()
        {
            if (panel != null && panel.activeSelf && Time.unscaledTime >= _hideAt) panel.SetActive(false);
        }
    }
}
