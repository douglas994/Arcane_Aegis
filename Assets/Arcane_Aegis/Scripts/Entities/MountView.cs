namespace Arcane_Aegis.Entities
{
    /// <summary>
    /// View for a rideable, NON-combat entity (mirrors the server's <c>Mount</c>). Extends EntityView directly
    /// (no HumanoidView health bar) — mounts have no HP.
    /// </summary>
    public class MountView : EntityView
    {
        /// <summary>The entity id of the rider sitting on this mount (from S2C_SpawnEntity.RiderId; 0 = none).
        /// The EntityManager uses it to parent the rider onto the mount's seat.</summary>
        public ushort RiderId;
    }
}
