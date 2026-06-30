using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using Arcane_Aegis.Content;
using Arcane_Aegis.Network;

namespace Arcane_Aegis.UI
{
    /// <summary>
    /// The QuestGiver window: lists the quests an NPC offers, each with its state — Aceitar (new), progress (active), or
    /// Entregar (active + all objectives met). Completed non-repeatable quests are hidden. Accept/turn-in ask the server
    /// (it re-validates). Rebuilds on <see cref="QuestState.OnChanged"/> while open. You build the UI: a row prefab
    /// (QuestRow) + a list parent. Opened by the NpcController at a QuestGiver. One scene instance.
    /// </summary>
    public sealed class QuestPanel : MonoBehaviour
    {
        public static QuestPanel Instance { get; private set; }

        [SerializeField] private ContentLibrary library;
        [SerializeField] private GameObject panel;     // root, toggled
        [SerializeField] private TMP_Text titleLabel;  // NPC name
        [SerializeField] private GameObject rowPrefab; // QuestRow
        [SerializeField] private Transform listParent; // where quest rows go

        private readonly List<GameObject> _rows = new();
        private NpcDefinitionSO _npc;

        private void Awake() { Instance = this; if (panel != null) panel.SetActive(false); }
        private void OnDestroy() { if (Instance == this) Instance = null; }

        private void OnEnable() { QuestState.OnChanged += Rebuild; }
        private void OnDisable() { QuestState.OnChanged -= Rebuild; }

        public bool IsOpen => panel != null && panel.activeSelf;

        /// <summary>Opens the quest panel for a QuestGiver NPC id (its offered quests come from the NpcDefinition).</summary>
        public void Open(string npcId)
        {
            _npc = library != null ? library.GetNpc(npcId) : null;
            if (_npc == null || !_npc.GivesQuests) return;
            if (titleLabel != null) titleLabel.text = string.IsNullOrEmpty(_npc.displayName) ? _npc.name : _npc.displayName;
            if (panel != null) panel.SetActive(true);
            Rebuild();
        }

        public void Close() { if (panel != null) panel.SetActive(false); _npc = null; ClearRows(); }

        private void Rebuild()
        {
            ClearRows();
            if (_npc == null || _npc.quests == null || rowPrefab == null || listParent == null) return;
            foreach (var q in _npc.quests)
            {
                if (q == null || string.IsNullOrWhiteSpace(q.id)) continue;
                bool active = QuestState.TryGet(q.id, out var rec) && rec.State == ArcaneShared.Models.CharQuestRecord.StateActive;
                bool completed = QuestState.IsCompleted(q.id);
                if (completed && !q.repeatable) continue; // done — hide

                string title = string.IsNullOrEmpty(q.displayName) ? q.id : q.displayName;
                string body = BuildBody(q, active ? rec : (ArcaneShared.Models.CharQuestRecord?)null);
                var row = NewRow();
                if (row == null) continue;

                if (active && IsComplete(q, rec))
                    row.Bind(title, body, "Entregar", () => { NetClient.Instance?.SendQuestTurnIn(q.id); });
                else if (active)
                    row.Bind(title, body, null, null); // in progress — no button
                else
                    row.Bind(title, body, "Aceitar", () => { NetClient.Instance?.SendQuestAccept(q.id); });
            }
        }

        private string BuildBody(QuestDefinitionSO q, ArcaneShared.Models.CharQuestRecord? rec)
        {
            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(q.description)) sb.AppendLine(q.description);
            if (q.objectives != null)
                for (int i = 0; i < q.objectives.Count; i++)
                {
                    var o = q.objectives[i];
                    int cur = rec.HasValue && rec.Value.Progress != null && i < rec.Value.Progress.Length ? rec.Value.Progress[i] : 0;
                    string label = string.IsNullOrEmpty(o.description) ? DefaultObjective(o) : o.description;
                    sb.AppendLine($"• {label}: {Mathf.Min(cur, o.count)}/{o.count}");
                }
            return sb.ToString().TrimEnd();
        }

        private static string DefaultObjective(QuestDefinitionSO.Objective o)
            => o.kind == QuestDefinitionSO.ObjectiveKind.KillMonster ? $"Matar {o.targetId}" : $"Coletar {o.targetId}";

        private bool IsComplete(QuestDefinitionSO q, ArcaneShared.Models.CharQuestRecord rec)
        {
            if (q.objectives == null) return true;
            for (int i = 0; i < q.objectives.Count; i++)
            {
                int cur = rec.Progress != null && i < rec.Progress.Length ? rec.Progress[i] : 0;
                if (cur < q.objectives[i].count) return false;
            }
            return true;
        }

        private QuestRow NewRow()
        {
            var go = Instantiate(rowPrefab, listParent);
            go.SetActive(true);
            _rows.Add(go);
            return go.GetComponent<QuestRow>();
        }

        private void ClearRows()
        {
            for (int i = _rows.Count - 1; i >= 0; i--) if (_rows[i] != null) Destroy(_rows[i]);
            _rows.Clear();
        }
    }
}
