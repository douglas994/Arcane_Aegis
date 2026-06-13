using UnityEngine;
using ArcaneShared.Enums;

namespace Arcane_Aegis.Content
{
    /// <summary>A status effect as a ScriptableObject: gameplay (synced to content.db) + client art. Mirrors the
    /// server's StatusRecord (Rules §12.2). Referenced by skills' ApplyStatus effects (by id). DoT/HoT tick a
    /// damage/heal; CC/buff kinds execute as the advanced phases land. Create via Assets ▸ Create ▸ ArcaneMMO ▸ Status Definition.</summary>
    [CreateAssetMenu(fileName = "Status_", menuName = "ArcaneMMO/Status Definition")]
    public class StatusDefinitionSO : ScriptableObject
    {
        [Header("Gameplay — synced to the server")]
        public string id;
        public string displayName;
        public StatusKind kind = StatusKind.Dot;
        [Tooltip("Total seconds the status lasts.")] public float duration = 5f;
        [Tooltip("Seconds between ticks (DoT/HoT).")] public float tickInterval = 1f;
        public ElementType element = ElementType.None;
        [Tooltip("Damage/heal per tick (DoT/HoT), the slow % (Slow), or the buff amount (StatBuff).")] public int amount;
        [Tooltip("For StatBuff: which attribute 'amount' boosts while active.")] public StatId stat = StatId.None;
        public StatusStacking stacking = StatusStacking.Refresh;
        public int maxStacks = 1;

        [Header("Client art — NOT synced")]
        public Sprite icon;
        [TextArea] public string description;
    }
}
