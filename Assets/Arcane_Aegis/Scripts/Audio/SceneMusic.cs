using UnityEngine;

namespace Arcane_Aegis.Audio
{
    /// <summary>
    /// Drop this on a GameObject in a scene (menu, lobby, a zone) and assign a track — it plays through the
    /// <see cref="AudioManager"/> when the scene starts, so music survives scene loads via the persistent manager
    /// instead of a per-scene AudioSource. Call <see cref="Play"/>/<see cref="Stop"/> from elsewhere to switch tracks
    /// (e.g. enter combat). One responsibility: pick the track for this scene; the manager owns playback/volume.
    /// </summary>
    public sealed class SceneMusic : MonoBehaviour
    {
        [Tooltip("Faixa tocada nesta cena. Vazio = nada (ou use Stop pra silenciar).")]
        [SerializeField] private AudioClip track;
        [SerializeField] private bool loop = true;
        [Range(0f, 1f)] [SerializeField] private float volume = 1f;
        [Tooltip("Tocar automaticamente quando a cena começa.")]
        [SerializeField] private bool playOnStart = true;
        [Tooltip("Parar a música ao destruir este objeto (ex.: sair da cena de menu). Deixe off pra deixar a faixa seguir.")]
        [SerializeField] private bool stopOnDestroy = false;

        private void Start() { if (playOnStart) Play(); }

        /// <summary>Play this scene's track (swaps whatever is currently playing).</summary>
        public void Play() => AudioManager.Ensure().PlayMusic(track, loop, volume);

        /// <summary>Stop the background music.</summary>
        public void Stop() => AudioManager.Instance?.StopMusic();

        private void OnDestroy() { if (stopOnDestroy) AudioManager.Instance?.StopMusic(); }
    }
}
