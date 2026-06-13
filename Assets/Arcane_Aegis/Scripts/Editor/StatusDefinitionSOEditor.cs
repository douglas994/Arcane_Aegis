using UnityEditor;
using UnityEngine;
using ArcaneShared.Enums;
using Arcane_Aegis.Content;

namespace Arcane_Aegis.EditorTools
{
    /// <summary>
    /// Contextual inspector for a status. Like the skill drawer, <c>amount</c> is overloaded (damage/heal per tick vs
    /// slow % vs stat bonus) and <c>tickInterval</c>/<c>stat</c> only matter for some kinds. This relabels <c>amount</c>
    /// per kind and hides the fields that don't apply.
    /// </summary>
    [CustomEditor(typeof(StatusDefinitionSO))]
    public class StatusDefinitionSOEditor : Editor
    {
        private static readonly Color ColSection = new(0.45f, 0.62f, 1f);

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            Section("Identidade");
            var id = serializedObject.FindProperty("id");
            EditorGUILayout.PropertyField(id, new GUIContent("Id", "Id de texto referenciado pelos efeitos ApplyStatus das skills."));
            if (string.IsNullOrWhiteSpace(id.stringValue)) EditorGUILayout.HelpBox("Id vazio — nenhuma skill conseguirá referenciar este status.", MessageType.Error);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("displayName"), new GUIContent("Nome"));

            Section("Comportamento");
            var kindProp = serializedObject.FindProperty("kind");
            EditorGUILayout.PropertyField(kindProp, new GUIContent("Tipo"));
            var kind = (StatusKind)kindProp.enumValueIndex;
            EditorGUILayout.LabelField(KindHint(kind), EditorStyles.miniLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("element"), new GUIContent("Elemento"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("duration"), new GUIContent("Duração (s)"));

            string amountLbl = AmountLabel(kind);
            if (amountLbl != null) EditorGUILayout.PropertyField(serializedObject.FindProperty("amount"), new GUIContent(amountLbl));

            if (kind == StatusKind.Dot || kind == StatusKind.Hot)
                EditorGUILayout.PropertyField(serializedObject.FindProperty("tickInterval"), new GUIContent("Intervalo do tick (s)"));

            if (kind == StatusKind.StatBuff)
                EditorGUILayout.PropertyField(serializedObject.FindProperty("stat"), new GUIContent("Atributo afetado"));

            Section("Acúmulo");
            EditorGUILayout.PropertyField(serializedObject.FindProperty("stacking"), new GUIContent("Empilhamento", "Refresh = renova a duração; Stack = soma camadas."));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("maxStacks"), new GUIContent("Máx. de camadas"));

            Section("Arte do cliente (não sincroniza)");
            EditorGUILayout.PropertyField(serializedObject.FindProperty("icon"), new GUIContent("Ícone"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("description"), new GUIContent("Descrição"));

            serializedObject.ApplyModifiedProperties();
        }

        /// <summary>The contextual label for <c>amount</c> (null = the kind ignores amount).</summary>
        private static string AmountLabel(StatusKind k) => k switch
        {
            StatusKind.Dot      => "Dano por tick",
            StatusKind.Hot      => "Cura por tick",
            StatusKind.Slow     => "Lentidão (%)",
            StatusKind.StatBuff => "Bônus no atributo",
            _                   => null, // Stun/Root não usam amount
        };

        private static string KindHint(StatusKind k) => k switch
        {
            StatusKind.Dot      => "Causa dano periódico.",
            StatusKind.Hot      => "Cura periódica.",
            StatusKind.Stun     => "Não pode agir nem se mover.",
            StatusKind.Slow     => "Reduz a velocidade de movimento (amount = %).",
            StatusKind.Root     => "Não pode se mover (mas pode agir).",
            StatusKind.StatBuff => "Soma o bônus a um atributo enquanto ativo.",
            _ => "",
        };

        private static void Section(string title)
        {
            EditorGUILayout.Space(6);
            var r = EditorGUILayout.GetControlRect(false, 18);
            EditorGUI.DrawRect(new Rect(r.x, r.y + 8, 3, 10), ColSection);
            EditorGUI.LabelField(new Rect(r.x + 8, r.y, r.width - 8, r.height), title.ToUpperInvariant(), EditorStyles.miniBoldLabel);
        }
    }
}
