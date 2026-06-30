using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using Arcane_Aegis.Content;

namespace Arcane_Aegis.UI
{
    /// <summary>HUD quest log: the player's ACTIVE quests with per-objective progress. Toggle with a key (default J).
    /// Rebuilds on <see cref="QuestState.OnChanged"/>. You build the UI: a row prefab (QuestRow) + a list parent.</summary>
    public sealed class QuestLogPanel : MonoBehaviour
    {
        [SerializeField] private ContentLibrary library;
        [SerializeField] private GameObject panel;     // root, toggled
        [SerializeField] private GameObject rowPrefab; // QuestRow
        [SerializeField] private Transform listParent;
        [SerializeField] private Key toggleKey = Key.J;

        private readonly List<GameObject> _rows = new();

        private void Awake() { if (panel != null) panel.SetActive(false); }
        private void OnEnable() { QuestState.OnChanged += RebuildIfOpen; }
        private void OnDisable() { QuestState.OnChanged -= RebuildIfOpen; }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb == null || !kb[toggleKey].wasPressedThisFrame || UiFocus.IsTyping) return;
            if (panel == null) return;
            bool open = !panel.activeSelf;
            panel.SetActive(open);
            if (open) Rebuild();
        }

        private void RebuildIfOpen() { if (panel != null && panel.activeSelf) Rebuild(); }

        private void Rebuild()
        {
            ClearRows();
            if (rowPrefab == null || listParent == null || library == null) return;
            var quests = QuestState.Quests;
            for (int i = 0; i < quests.Length; i++)
            {
                if (quests[i].State != ArcaneShared.Models.CharQuestRecord.StateActive) continue;
                var def = library.GetQuest(quests[i].QuestId);
                if (def == null) continue;
                var go = Instantiate(rowPrefab, listParent);
                go.SetActive(true);
                _rows.Add(go);
                var row = go.GetComponent<QuestRow>();
                if (row != null) row.Bind(string.IsNullOrEmpty(def.displayName) ? def.id : def.displayName, Body(def, quests[i]), null, null);
            }
        }

        private static string Body(QuestDefinitionSO q, ArcaneShared.Models.CharQuestRecord rec)
        {
            var sb = new StringBuilder();
            if (q.objectives != null)
                for (int i = 0; i < q.objectives.Count; i++)
                {
                    var o = q.objectives[i];
                    int cur = rec.Progress != null && i < rec.Progress.Length ? rec.Progress[i] : 0;
                    string label = string.IsNullOrEmpty(o.description)
                        ? (o.kind == QuestDefinitionSO.ObjectiveKind.KillMonster ? $"Matar {o.targetId}" : $"Coletar {o.targetId}")
                        : o.description;
                    sb.AppendLine($"• {label}: {Mathf.Min(cur, o.count)}/{o.count}");
                }
            return sb.ToString().TrimEnd();
        }

        private void ClearRows()
        {
            for (int i = _rows.Count - 1; i >= 0; i--) if (_rows[i] != null) Destroy(_rows[i]);
            _rows.Clear();
        }
    }
}
