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

            Section("Skills da classe");
            DrawSkillPicker();

            Section("Arte do cliente (não sincroniza)");
            EditorGUILayout.PropertyField(serializedObject.FindProperty("icon"), new GUIContent("Ícone"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("description"), new GUIContent("Descrição"));

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawSkillPicker()
        {
            var listProp = serializedObject.FindProperty("skillIds");
            var chosen = new HashSet<int>();
            for (int i = 0; i < listProp.arraySize; i++) chosen.Add(listProp.GetArrayElementAtIndex(i).intValue);

            var skills = GatherSkills();
            if (skills.Count == 0) { EditorGUILayout.HelpBox("Nenhuma skill autorada ainda.", MessageType.Info); return; }

            EditorGUILayout.LabelField(chosen.Count == 0 ? "Nenhuma marcada = pode castar QUALQUER skill." : $"{chosen.Count} skill(s) marcada(s).", EditorStyles.miniLabel);

            foreach (var s in skills)
            {
                bool was = chosen.Contains(s.id);
                bool now = EditorGUILayout.ToggleLeft($"#{s.id}  {s.displayName}", was);
                if (now == was) continue;
                if (now) { listProp.arraySize++; listProp.GetArrayElementAtIndex(listProp.arraySize - 1).intValue = s.id; }
                else RemoveValue(listProp, s.id);
            }
        }

        private static void RemoveValue(SerializedProperty listProp, int value)
        {
            for (int i = 0; i < listProp.arraySize; i++)
                if (listProp.GetArrayElementAtIndex(i).intValue == value) { listProp.DeleteArrayElementAtIndex(i); return; }
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
