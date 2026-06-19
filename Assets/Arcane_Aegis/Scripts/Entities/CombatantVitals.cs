using UnityEngine;

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
        [SerializeField] private float deathHideDelay = 2f; // let the death anim play before the body hides

        private EntityView _view;
        private EntityVitals _bar;          // world-space bar widget above the head (optional child)
        private EntityAnimation _anim;      // shared trigger-animation hub (death pose)
        private Renderer[] _renderers;
        private bool _wasDead;

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

        // Death: play the death anim, then hide the body after a delay. Respawn: snap to spawn (no slide) + show.
        private void OnDeathChanged(bool dead)
        {
            if (_anim != null) _anim.SetDead(dead);
            CancelInvoke(nameof(HideModel));
            if (dead) Invoke(nameof(HideModel), deathHideDelay);
            else { if (_view != null) _view.SnapToTarget(); SetRenderers(true); }
        }

        private void HideModel() => SetRenderers(false);

        private void SetRenderers(bool on)
        {
            if (_renderers == null) return;
            for (int i = 0; i < _renderers.Length; i++)
                if (_renderers[i] != null) _renderers[i].enabled = on;
        }
    }
}
