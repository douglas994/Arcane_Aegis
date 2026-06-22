using System.Collections;
using UnityEngine;

namespace Arcane_Aegis.Combat
{
    /// <summary>
    /// Camera juice: trauma-based screen shake + a brief hit-stop (time freeze) on impacts. Runs AFTER the camera rig
    /// (high execution order) so it ADDS a transient offset on top of the rig's position each frame — the rig recomputes
    /// from scratch, so the offset never drifts. Self-bootstraps onto the main camera via <see cref="Request"/>/<see cref="Stop"/>,
    /// so no manual setup is needed. Uses UNSCALED time so the shake keeps animating during a hit-stop.
    /// </summary>
    [DefaultExecutionOrder(300)]
    public sealed class CameraShake : MonoBehaviour
    {
        public static CameraShake Instance { get; private set; }

        [SerializeField] private float traumaDecay = 1.8f;  // trauma units per second
        [SerializeField] private float maxOffset = 0.35f;   // metres at full trauma
        [SerializeField] private float maxRoll = 2.5f;      // degrees at full trauma
        [SerializeField] private float frequency = 26f;     // noise speed

        private float _trauma;
        private const float Seed = 17.3f;

        private void Awake() { if (Instance == null) Instance = this; }
        private void OnDestroy() { if (Instance == this) Instance = null; }

        /// <summary>Add trauma (0..1). Lazily attaches to the main camera the first time it's called.</summary>
        public static void Request(float amount)
        {
            if (Instance == null)
            {
                var cam = Camera.main; // bind only to the tagged gameplay camera — never an arbitrary (UI/minimap) one
                if (cam == null) return; // no main camera yet → skip this shake; bind on a later event
                Instance = cam.gameObject.GetComponent<CameraShake>() ?? cam.gameObject.AddComponent<CameraShake>();
            }
            Instance._trauma = Mathf.Clamp01(Instance._trauma + amount);
        }

        /// <summary>Brief near-freeze on impact (game-feel). Uses realtime so it self-restores. Scale 0.05 (not 0) so the
        /// restore guard never collides with a hard pause set elsewhere (e.g. the disconnect screen freezes at exactly 0).</summary>
        public static void Stop(float seconds, float scale = 0.05f)
        {
            if (Instance == null) Request(0f); // bootstrap a runner
            if (Instance != null) Instance.RunHitStop(seconds, scale);
        }

        private void RunHitStop(float seconds, float scale)
        {
            StopCoroutine(nameof(HitStopCo));
            StartCoroutine(HitStopCo(seconds, scale));
        }

        private IEnumerator HitStopCo(float seconds, float scale)
        {
            Time.timeScale = scale;
            yield return new WaitForSecondsRealtime(seconds);
            // Don't stomp a hard pause set elsewhere (e.g. the disconnect screen freezes the game at 0).
            if (Time.timeScale == scale) Time.timeScale = 1f;
        }

        private void LateUpdate()
        {
            if (_trauma <= 0f) return;
            float shake = _trauma * _trauma; // quadratic ramp feels punchier
            float t = Time.unscaledTime * frequency;
            float ox = (Mathf.PerlinNoise(Seed, t) - 0.5f) * 2f;
            float oy = (Mathf.PerlinNoise(Seed + 1f, t) - 0.5f) * 2f;
            float roll = (Mathf.PerlinNoise(Seed + 2f, t) - 0.5f) * 2f;

            transform.position += (transform.right * (ox * maxOffset * shake)) + (transform.up * (oy * maxOffset * shake));
            transform.rotation *= Quaternion.Euler(0f, 0f, roll * maxRoll * shake);

            _trauma = Mathf.Max(0f, _trauma - traumaDecay * Time.unscaledDeltaTime);
        }
    }
}
