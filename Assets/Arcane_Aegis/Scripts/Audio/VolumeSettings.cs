using UnityEngine;
using UnityEngine.UI;

namespace Arcane_Aegis.Audio
{
    /// <summary>
    /// Binds a hand-built settings panel's volume sliders to the <see cref="AudioManager"/>: loads the saved values
    /// into the sliders on open, writes (and persists) as you drag. You build the panel + sliders by hand and just
    /// drag them into the fields here. Sliders should range 0..1. Optional: a short clip previews the SFX level as you
    /// drag (throttled so it doesn't machine-gun). One responsibility: wire UI ↔ AudioManager.
    /// </summary>
    public sealed class VolumeSettings : MonoBehaviour
    {
        [SerializeField] private Slider sfxSlider;
        [SerializeField] private Slider musicSlider;
        [Tooltip("Som curto tocado enquanto arrasta o slider de SFX, pra ouvir o nível. Opcional.")]
        [SerializeField] private AudioClip sfxPreview;

        private float _lastPreview;

        private void OnEnable()
        {
            var am = AudioManager.Ensure();
            if (sfxSlider != null)
            {
                sfxSlider.SetValueWithoutNotify(am.SfxVolume);
                sfxSlider.onValueChanged.AddListener(OnSfxChanged);
            }
            if (musicSlider != null)
            {
                musicSlider.SetValueWithoutNotify(am.MusicVolume);
                musicSlider.onValueChanged.AddListener(OnMusicChanged);
            }
        }

        private void OnDisable()
        {
            if (sfxSlider != null) sfxSlider.onValueChanged.RemoveListener(OnSfxChanged);
            if (musicSlider != null) musicSlider.onValueChanged.RemoveListener(OnMusicChanged);
        }

        private void OnSfxChanged(float v)
        {
            AudioManager.Ensure().SfxVolume = v;
            // Throttle the audible preview so dragging doesn't spam the clip every frame.
            if (sfxPreview != null && Time.unscaledTime - _lastPreview > 0.15f)
            {
                _lastPreview = Time.unscaledTime;
                AudioManager.Ensure().PlaySfx2D(sfxPreview);
            }
        }

        private void OnMusicChanged(float v) => AudioManager.Ensure().MusicVolume = v;
    }
}
