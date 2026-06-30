using UnityEngine;

namespace Arcane_Aegis.Entities
{
    /// <summary>
    /// Receives Animation Events from the attack/skill clips so a skill's VFX fires on the EXACT frame the blade passes
    /// (the pro way — frame-accurate, not a guessed timer). Put it on the MODEL (the GameObject that has the Animator),
    /// because Unity calls animation-event methods on a component of the Animator's GameObject. On the attack clip, add
    /// an Animation Event at the apex frame calling <see cref="Release"/> → it spawns the CURRENT skill's slash via the
    /// owning <see cref="EntityAnimation"/>. Skills with <c>useAnimEvent</c> off keep using their releaseTime instead.
    /// </summary>
    public sealed class CombatAnimEvents : MonoBehaviour
    {
        private EntityAnimation _owner;
        private EntityAnimation Owner => _owner != null ? _owner : (_owner = GetComponentInParent<EntityAnimation>(true));

        /// <summary>Animation Event: fire the current skill's slash VFX NOW. Place this event at the frame the blade passes.</summary>
        public void Release() { if (Owner != null) Owner.FireSlashNow(); }
    }
}
