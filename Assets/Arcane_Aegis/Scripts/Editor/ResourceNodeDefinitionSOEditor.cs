using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Arcane_Aegis.Content;

namespace Arcane_Aegis.EditorTools
{
    /// <summary>Inspector for a resource node: grouped sections, a tool dropdown (the equipped weapon Category needed to
    /// gather), and a materials list (drag ItemDefinitionSO + chance + qty). Mirrors the monster inspector.</summary>
    [CustomEditor(typeof(ResourceNodeDefinitionSO))]
    public class ResourceNodeDefinitionSOEditor : Editor
    {
        private static readonly Color ColSection = new(0.45f, 0.62f, 1f);

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var so = (ResourceNodeDefinitionSO)target;

            EditorGUILayout.HelpBox($"{(string.IsNullOrEmpty(so.displayName) ? so.name : so.displayName)} · {so.profession} · {so.gatherSeconds:0.#}s · {so.charges} cargas · {so.yields.Count} material(is)", MessageType.None);

            Section("Identidade");
            P("id", "Id"); P("displayName", "Nome"); P("profession", "Profissão");
            P("requiredLevel", "Nível mín. da profissão");

            Section("Coleta");
            DrawToolDropdown(serializedObject.FindProperty("requiredTool"));
            P("gatherSeconds", "Tempo de coleta (s)");
            P("charges", "Cargas (coletas)");
            P("respawnSeconds", "Respawn (s)");
            P("xpReward", "XP por coleta");

            Section("Materiais");
            EditorGUILayout.HelpBox("Cada linha: um item + chance (%) + quantidade. A sorte (LUK) dá um bônus pequeno.", MessageType.None);
            DrawYields(serializedObject.FindProperty("yields"));

            Section("Arte do cliente (não sincroniza)");
            P("icon", "Ícone"); P("description", "Descrição"); P("model3D", "Modelo 3D");

            serializedObject.ApplyModifiedProperties();
        }

        /// <summary>Materials list with sane defaults on add (chance 100 / 1–1) so a freshly-added yield actually drops,
        /// plus a red warning if any line would never yield (chance ≤ 0 or max ≤ 0 — the #1 "nada caiu" gotcha).</summary>
        private static void DrawYields(SerializedProperty list)
        {
            bool anyDead = false;
            for (int i = 0; i < list.arraySize; i++)
            {
                var el = list.GetArrayElementAtIndex(i);
                var item = el.FindPropertyRelative("item");
                var chance = el.FindPropertyRelative("chancePct");
                var min = el.FindPropertyRelative("min");
                var max = el.FindPropertyRelative("max");
                if (chance.intValue <= 0 || max.intValue <= 0) anyDead = true;

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PropertyField(item, GUIContent.none);
                    GUILayout.Label("%", GUILayout.Width(14));
                    chance.intValue = Mathf.Clamp(EditorGUILayout.IntField(chance.intValue, GUILayout.Width(42)), 0, 100);
                    GUILayout.Label("x", GUILayout.Width(12));
                    min.intValue = Mathf.Max(0, EditorGUILayout.IntField(min.intValue, GUILayout.Width(34)));
                    GUILayout.Label("–", GUILayout.Width(10));
                    max.intValue = Mathf.Max(min.intValue, EditorGUILayout.IntField(max.intValue, GUILayout.Width(34)));
                    if (GUILayout.Button("✕", GUILayout.Width(22))) { list.DeleteArrayElementAtIndex(i); break; }
                }
            }

            if (anyDead)
                EditorGUILayout.HelpBox("Um material está com chance 0% ou quantidade máx 0 → NUNCA vai cair. Ajuste pra ≥ 1.", MessageType.Error);

            if (GUILayout.Button("+ Adicionar material"))
            {
                int n = list.arraySize;
                list.InsertArrayElementAtIndex(n);
                var el = list.GetArrayElementAtIndex(n);
                el.FindPropertyRelative("item").objectReferenceValue = null;
                el.FindPropertyRelative("chancePct").intValue = 100; // defaults que realmente caem
                el.FindPropertyRelative("min").intValue = 1;
                el.FindPropertyRelative("max").intValue = 1;
            }
        }

        private static void DrawToolDropdown(SerializedProperty prop)
        {
            string cur = prop.stringValue ?? "";
            var labels = new List<string> { "(mãos / nenhuma)" };
            var values = new List<string> { "" };
            foreach (var cat in GatherWeaponCategories()) { labels.Add($"Categoria: {cat}"); values.Add(cat); }
            int idx = values.IndexOf(cur);
            if (idx < 0) { labels.Insert(1, $"{cur} (não encontrada)"); values.Insert(1, cur); idx = 1; }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel(new GUIContent("Ferramenta exigida", "A categoria da arma/ferramenta equipada na mão pra poder coletar (ex.: 'axe')."));
                int sel = EditorGUILayout.Popup(idx, labels.ToArray());
                if (sel != idx) prop.stringValue = values[sel];
            }
        }

        private static string[] GatherWeaponCategories()
        {
            var guids = AssetDatabase.FindAssets("t:ItemDefinitionSO");
            var cats = new SortedSet<string>();
            foreach (var g in guids)
            {
                var it = AssetDatabase.LoadAssetAtPath<ItemDefinitionSO>(AssetDatabase.GUIDToAssetPath(g));
                if (it != null && it.type == ArcaneShared.Enums.ItemType.Weapon && !string.IsNullOrWhiteSpace(it.category)) cats.Add(it.category);
            }
            var arr = new string[cats.Count]; cats.CopyTo(arr); return arr;
        }

        private void P(string prop, string label)
        {
            var p = serializedObject.FindProperty(prop);
            if (p == null) return;
            if (string.IsNullOrEmpty(label)) EditorGUILayout.PropertyField(p, true);
            else EditorGUILayout.PropertyField(p, new GUIContent(label), true);
        }

        private static void Section(string title)
        {
            EditorGUILayout.Space(6);
            var r = EditorGUILayout.GetControlRect(false, 18);
            EditorGUI.DrawRect(new Rect(r.x, r.y + 8, 3, 10), ColSection);
            EditorGUI.LabelField(new Rect(r.x + 8, r.y, r.width - 8, r.height), title.ToUpperInvariant(), EditorStyles.miniBoldLabel);
        }
    }
}
