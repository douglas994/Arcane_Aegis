namespace Arcane_Aegis.Entities
{
    /// <summary>View for a hostile mob (mirrors the server's <c>Monster : Entity</c> — NOT a humanoid). Its HP bar +
    /// death come from the <see cref="CombatantVitals"/> component (composition), not a base class. Hook
    /// nameplate/aggro FX here later.</summary>
    public class MonsterView : EntityView
    {
    }
}
