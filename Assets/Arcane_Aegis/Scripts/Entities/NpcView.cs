namespace Arcane_Aegis.Entities
{
    /// <summary>
    /// View for a non-player humanoid (mirrors the server's <c>Npc : Humanoid</c>). HP bar + death come from the
    /// <see cref="CombatantVitals"/> component. Remote-only (no local control stack).
    /// </summary>
    public class NpcView : HumanoidView
    {
    }
}
