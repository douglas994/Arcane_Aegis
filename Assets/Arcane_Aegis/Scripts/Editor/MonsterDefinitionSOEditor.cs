using UnityEditor;
using UnityEngine;
using Arcane_Aegis.Content;

namespace Arcane_Aegis.EditorTools
{
    /// <summary>Inspector for a monster: grouped sections + a loot table (drag ItemDefinitionSO into each row, set the
    /// chance % and quantity). Mirrors the other content inspectors.</summary>
    [CustomEditor(typeof(MonsterDefinitionSO))]
    public class MonsterDefinitionSOEditor : Editor
    {
        private static readonly Color ColSection = new(0.45f, 0.62f, 1f);

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var so = (MonsterDefinitionSO)target;

            EditorGUILayout.HelpBox($"{(string.IsNullOrEmpty(so.displayName) ? so.name : so.displayName)} · Lv{so.level} · {so.xpReward} XP · {so.loot.Count} loot", MessageType.None);

            Section("Identidade");
            P("id", "Id"); P("displayName", "Nome"); P("level", "Nível");
            P("disposition", "Disposição"); P("kind", "Tipo");

            Section("Atributos");
            P("maxHp", "Vida (0 = fórmula)");
            P("str", "Força"); P("dex", "Destreza"); P("intel", "Inteligência");
            P("vit", "Vitalidade"); P("spi", "Espírito"); P("luk", "Sorte"); P("armor", "Armadura");
            P("element", "Elemento");

            Section("Comportamento");
            P("aggroRadius", "Raio de aggro (m)"); P("leashRadius", "Raio de leash (m)");
            P("attackRange", "Alcance de ataque (m)"); P("moveSpeed", "Velocidade");
            P("attackAbilityId", "Skill de ataque legada (id)"); P("xpReward", "XP ao matar");

            Section("IA — arquétipo + skills");
            P("archetype", "Arquétipo"); P("abilities", "Skills da IA");
            P("fleeHpPct", "Foge abaixo de (% vida)"); P("eliteScale", "Escala Elite");
            P("patrolRadius", "Raio de ronda (m)");

            Section("Hitbox de mira");
            P("hitboxRadius", "Raio (m)"); P("hitboxHeight", "Altura (m)");

            Section("Boss (opcional — preencher torna o monstro um boss)");
            EditorGUILayout.HelpBox("Um boss é um monstro com fases/enrage/anúncio. O arquétipo acima continua sendo o ESTILO de luta (melee/ranged/caster). Deixe vazio para um monstro normal.", MessageType.None);
            P("enrageSeconds", "Enrage após (s)"); P("announceGlobal", "Anunciar na zona");
            P("phases", "Fases (por % de vida)");

            Section("Loot");
            EditorGUILayout.HelpBox("Cada linha: um item + chance (%) + quantidade. A sorte (LUK) do matador dá um bônus pequeno na chance.", MessageType.None);
            P("loot", "");

            Section("Arte do cliente (não sincroniza)");
            P("icon", "Ícone"); P("description", "Descrição"); P("model3D", "Modelo 3D");

            serializedObject.ApplyModifiedProperties();
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
