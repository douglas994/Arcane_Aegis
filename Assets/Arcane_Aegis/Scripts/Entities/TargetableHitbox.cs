using UnityEngine;

namespace Arcane_Aegis.Entities
{
    /// <summary>Marks a collider as a combat target. <see cref="EntityManager"/> attaches one (on a trigger capsule
    /// child, on the <c>Targetable</c> layer) to every remote combatant; the <c>TargetingSystem</c> raycasts that
    /// layer and reads the <see cref="View"/> (→ entity id) off whatever it hits. A trigger so it never blocks the
    /// player's movement — raycasts still hit it via <c>QueryTriggerInteraction.Collide</c>.</summary>
    public sealed class TargetableHitbox : MonoBehaviour
    {
        public EntityView View { get; private set; }
        public void Bind(EntityView view) => View = view;
    }
}
