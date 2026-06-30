using UnityEngine;
using ArcaneShared.Enums;
using Arcane_Aegis.Effects;

namespace Arcane_Aegis.Entities
{
    /// <summary>
    /// The "combatant" of an entity view (composition — mirrors the server's <c>Vitals</c> component): the
    /// world-space health bar + the death cue (play the death anim, hide the body, show it again on respawn).
    /// <see cref="EntityManager"/> adds one to ANY view that has vitals (player/npc/monster/boss/pet), so HP +
    /// death aren't baked into a base class — a non-humanoid (Monster/Pet) composes the exact same behaviour
    /// without inheriting from a "Humanoid". Wires itself to its <see cref="EntityView"/> on Awake; the view
    /// feeds it each snapshot via <see cref="OnSnapshot"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CombatantVitals : MonoBehaviour
    {
        [Tooltip("Fallback (s) se o clip de morte não for encontrado — senão o tempo SEGUE o tamanho do clip de morte.")]
        [SerializeField] private float deathHideDelay = 2f;
        [Tooltip("Buffer (s) somado ao tempo do clip de morte antes de dissolver/esconder (pausa na pose de morto).")]
        [SerializeField] private float deathDissolveBuffer = 0.15f;

        private EntityView _view;
        private EntityVitals _bar;          // world-space bar widget above the head (optional child)
        private EntityAnimation _anim;      // shared trigger-animation hub (death pose)
        private Renderer[] _renderers;
        private bool _wasDead;
        private bool _dissolvedAway;        // creature body was dissolved out on death (→ dissolve back in on revive)

        // Creatures (mob/boss/pet) dissolve away on death + back in on revive; players keep the plain hide/show.
        private bool DissolveOnDeath => _view != null && _view.Type is EntityType.Monster or EntityType.Boss or EntityType.Pet;

        private void Awake()
        {
            _view = GetComponent<EntityView>();
            if (_view != null) _view.AttachCombatant(this);
            _bar = GetComponentInChildren<EntityVitals>(true);
            _anim = EntityAnimation.Of(gameObject);
            _renderers = GetComponentsInChildren<Renderer>(true);
        }

        /// <summary>Fed by <see cref="EntityView.ApplySnapshot"/>: the quantized HP fraction + the dead flag.</summary>
        public void OnSnapshot(float hp01, bool dead)
        {
            if (_bar != null) _bar.SetHp01(hp01);
            if (dead != _wasDead) { _wasDead = dead; OnDeathChanged(dead); }
        }

        /// <summary>Show/hide the world bar (the local player hides its own — its HP is on the HUD).</summary>
        public void ShowWorldBar(bool show)
        {
            if (_bar != null) _bar.SetVisible(show);
        }

        // Death: play the death anim, then (creatures) dissolve the body away / (players) hide it after a delay.
        // Respawn: snap to spawn (no slide) + dissolve back in / show.
        private void OnDeathChanged(bool dead)
        {
            if (_anim != null) _anim.SetDead(dead);
            CancelInvoke(nameof(HideModel));
            CancelInvoke(nameof(DissolveAway));
            if (dead)
            {
                float delay = DeathAnimLength() + deathDissolveBuffer; // wait the actual death clip, not a guessed 2s
                if (DissolveOnDeath) Invoke(nameof(DissolveAway), delay);
                else Invoke(nameof(HideModel), delay);
            }
            else // revive
            {
                if (_view != null) _view.SnapToTarget();
                if (_dissolvedAway) { _dissolvedAway = false; DissolveEffect.PlayIn(gameObject); } // materialize back in
                else SetRenderers(true);
            }
        }

        private void DissolveAway() { _dissolvedAway = true; DissolveEffect.PlayDeath(gameObject); }
        private void HideModel() => SetRenderers(false);

        // Length (s) of the model's death clip, so the dissolve/hide waits for the WHOLE animation. Falls back to
        // deathHideDelay if no death clip is found on the controller. Cached after the first lookup.
        private float _deathLen = -1f;
        private float DeathAnimLength()
        {
            if (_deathLen >= 0f) return _deathLen;
            _deathLen = deathHideDelay; // fallback
            var anim = GetComponentInChildren<Animator>();
            var rac = anim != null ? anim.runtimeAnimatorController : null;
            if (rac != null)
                foreach (var c in rac.animationClips)
                {
                    if (c == null) continue;
                    string n = c.name.ToLowerInvariant();
                    // Specific enough to not match "diet"/"deadly"/"idle": "death"/"dying" anywhere, or a clip that IS die/dead.
                    if (n.Contains("death") || n.Contains("dying") || n == "die" || n == "dead"
                        || n.EndsWith("_die") || n.EndsWith("_dead") || n.EndsWith("|die") || n.EndsWith("|dead"))
                    { _deathLen = Mathf.Max(0.1f, c.length); break; }
                }
            return _deathLen;
        }

        private void SetRenderers(bool on)
        {
            if (_renderers == null) return;
            for (int i = 0; i < _renderers.Length; i++)
                if (_renderers[i] != null) _renderers[i].enabled = on;
        }
    }
}
