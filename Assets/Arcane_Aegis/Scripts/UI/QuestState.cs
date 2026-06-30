using System;
using ArcaneShared.Models;

namespace Arcane_Aegis.UI
{
    /// <summary>Client-side cache of the player's quest log (from S2C_QuestLog): active + completed quests with the
    /// server-computed per-objective progress. The quest panel + log rebuild on <see cref="OnChanged"/>.</summary>
    public static class QuestState
    {
        public static CharQuestRecord[] Quests = Array.Empty<CharQuestRecord>();
        public static event Action OnChanged;

        public static void Set(CharQuestRecord[] quests)
        {
            Quests = quests ?? Array.Empty<CharQuestRecord>();
            OnChanged?.Invoke();
        }

        public static bool TryGet(string questId, out CharQuestRecord rec)
        {
            for (int i = 0; i < Quests.Length; i++)
                if (Quests[i].QuestId == questId) { rec = Quests[i]; return true; }
            rec = default; return false;
        }

        public static bool IsActive(string questId) => TryGet(questId, out var r) && r.State == CharQuestRecord.StateActive;
        public static bool IsCompleted(string questId) => TryGet(questId, out var r) && r.State == CharQuestRecord.StateCompleted;
    }
}
