using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using ArcaneShared.Enums;
using Arcane_Aegis.Content;

namespace Arcane_Aegis.EditorTools
{
    /// <summary>
    /// Contextual inspector for a skill. The raw SO is confusing because one field (<c>amount</c>) means different things
    /// per effect (damage / heal / metres / shield points / projectile speed), <c>statusId</c> is a free string, and the
    /// targeting fields (cone/width) only matter for some modes. This drawer relabels <c>amount</c> per effect type, turns
    /// <c>statusId</c> into a dropdown of existing statuses, and hides fields that don't apply — so authoring is obvious.
    /// </summary>
    [CustomEditor(typeof(SkillDefinitionSO))]
    public class SkillDefinitionSOEditor : Editor
    {
        private static readonly Color ColSection = new(0.45f, 0.62f, 1f);
        private static readonly Color ColCard    = new(1f, 1f, 1f, 0.04f);

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var so = (SkillDefinitionSO)target;

            DrawSummary(so);

            // ── Identidade ──
            Section("Identidade");
            var id = serializedObject.FindProperty("id");
            EditorGUILayout.PropertyField(id, new GUIContent("Id (1–255)", "Id único usado pelo pacote de cast. Mantenha estável — mudar quebra referências."));
            if (id.intValue < 1 || id.intValue > 255) EditorGUILayout.HelpBox("Id precisa estar entre 1 e 255.", MessageType.Error);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("displayName"), new GUIContent("Nome"));

            // ── Lançamento ──
            Section("Lançamento");
            EditorGUILayout.PropertyField(serializedObject.FindProperty("castTime"), new GUIContent("Tempo de cast (s)", "Wind-up antes do efeito sair. 0 = instantâneo. >0 telegrafa (dá pra desviar)."));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("cooldown"), new GUIContent("Recarga (s)"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("cost"), new GUIContent("Custo de mana"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("element"), new GUIContent("Elemento"));
            DrawWeaponDropdown(serializedObject.FindProperty("requiredWeapon"));

            // ── Mira (contextual) ──
            Section("Mira");
            var targeting = serializedObject.FindProperty("targeting");
            EditorGUILayout.PropertyField(targeting, new GUIContent("Tipo de mira"));
            var mode = (TargetingMode)targeting.enumValueIndex;
            EditorGUILayout.LabelField(TargetingHint(mode), EditorStyles.miniLabel);
            if (mode != TargetingMode.Self)
                EditorGUILayout.PropertyField(serializedObject.FindProperty("range"), new GUIContent(RangeLabel(mode)));
            if (mode == TargetingMode.Cone)
                EditorGUILayout.PropertyField(serializedObject.FindProperty("coneAngle"), new GUIContent("Ângulo do cone (°)"));
            if (mode == TargetingMode.Line)
                EditorGUILayout.PropertyField(serializedObject.FindProperty("width"), new GUIContent("Largura da linha (m)"));

            // ── Efeitos (contextual) ──
            Section("Efeitos");
            DrawEffects();

            // ── Arte do cliente ──
            Section("Arte do cliente (não sincroniza)");
            EditorGUILayout.PropertyField(serializedObject.FindProperty("icon"), new GUIContent("Ícone"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("description"), new GUIContent("Descrição"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("animTrigger"), new GUIContent("Trigger de animação", "Trigger do Animator ao conjurar (ex.: 'CastSpell', 'Shoot'). Vazio = ataque padrão."));

            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField("VFX (vazio = flash padrão embutido)", EditorStyles.miniLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("castVfx"), new GUIContent("VFX ao conjurar", "Prefab no conjurador quando lança a skill. Vazio = flash (só em skills com cast)."));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("castVfxFollows"), new GUIContent("VFX segue o conjurador"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("impactVfx"), new GUIContent("VFX de impacto", "Prefab onde o golpe acerta (por alvo). Vazio = flash padrão."));
            if (mode is TargetingMode.Cone or TargetingMode.Circle or TargetingMode.Line)
                EditorGUILayout.PropertyField(serializedObject.FindProperty("areaVfx"), new GUIContent("VFX da área (AoE)", "Efeito único na área (explosão/cone), no conjurador virado pra frente, no momento do efeito."));
            if (HasEffect(so, AbilityEffectType.Projectile))
                EditorGUILayout.PropertyField(serializedObject.FindProperty("projectileVfx"), new GUIContent("VFX do projétil", "Prefab que voa. Vazio = esfera padrão."));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("vfxLifetime"), new GUIContent("Duração do VFX (s)"));

            serializedObject.ApplyModifiedProperties();
        }

        // ───────────────────────── effects ─────────────────────────
        private void DrawEffects()
        {
            var list = serializedObject.FindProperty("effects");
            string[] statusIds = GatherStatusIds();

            if (list.arraySize == 0)
                EditorGUILayout.LabelField("— nenhum efeito (a skill não faz nada) —", EditorStyles.miniLabel);

            int removeAt = -1;
            for (int i = 0; i < list.arraySize; i++)
            {
                var e = list.GetArrayElementAtIndex(i);
                var typeProp   = e.FindPropertyRelative("type");
                var amountProp = e.FindPropertyRelative("amount");
                var scaleProp  = e.FindPropertyRelative("scalesWith");
                var statusProp = e.FindPropertyRelative("statusId");
                var type = (AbilityEffectType)typeProp.enumValueIndex;

                var rect = EditorGUILayout.BeginVertical();
                EditorGUI.DrawRect(rect, ColCard);
                EditorGUILayout.Space(2);
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField($"#{i + 1}", GUILayout.Width(24));
                    EditorGUILayout.PropertyField(typeProp, GUIContent.none);
                    if (GUILayout.Button("✕", GUILayout.Width(22))) removeAt = i;
                }

                string amountLbl = AmountLabel(type);
                if (amountLbl != null) EditorGUILayout.PropertyField(amountProp, new GUIContent(amountLbl));

                if (type == AbilityEffectType.Damage || type == AbilityEffectType.Heal)
                    EditorGUILayout.PropertyField(scaleProp, new GUIContent("Escala com", "Qual poder de ataque multiplica o valor."));

                if (type == AbilityEffectType.ApplyStatus)
                    DrawStatusDropdown(statusProp, statusIds);
                else if (type == AbilityEffectType.Summon)
                    EditorGUILayout.PropertyField(statusProp, new GUIContent("Id da criatura", "Id do monstro a invocar (no-op até a Invocação ser ligada)."));

                EditorGUILayout.LabelField(EffectHint(type), EditorStyles.miniLabel);
                EditorGUILayout.Space(3);
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(2);
            }
            if (removeAt >= 0) list.DeleteArrayElementAtIndex(removeAt);

            if (GUILayout.Button("+ Adicionar efeito"))
            {
                list.arraySize++;
                var e = list.GetArrayElementAtIndex(list.arraySize - 1);
                e.FindPropertyRelative("type").enumValueIndex = 0;
                e.FindPropertyRelative("amount").intValue = 0;
                e.FindPropertyRelative("scalesWith").enumValueIndex = 0;
                e.FindPropertyRelative("statusId").stringValue = "";
            }
        }

        private static void DrawStatusDropdown(SerializedProperty statusProp, string[] statusIds)
        {
            string cur = statusProp.stringValue ?? "";
            var options = new List<string>(statusIds);
            int idx = options.IndexOf(cur);
            if (idx < 0) { options.Insert(0, string.IsNullOrEmpty(cur) ? "(escolha um status)" : $"{cur} (não encontrado)"); idx = 0; }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel(new GUIContent("Status a aplicar"));
                int sel = EditorGUILayout.Popup(idx, options.ToArray());
                if (sel != idx)
                {
                    string picked = options[sel];
                    statusProp.stringValue = (picked.StartsWith("(") || picked.EndsWith(")")) ? "" : picked;
                }
            }
            if (string.IsNullOrEmpty(statusProp.stringValue))
                EditorGUILayout.HelpBox("Nenhum status escolhido — esse efeito não fará nada.", MessageType.Warning);
        }

        /// <summary>Dropdown for the weapon requirement: "(nenhuma)" / "qualquer arma" / one of the weapon Categories
        /// that exist among the item SOs. Keeps an unknown current value visible so authoring never loses data.</summary>
        private static void DrawWeaponDropdown(SerializedProperty prop)
        {
            string cur = prop.stringValue ?? "";
            var labels = new List<string> { "(nenhuma)", "qualquer arma" };
            var values = new List<string> { "", "any" };
            foreach (var cat in GatherWeaponCategories()) { labels.Add($"Categoria: {cat}"); values.Add(cat); }

            int idx = values.IndexOf(cur);
            if (idx < 0) { labels.Insert(2, $"{cur} (não encontrada)"); values.Insert(2, cur); idx = 2; }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel(new GUIContent("Arma exigida", "A skill só pode ser castada com essa arma equipada na mão principal."));
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

        // ───────────────────────── helpers ─────────────────────────
        private static string[] GatherStatusIds()
        {
            var guids = AssetDatabase.FindAssets("t:StatusDefinitionSO");
            var ids = new List<string>(guids.Length);
            foreach (var g in guids)
            {
                var s = AssetDatabase.LoadAssetAtPath<StatusDefinitionSO>(AssetDatabase.GUIDToAssetPath(g));
                if (s != null && !string.IsNullOrWhiteSpace(s.id)) ids.Add(s.id);
            }
            ids.Sort();
            return ids.ToArray();
        }

        private static bool HasEffect(SkillDefinitionSO so, AbilityEffectType t)
        {
            if (so.effects == null) return false;
            foreach (var e in so.effects) if (e.type == t) return true;
            return false;
        }

        /// <summary>The contextual label for <c>amount</c> (null = the effect ignores amount, so hide it).</summary>
        private static string AmountLabel(AbilityEffectType t) => t switch
        {
            AbilityEffectType.Damage     => "Dano base",
            AbilityEffectType.Heal       => "Cura",
            AbilityEffectType.Knockback  => "Empurrão (m)",
            AbilityEffectType.Pull       => "Puxão (m)",
            AbilityEffectType.Shield     => "Escudo (pontos)",
            AbilityEffectType.Dash       => "Avanço (m)",
            AbilityEffectType.Projectile => "Velocidade (m/s)",
            _                            => null, // Blink usa Range; ApplyStatus/Summon não usam amount
        };

        private static string EffectHint(AbilityEffectType t) => t switch
        {
            AbilityEffectType.Damage     => "Causa dano no alvo.",
            AbilityEffectType.Heal       => "Cura o alvo.",
            AbilityEffectType.ApplyStatus=> "Aplica um status (DoT/buff/CC) — a duração vem do status.",
            AbilityEffectType.Knockback  => "Empurra o alvo pra longe do conjurador.",
            AbilityEffectType.Pull       => "Puxa o alvo na direção do conjurador.",
            AbilityEffectType.Shield     => "Dá um escudo que absorve dano.",
            AbilityEffectType.Dash       => "Move o CONJURADOR pra frente antes de mirar.",
            AbilityEffectType.Blink      => "Teleporta o conjurador até o ponto mirado (usa o Alcance).",
            AbilityEffectType.Summon     => "Invoca uma criatura (ainda não executado no servidor).",
            AbilityEffectType.Projectile => "Vira skillshot: voa do conjurador e aplica os OUTROS efeitos no impacto.",
            _ => "",
        };

        private static string RangeLabel(TargetingMode m) => m switch
        {
            TargetingMode.Cone   => "Alcance do cone (m)",
            TargetingMode.Circle => "Raio do círculo (m)",
            TargetingMode.Line   => "Comprimento da linha (m)",
            _                    => "Alcance (m)",
        };

        private static string TargetingHint(TargetingMode m) => m switch
        {
            TargetingMode.Self   => "Afeta só o conjurador (cura/buff próprios).",
            TargetingMode.Single => "Um alvo dentro do alcance.",
            TargetingMode.Cone   => "Todos num cone à frente (alcance + ângulo).",
            TargetingMode.Circle => "Todos num círculo (alcance = raio).",
            TargetingMode.Line   => "Todos numa linha à frente (alcance + largura).",
            _ => "",
        };

        private void DrawSummary(SkillDefinitionSO so)
        {
            int n = so.effects?.Count ?? 0;
            string mira = ((TargetingMode)serializedObject.FindProperty("targeting").enumValueIndex).ToString();
            string cast = so.castTime > 0 ? $"{so.castTime:0.##}s cast" : "instantâneo";
            EditorGUILayout.HelpBox($"#{so.id} · {mira} · {cast} · {n} efeito(s)", MessageType.None);
        }

        private static void Section(string title)
        {
            EditorGUILayout.Space(6);
            var r = EditorGUILayout.GetControlRect(false, 18);
            EditorGUI.DrawRect(new Rect(r.x, r.y + 8, 3, 10), ColSection);
            var st = new GUIStyle(EditorStyles.miniBoldLabel);
            EditorGUI.LabelField(new Rect(r.x + 8, r.y, r.width - 8, r.height), title.ToUpperInvariant(), st);
        }
    }
}
