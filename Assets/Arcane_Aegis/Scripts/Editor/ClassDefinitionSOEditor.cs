using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Arcane_Aegis.Content;

namespace Arcane_Aegis.EditorTools
{
    /// <summary>
    /// Inspector for a class. Beyond the base stats it adds a <b>skill picker</b>: a checklist of every authored skill,
    /// so you tick which abilities the class can cast (synced to the server, which rejects casts the class doesn't have).
    /// An empty list = no restriction (the class can cast anything), so unauthored classes keep working.
    /// </summary>
    [CustomEditor(typeof(ClassDefinitionSO))]
    public class ClassDefinitionSOEditor : Editor
    {
        private static readonly Color ColSection = new(0.45f, 0.62f, 1f);

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            Section("Identidade");
            EditorGUILayout.PropertyField(serializedObject.FindProperty("id"), new GUIContent("Id"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("displayName"), new GUIContent("Nome"));

            Section("Atributos base (nível 1)");
            Row("str", "Força"); Row("dex", "Destreza"); Row("intel", "Inteligência");
            Row("vit", "Vitalidade"); Row("spi", "Espírito"); Row("luk", "Sorte");

            Section("Crescimento por nível");
            Row("strPerLevel", "Força/nível"); Row("dexPerLevel", "Destreza/nível"); Row("intPerLevel", "Inteligência/nível");
            Row("vitPerLevel", "Vitalidade/nível"); Row("spiPerLevel", "Espírito/nível"); Row("lukPerLevel", "Sorte/nível");

            Section("Skills da classe (ordem = barra de ação)");
            DrawSkillPicker();

            Section("Itens iniciais (loadout)");
            DrawStartItems();

            Section("Arte do cliente (não sincroniza)");
            EditorGUILayout.PropertyField(serializedObject.FindProperty("icon"), new GUIContent("Ícone"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("description"), new GUIContent("Descrição"));

            serializedObject.ApplyModifiedProperties();
        }

        // The class's skillIds is its ORDERED action bar (slot 0 = basic = left-click / key 1). This draws the chosen
        // skills in order with reorder (▲▼) + remove (✕), plus a dropdown to add ones not yet chosen. Empty list = the
        // class can cast ANY skill (server gate is permissive) but then the bar/basic is undefined → author at least one.
        private void DrawSkillPicker()
        {
            var listProp = serializedObject.FindProperty("skillIds");
            var skills = GatherSkills();
            if (skills.Count == 0) { EditorGUILayout.HelpBox("Nenhuma skill autorada ainda.", MessageType.Info); return; }

            EditorGUILayout.LabelField(
                listProp.arraySize == 0 ? "Vazia = pode castar QUALQUER skill (mas a básica fica indefinida — adicione ao menos uma)."
                                        : "Ordem = barra de ação. Slot 0 = básica (clique esquerdo / tecla 1).",
                EditorStyles.miniLabel);

            int moveFrom = -1, moveTo = -1, removeAt = -1;
            for (int i = 0; i < listProp.arraySize; i++)
            {
                int id = listProp.GetArrayElementAtIndex(i).intValue;
                var so = skills.Find(s => s.id == id);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(i == 0 ? "★ básica" : $"slot {i}", GUILayout.Width(64));
                EditorGUILayout.LabelField(so != null ? $"#{id}  {so.displayName}" : $"#{id}  (skill removida?)");
                using (new EditorGUI.DisabledScope(i == 0)) if (GUILayout.Button("▲", GUILayout.Width(26))) { moveFrom = i; moveTo = i - 1; }
                using (new EditorGUI.DisabledScope(i == listProp.arraySize - 1)) if (GUILayout.Button("▼", GUILayout.Width(26))) { moveFrom = i; moveTo = i + 1; }
                if (GUILayout.Button("✕", GUILayout.Width(26))) removeAt = i;
                EditorGUILayout.EndHorizontal();
            }
            if (moveFrom >= 0) listProp.MoveArrayElement(moveFrom, moveTo);
            if (removeAt >= 0) listProp.DeleteArrayElementAtIndex(removeAt);

            // Add a skill not yet in the bar (dropdown of the remaining authored skills).
            var chosen = new HashSet<int>();
            for (int i = 0; i < listProp.arraySize; i++) chosen.Add(listProp.GetArrayElementAtIndex(i).intValue);
            var addable = skills.FindAll(s => !chosen.Contains(s.id));
            if (addable.Count > 0)
            {
                var labels = new string[addable.Count + 1];
                labels[0] = "+ Adicionar skill à barra...";
                for (int i = 0; i < addable.Count; i++) labels[i + 1] = $"#{addable[i].id}  {addable[i].displayName}";
                int pick = EditorGUILayout.Popup(0, labels);
                if (pick > 0)
                {
                    listProp.arraySize++;
                    listProp.GetArrayElementAtIndex(listProp.arraySize - 1).intValue = addable[pick - 1].id;
                }
            }
        }

        // Starter loadout: each row = an item dropdown (authored items) + a quantity. The server grants these to a
        // brand-new character of this class, at creation (Atavism-style). Empty = the class starts with nothing.
        private void DrawStartItems()
        {
            var listProp = serializedObject.FindProperty("startItems");
            var items = GatherItems();
            if (items.Count == 0) { EditorGUILayout.HelpBox("Nenhum item autorado ainda (crie um ItemDefinitionSO).", MessageType.Info); return; }

            var ids = new string[items.Count];
            var labels = new string[items.Count];
            for (int i = 0; i < items.Count; i++)
            {
                ids[i] = items[i].id;
                labels[i] = string.IsNullOrEmpty(items[i].displayName) ? items[i].id : $"{items[i].id}  ({items[i].displayName})";
            }

            EditorGUILayout.LabelField(listProp.arraySize == 0 ? "Nenhum item inicial — a classe começa sem nada." : $"{listProp.arraySize} item(ns) inicial(is) (vão pra bag).", EditorStyles.miniLabel);

            int removeAt = -1;
            for (int i = 0; i < listProp.arraySize; i++)
            {
                var entry = listProp.GetArrayElementAtIndex(i);
                var idProp = entry.FindPropertyRelative("itemId");
                var qtyProp = entry.FindPropertyRelative("qty");

                EditorGUILayout.BeginHorizontal();
                int cur = Mathf.Max(0, System.Array.IndexOf(ids, idProp.stringValue));
                idProp.stringValue = ids[EditorGUILayout.Popup(cur, labels)];
                qtyProp.intValue = Mathf.Max(1, EditorGUILayout.IntField(Mathf.Max(1, qtyProp.intValue), GUILayout.Width(60)));
                if (GUILayout.Button("✕", GUILayout.Width(24))) removeAt = i;
                EditorGUILayout.EndHorizontal();
            }
            if (removeAt >= 0) listProp.DeleteArrayElementAtIndex(removeAt);

            if (GUILayout.Button("+ Adicionar item inicial"))
            {
                listProp.arraySize++;
                var entry = listProp.GetArrayElementAtIndex(listProp.arraySize - 1);
                entry.FindPropertyRelative("itemId").stringValue = ids[0];
                entry.FindPropertyRelative("qty").intValue = 1;
            }
        }

        private static List<ItemDefinitionSO> GatherItems()
        {
            var guids = AssetDatabase.FindAssets("t:ItemDefinitionSO");
            var list = new List<ItemDefinitionSO>(guids.Length);
            foreach (var g in guids)
            {
                var it = AssetDatabase.LoadAssetAtPath<ItemDefinitionSO>(AssetDatabase.GUIDToAssetPath(g));
                if (it != null) list.Add(it);
            }
            list.Sort((a, b) => string.Compare(a.id, b.id, System.StringComparison.OrdinalIgnoreCase));
            return list;
        }

        private static List<SkillDefinitionSO> GatherSkills()
        {
            var guids = AssetDatabase.FindAssets("t:SkillDefinitionSO");
            var list = new List<SkillDefinitionSO>(guids.Length);
            foreach (var g in guids)
            {
                var s = AssetDatabase.LoadAssetAtPath<SkillDefinitionSO>(AssetDatabase.GUIDToAssetPath(g));
                if (s != null) list.Add(s);
            }
            list.Sort((a, b) => a.id.CompareTo(b.id));
            return list;
        }

        private void Row(string prop, string label)
            => EditorGUILayout.PropertyField(serializedObject.FindProperty(prop), new GUIContent(label));

        private static void Section(string title)
        {
            EditorGUILayout.Space(6);
            var r = EditorGUILayout.GetControlRect(false, 18);
            EditorGUI.DrawRect(new Rect(r.x, r.y + 8, 3, 10), ColSection);
            EditorGUI.LabelField(new Rect(r.x + 8, r.y, r.width - 8, r.height), title.ToUpperInvariant(), EditorStyles.miniBoldLabel);
        }
    }
}
