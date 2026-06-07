namespace Arcane_Aegis.Entities
{
    /// <summary>
    /// View for a non-player living entity (mirrors the server's <c>Npc</c>). Inherits the HumanoidView health
    /// bar + death cue; base of monster/boss/pet views. Remote-only (no local control stack).
    /// </summary>
    public class NpcView : HumanoidView
    {
    }
}
