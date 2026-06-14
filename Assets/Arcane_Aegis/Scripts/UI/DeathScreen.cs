using UnityEngine;
using TMPro;
using Arcane_Aegis.Entities;

namespace Arcane_Aegis.UI
{
    /// <summary>
    /// Shows a "you died" overlay while the local player is dead, with a respawn countdown, and hides it on respawn.
    /// Server-authoritative: death/respawn come from the player's HP (0 = dead) via S2C_StateUpdate; this is just the
    /// overlay. Put it on a full-screen panel (assign <see cref="panel"/>) under the HUD canvas.
    /// </summary>
    public sealed class DeathScreen : MonoBehaviour
    {
        [SerializeField] private GameObject panel;       // the overlay root (toggled on/off)
        [SerializeField] private TMP_Text message;       // optional: countdown text
        [SerializeField] private float respawnSeconds = 5f; // matches server CombatRules.RespawnSeconds

        private EntityManager _entities;
        private bool _dead;
        private float _diedAt;

        private void Awake()
        {
            _entities = FindAnyObjectByType<EntityManager>();
            if (panel != null) panel.SetActive(false);
        }

        private void Update()
        {
            if (_entities == null) _entities = FindAnyObjectByType<EntityManager>();
            var local = _entities != null ? _entities.Local : null;
            if (local == null) { Set(false); return; }

            bool dead = local.MaxHp > 0 && local.Hp <= 0;
            if (dead && !_dead) _diedAt = Time.time;   // entered death
            if (dead != _dead) Set(dead);

            if (_dead && message != null)
            {
                float left = Mathf.Max(0f, respawnSeconds - (Time.time - _diedAt));
                message.text = left > 0.1f ? $"Você morreu\nReaparecendo em {left:0}s" : "Reaparecendo…";
            }
        }

        private void Set(bool dead)
        {
            _dead = dead;
            if (panel != null) panel.SetActive(dead);
        }
    }
}
