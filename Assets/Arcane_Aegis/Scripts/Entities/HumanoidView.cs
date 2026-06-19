namespace Arcane_Aegis.Entities
{
    /// <summary>
    /// A humanoid-form entity view (mirrors the server's <c>Humanoid</c>): the base for the player + NPC views.
    /// Carries NO combat logic — vitals (HP bar + death) are the <see cref="CombatantVitals"/> COMPONENT, so a
    /// non-humanoid combatant (Monster/Pet) composes the exact same behaviour without inheriting "Humanoid".
    /// The own player's exact vitals/XP live on <see cref="PlayerView"/> (the only one that uses them).
    /// </summary>
    public class HumanoidView : EntityView
    {
    }
}
