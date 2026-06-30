using UnityEngine;

namespace Arcane_Aegis.Audio
{
    /// <summary>
    /// One-stop sound playback: a pool of AudioSources for 3D one-shot SFX in the world (combat hits, casts, pickups…)
    /// plus a single music track. Auto-creates itself (no scene setup, like CombatFx) so any system can call
    /// <c>AudioManager.Instance?.PlaySfx3D(clip, pos)</c>. Drop one in the scene instead to tune the pool/volumes via
    /// serialized fields. Round-robins free sources so overlapping sounds don't cut each other; randomizes pitch so
    /// repeats don't sound robotic. Volumes are simple globals here — a proper mixer/settings menu comes later.
    /// </summary>
    public sealed class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("SFX pool")]
        [Tooltip("Quantas vozes simultâneas (sons que tocam ao mesmo tempo).")]
        [SerializeField] private int poolSize = 16;
        [Range(0f, 1f)] [SerializeField] private float sfxVolume = 1f;
        [Tooltip("Distância (m) onde o som 3D começa a atenuar / fica inaudível.")]
        [SerializeField] private float minDistance = 4f, maxDistance = 45f;

        [Header("Música")]
        [Range(0f, 1f)] [SerializeField] private float musicVolume = 0.5f;

        private AudioSource[] _pool;
        private int _next;
        private AudioSource _music;
        private float _musicTrackVolume = 1f; // the per-track volume of what's playing (so changing MusicVolume rescales it)

        private const string PrefSfx = "audio.sfx", PrefMusic = "audio.music";

        /// <summary>Global SFX volume (0..1). Persisted; bind a settings slider to it.</summary>
        public float SfxVolume
        {
            get => sfxVolume;
            set { sfxVolume = Mathf.Clamp01(value); Save(); }
        }

        /// <summary>Global music volume (0..1). Persisted; applied live to the current track.</summary>
        public float MusicVolume
        {
            get => musicVolume;
            set { musicVolume = Mathf.Clamp01(value); if (_music != null) _music.volume = musicVolume * _musicTrackVolume; Save(); }
        }

        private void LoadPrefs()
        {
            sfxVolume = PlayerPrefs.GetFloat(PrefSfx, sfxVolume);
            musicVolume = PlayerPrefs.GetFloat(PrefMusic, musicVolume);
        }

        private void Save()
        {
            PlayerPrefs.SetFloat(PrefSfx, sfxVolume);
            PlayerPrefs.SetFloat(PrefMusic, musicVolume);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap() => Ensure();

        /// <summary>The live AudioManager, creating it on demand — so a caller in an early Start() never gets null.</summary>
        public static AudioManager Ensure()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("AudioManager");
            DontDestroyOnLoad(go);
            return go.AddComponent<AudioManager>(); // Awake sets Instance synchronously
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadPrefs();
            BuildPool();
        }
        private void OnDestroy() { if (Instance == this) Instance = null; }

        private void BuildPool()
        {
            _pool = new AudioSource[Mathf.Max(1, poolSize)];
            for (int i = 0; i < _pool.Length; i++)
            {
                var child = new GameObject($"Sfx{i}");
                child.transform.SetParent(transform, false);
                var src = child.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialBlend = 1f;                 // 3D by default
                src.rolloffMode = AudioRolloffMode.Linear;
                src.minDistance = minDistance;
                src.maxDistance = maxDistance;
                _pool[i] = src;
            }
        }

        /// <summary>3D one-shot at a world position. <paramref name="pitchVar"/> randomizes pitch ±var so repeated hits
        /// don't sound identical. No-op if the clip is null (so unauthored skills are silent, not an error).</summary>
        public void PlaySfx3D(AudioClip clip, Vector3 pos, float volume = 1f, float pitchVar = 0.06f)
        {
            if (clip == null || _pool == null) return;
            var src = NextFree();
            src.transform.position = pos;
            src.spatialBlend = 1f;
            src.clip = clip;
            src.volume = sfxVolume * Mathf.Clamp01(volume);
            src.pitch = 1f + Random.Range(-pitchVar, pitchVar);
            src.Play();
        }

        /// <summary>2D one-shot (UI/feedback) — no spatialization, full volume regardless of position.</summary>
        public void PlaySfx2D(AudioClip clip, float volume = 1f)
        {
            if (clip == null || _pool == null) return;
            var src = NextFree();
            src.spatialBlend = 0f;
            src.clip = clip;
            src.volume = sfxVolume * Mathf.Clamp01(volume);
            src.pitch = 1f;
            src.Play();
        }

        // Prefer a source that isn't currently playing; if all are busy, steal the next in round-robin order.
        private AudioSource NextFree()
        {
            for (int i = 0; i < _pool.Length; i++)
            {
                var s = _pool[(_next + i) % _pool.Length];
                if (!s.isPlaying) { _next = (_next + i + 1) % _pool.Length; return s; }
            }
            var pick = _pool[_next];
            _next = (_next + 1) % _pool.Length;
            return pick;
        }

        /// <summary>Plays (or swaps) the background music. Null clip stops it.</summary>
        public void PlayMusic(AudioClip clip, bool loop = true, float volume = 1f)
        {
            if (_music == null)
            {
                var go = new GameObject("Music");
                go.transform.SetParent(transform, false);
                _music = go.AddComponent<AudioSource>();
                _music.playOnAwake = false;
                _music.spatialBlend = 0f;
            }
            if (clip == null) { _music.Stop(); return; }
            _musicTrackVolume = Mathf.Clamp01(volume);
            _music.clip = clip;
            _music.loop = loop;
            _music.volume = musicVolume * _musicTrackVolume;
            _music.Play();
        }

        public void StopMusic() { if (_music != null) _music.Stop(); }
    }
}
