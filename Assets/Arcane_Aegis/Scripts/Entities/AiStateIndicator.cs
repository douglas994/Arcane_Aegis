using UnityEngine;
using ArcaneShared.Enums;

namespace Arcane_Aegis.Entities
{
    /// <summary>
    /// World-space cue above a monster showing its AI/FSM state (replicated in the snapshot): a "!" the moment it
    /// spots you (Alert), "Zzz" while it sleeps, "?" while it investigates your last-known spot (Search). Every other
    /// state shows nothing. Composition (like <see cref="CombatantVitals"/>): <see cref="EntityManager"/> adds one to
    /// monster/boss views and the view feeds it each snapshot via <see cref="OnAiState"/>.
    /// Assign your OWN art to the three icon slots to style it (each is toggled active/inactive). If all are empty a
    /// plain billboarded TextMesh is created at runtime as a dev fallback, so the cue works without any art.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AiStateIndicator : MonoBehaviour
    {
        [Tooltip("Altura acima do pivô onde o aviso aparece (m).")]
        [SerializeField] private float height = 2.2f;

        [Header("Arte opcional (vazio = fallback de texto)")]
        [Tooltip("Mostrado em ALERTA (acabou de te ver). Vazio = '!' de texto.")]
        [SerializeField] private GameObject alertIcon;
        [Tooltip("Mostrado DORMINDO. Vazio = 'Zzz' de texto.")]
        [SerializeField] private GameObject sleepIcon;
        [Tooltip("Mostrado INVESTIGANDO (te perdeu). Vazio = '?' de texto.")]
        [SerializeField] private GameObject searchIcon;

        private EntityView _view;
        private Camera _cam;
        private AiState _state = AiState.Idle;
        private Transform _active; // the cue currently shown (custom art or fallback text) → billboarded each frame

        private TextMesh _text;    // lazy dev fallback
        private Transform _textTf;

        private void Awake()
        {
            _view = GetComponent<EntityView>();
            if (_view != null) _view.AttachIndicator(this);
            HideAll();
        }

        /// <summary>Fed by <see cref="EntityView.ApplySnapshot"/> — swaps the cue when the FSM state changes.</summary>
        public void OnAiState(AiState state)
        {
            if (state == _state) return;
            _state = state;
            HideAll();
            switch (_state)
            {
                case AiState.Alert:  Show(alertIcon, "!", new Color(1f, 0.25f, 0.2f)); break;
                case AiState.Sleep:  Show(sleepIcon, "Zzz", new Color(0.6f, 0.8f, 1f)); break;
                case AiState.Search: Show(searchIcon, "?", new Color(1f, 0.85f, 0.3f)); break;
                // every other state: no cue
            }
        }

        private void Show(GameObject art, string fallback, Color color)
        {
            if (art != null) { art.SetActive(true); _active = art.transform; return; }
            EnsureText();
            _text.text = fallback;
            _text.color = color;
            _textTf.gameObject.SetActive(true);
            _active = _textTf;
        }

        private void HideAll()
        {
            if (alertIcon != null) alertIcon.SetActive(false);
            if (sleepIcon != null) sleepIcon.SetActive(false);
            if (searchIcon != null) searchIcon.SetActive(false);
            if (_textTf != null) _textTf.gameObject.SetActive(false);
            _active = null;
        }

        private void EnsureText()
        {
            if (_text != null) return;
            var go = new GameObject("AiCue");
            _textTf = go.transform;
            _textTf.SetParent(transform, false);
            _textTf.localPosition = new Vector3(0f, height, 0f);

            _text = go.AddComponent<TextMesh>();
            _text.anchor = TextAnchor.LowerCenter;
            _text.alignment = TextAlignment.Center;
            _text.fontSize = 64;
            _text.characterSize = 0.05f;
            _text.fontStyle = FontStyle.Bold;

            // Unity 6 dropped the implicit built-in Arial → assign the legacy runtime font + its material explicitly.
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font != null)
            {
                _text.font = font;
                var mr = go.GetComponent<MeshRenderer>();
                if (mr != null) mr.sharedMaterial = font.material;
            }
        }

        private void LateUpdate()
        {
            if (_active == null) return;
            if (_cam == null) _cam = Camera.main;
            if (_cam != null) _active.forward = _cam.transform.forward; // face the camera (only the cue, never the body)
        }
    }
}
