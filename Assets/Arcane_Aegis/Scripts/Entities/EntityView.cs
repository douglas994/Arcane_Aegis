using System.Collections.Generic;
using UnityEngine;
using ArcaneShared.Enums;
using ArcaneShared.Models;
using Arcane_Aegis.Controllers;

namespace Arcane_Aegis.Entities
{
    /// <summary>
    /// Base view for ANY networked entity (mirrors the server's Entity). Owns transform sync: smoothly
    /// follows the latest snapshot (SmoothDamp) and feeds the CharacterAnimator the state + a stable
    /// networked speed. Locally-controlled entities turn interpolation OFF (their own controller drives).
    /// </summary>
    public class EntityView : MonoBehaviour
    {
        public ushort Id;
        public EntityType Type;

        [SerializeField] private float positionSmoothTime = 0.1f;
        [SerializeField] private float rotationLerp = 12f;
        [SerializeField] private float speedWindow = 0.25f;
        [SerializeField] protected CharacterAnimator characterAnimator;

        private Vector3 _targetPos;
        private float _targetYaw;
        private Vector3 _vel;                 // SmoothDamp state
        private bool _interpolate;            // OFF by default; remote players turn it on in Initialize
                                             // (so a mis-set local player is never dragged to the origin)
        private readonly Queue<(double time, Vector3 pos)> _hist = new();

        protected virtual void Awake()
        {
            if (characterAnimator == null) characterAnimator = GetComponentInChildren<CharacterAnimator>();
        }

        /// <summary>Applies a snapshot. Base = transform target + anim state + speed. Subclasses extend (HP, …).</summary>
        public virtual void ApplySnapshot(in SnapshotEntry e)
        {
            SetTarget(new Vector3(e.Position.X, e.Position.Y, e.Position.Z), e.Yaw, e.State);
        }

        /// <summary>Local players drive their own transform (KCC), so interpolation is turned off for them.</summary>
        public void SetInterpolated(bool on) => _interpolate = on;

        /// <summary>Updates the follow target, animation state, and windowed networked speed.</summary>
        public void SetTarget(Vector3 position, float yaw, MovementState state)
        {
            _targetPos = position;
            _targetYaw = yaw;

            // Stable speed = straight-line distance across the window / its duration (cancels the
            // snapshot-vs-movement-rate beat that would otherwise flicker the blend tree).
            double now = Time.timeAsDouble;
            _hist.Enqueue((now, position));
            while (_hist.Count > 2 && now - _hist.Peek().time > speedWindow) _hist.Dequeue();

            if (characterAnimator != null)
            {
                if (_hist.Count >= 2)
                {
                    var oldest = _hist.Peek();
                    double dt = now - oldest.time;
                    if (dt > 1e-3)
                    {
                        Vector3 d = position - oldest.pos;
                        d.y = 0f;
                        characterAnimator.SourceSpeed = d.magnitude / (float)dt;
                    }
                }
                characterAnimator.State = state;
            }
        }

        /// <summary>Snaps instantly and clears history (on spawn).</summary>
        public void Teleport(Vector3 position, float yaw)
        {
            _targetPos = position;
            _targetYaw = yaw;
            _vel = Vector3.zero;
            _hist.Clear();
            transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
        }

        private void Update()
        {
            if (!_interpolate) return;
            transform.position = Vector3.SmoothDamp(transform.position, _targetPos, ref _vel, positionSmoothTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(0f, _targetYaw, 0f), Time.deltaTime * rotationLerp);
        }
    }
}
