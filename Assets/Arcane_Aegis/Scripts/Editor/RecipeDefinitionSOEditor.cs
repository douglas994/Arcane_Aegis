using UnityEditor;
using UnityEngine;
using Arcane_Aegis.Content;

namespace Arcane_Aegis.EditorTools
{
    /// <summary>Inspector for a crafting recipe: identity + profession gate, the output item, and an ingredients list
    /// (drag ItemDefinitionSO + qty). Mirrors the resource-node inspector.</summary>
    [CustomEditor(typeof(RecipeDefinitionSO))]
    public class RecipeDefinitionSOEditor : Editor
    {
        private static readonly Color ColSection = new(0.45f, 0.62f, 1f);

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var so = (RecipeDefinitionSO)target;

            string outName = so.output != null ? (string.IsNullOrEmpty(so.output.displayName) ? so.output.name : so.output.displayName) : "—";
            EditorGUILayout.HelpBox($"{(string.IsNullOrEmpty(so.displayName) ? so.name : so.displayName)} · {so.profession} Nv{so.requiredLevel} · {so.craftSeconds:0.#}s → {so.outputQty}x {outName} · {so.ingredients.Count} ingrediente(s)", MessageType.None);

            Section("Identidade");
            P("id", "Id"); P("displayName", "Nome"); P("profession", "Profissão");
            P("requiredLevel", "Nível mín. da profissão");

            Section("Criação");
            P("craftSeconds", "Tempo (s)");
            P("xpReward", "XP por criação");

            Section("Resultado");
            P("output", "Item produzido");
            P("outputQty", "Quantidade");

            Section("Ingredientes");
            EditorGUILayout.HelpBox("Cada linha: um material + quantidade consumida.", MessageType.None);
            DrawIngredients(serializedObject.FindProperty("ingredients"));

            Section("Arte do cliente (não sincroniza)");
            P("icon", "Ícone"); P("description", "Descrição");

            serializedObject.ApplyModifiedProperties();
        }

        private static void DrawIngredients(SerializedProperty list)
        {
            bool anyBad = false;
            for (int i = 0; i < list.arraySize; i++)
            {
                var el = list.GetArrayElementAtIndex(i);
                var item = el.FindPropertyRelative("item");
                var qty = el.FindPropertyRelative("qty");
                if (item.objectReferenceValue == null || qty.intValue <= 0) anyBad = true;

                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.PropertyField(item, GUIContent.none);
                    GUILayout.Label("x", GUILayout.Width(12));
                    qty.intValue = Mathf.Max(1, EditorGUILayout.IntField(qty.intValue, GUILayout.Width(44)));
                    if (GUILayout.Button("✕", GUILayout.Width(22))) { list.DeleteArrayElementAtIndex(i); break; }
                }
            }

            if (anyBad)
                EditorGUILayout.HelpBox("Um ingrediente está sem item ou com quantidade 0 — corrija antes de sincronizar.", MessageType.Warning);

            if (GUILayout.Button("+ Adicionar ingrediente"))
            {
                int n = list.arraySize;
                list.InsertArrayElementAtIndex(n);
                var el = list.GetArrayElementAtIndex(n);
                el.FindPropertyRelative("item").objectReferenceValue = null;
                el.FindPropertyRelative("qty").intValue = 1; // default que funciona
            }
        }

        private void P(string prop, string label)
        {
            var p = serializedObject.FindProperty(prop);
            if (p == null) return;
            EditorGUILayout.PropertyField(p, new GUIContent(label), true);
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
