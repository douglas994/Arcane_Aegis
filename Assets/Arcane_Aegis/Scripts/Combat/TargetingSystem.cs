using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using Arcane_Aegis.Entities;
using Arcane_Aegis.UI;

namespace Arcane_Aegis.Combat
{
    /// <summary>
    /// Hybrid targeting for the local player (the design's action + tab combat). A HARD target is locked by Tab
    /// (cycle the nearest enemies) or left-click; when none is locked, an action-aim SOFT target is read from a
    /// center-screen raycast under the crosshair. <see cref="CurrentTargetId"/> is what <c>PlayerCombat</c> sends
    /// with each cast (0 = pure action aim → the server uses our facing). The server stays authoritative — this
    /// only chooses an id. Tab/click/aim hit the <c>Targetable</c>-layer hitboxes added by <see cref="EntityManager"/>.
    /// </summary>
    public sealed class TargetingSystem : MonoBehaviour
    {
        [SerializeField] private Camera cam;                 // defaults to Camera.main
        [SerializeField] private float maxTargetRange = 40f; // tab/aim acquisition range (m)
        [SerializeField] private float clickRange = 60f;     // click-to-select ray length + keep-lock range (m)

        /// <summary>The id the player acts on this frame: the hard lock if valid, else the soft crosshair target. 0 = none.</summary>
        public ushort CurrentTargetId { get; private set; }
        /// <summary>The explicitly locked target (Tab/click); 0 if only soft-aiming.</summary>
        public ushort HardTargetId { get; private set; }

        private EntityManager _entities;
        private int _targetableMask;
        private readonly List<EntityView> _candidates = new();
        private int _cycleIndex = -1;

        private void Awake()
        {
            _entities = FindAnyObjectByType<EntityManager>();
            int layer = LayerMask.NameToLayer("Targetable");
            _targetableMask = layer >= 0 ? (1 << layer) : ~0; // fall back to everything if the layer is missing
        }

        private void Update()
        {
            if (cam == null) cam = Camera.main;
            if (_entities == null) { _entities = FindAnyObjectByType<EntityManager>(); if (_entities == null) return; }

            bool typing = UiFocus.IsTyping;
            var kb = Keyboard.current;
            if (!typing && kb != null)
            {
                if (kb.escapeKey.wasPressedThisFrame) ClearHard();   // Esc drops the lock
                if (kb.tabKey.wasPressedThisFrame) CycleHard();      // Tab cycles nearest → farthest enemies
            }

            var mouse = Mouse.current;
            if (!typing && mouse != null && mouse.leftButton.wasPressedThisFrame && !IsPointerOverUI())
                TryClickSelect(mouse.position.ReadValue());

            ResolveCurrent();
        }

        // Keep the hard lock while it's valid; otherwise fall back to the soft crosshair target.
        private void ResolveCurrent()
        {
            if (HardTargetId != 0 && IsValidTarget(HardTargetId)) { CurrentTargetId = HardTargetId; return; }
            HardTargetId = 0; // died / despawned / out of range
            CurrentTargetId = SoftAimTarget();
        }

        // Center-screen raycast (action-aim assist): snaps single-target casts to whatever's under the crosshair.
        private ushort SoftAimTarget()
        {
            if (cam == null) return 0;
            Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            if (Physics.Raycast(ray, out var hit, maxTargetRange, _targetableMask, QueryTriggerInteraction.Collide))
            {
                var hb = hit.collider.GetComponentInParent<TargetableHitbox>();
                if (hb != null && hb.View != null && IsAlive(hb.View)) return hb.View.Id;
            }
            return 0;
        }

        private void TryClickSelect(Vector2 screenPos)
        {
            if (cam == null) return;
            Ray ray = cam.ScreenPointToRay(screenPos);
            if (Physics.Raycast(ray, out var hit, clickRange, _targetableMask, QueryTriggerInteraction.Collide))
            {
                var hb = hit.collider.GetComponentInParent<TargetableHitbox>();
                if (hb != null && hb.View != null && IsAlive(hb.View)) HardTargetId = hb.View.Id;
            }
        }

        private void CycleHard()
        {
            Vector3 center = Origin();
            _entities.CollectTargetables(_candidates, center, maxTargetRange, enemiesOnly: true);
            if (_candidates.Count == 0) { _cycleIndex = -1; HardTargetId = 0; return; }

            _candidates.Sort((a, b) =>
                (a.transform.position - center).sqrMagnitude.CompareTo((b.transform.position - center).sqrMagnitude));

            _cycleIndex = (_cycleIndex + 1) % _candidates.Count;
            HardTargetId = _candidates[_cycleIndex].Id;
        }

        private bool IsValidTarget(ushort id)
        {
            if (!_entities.TryGetView(id, out var v) || !IsAlive(v)) return false;
            var self = _entities.Local;
            if (self == null) return true;
            return (v.transform.position - self.transform.position).sqrMagnitude <= clickRange * clickRange;
        }

        private Vector3 Origin()
        {
            var self = _entities.Local;
            if (self != null) return self.transform.position;
            return cam != null ? cam.transform.position : transform.position;
        }

        private static bool IsAlive(EntityView v) => v != null && v.SnapHp01 > 0f;

        private void ClearHard() { HardTargetId = 0; _cycleIndex = -1; }

        private static bool IsPointerOverUI()
            => EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }
}
