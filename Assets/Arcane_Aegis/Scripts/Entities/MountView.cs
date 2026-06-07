namespace Arcane_Aegis.Entities
{
    /// <summary>
    /// View for a rideable, NON-combat entity (mirrors the server's <c>Mount</c>). Extends EntityView directly
    /// (no HumanoidView health bar) — mounts have no HP.
    /// </summary>
    public class MountView : EntityView
    {
    }
}
