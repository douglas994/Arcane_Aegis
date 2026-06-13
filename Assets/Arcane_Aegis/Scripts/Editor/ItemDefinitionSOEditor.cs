using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using ArcaneShared.Enums;
using Arcane_Aegis.Content;

namespace Arcane_Aegis.EditorTools
{
    /// <summary>
    /// A nicer, CONTEXTUAL inspector for <see cref="ItemDefinitionSO"/>: a header card (icon preview + rarity-colored
    /// name) then fields grouped into FOLDABLE sections that show/hide by item TYPE — a Weapon shows
    /// slot/2H/durability/stats, a Consumable shows the stack, etc. Hidden fields keep their value (just not shown).
    /// </summary>
    [CustomEditor(typeof(ItemDefinitionSO))]
    public class ItemDefinitionSOEditor : Editor
    {
        private static readonly Dictionary<string, bool> Fold = new();

        public override void OnInspectorGUI()
        {
            var so = (ItemDefinitionSO)target;
            serializedObject.Update();

            DrawCard(so);
            EditorGUILayout.Space(4);

            if (Section("Identidade"))
            {
                P("id"); P("displayName");
                EditorGUILayout.PropertyField(serializedObject.FindProperty("type"));
                P("rarity"); P("category");
                P("icon"); P("description");
            }
            var type = (ItemType)serializedObject.FindProperty("type").enumValueIndex;

            bool equippable = type is ItemType.Weapon or ItemType.Armor or ItemType.Accessory;
            bool gem = type == ItemType.Gem;

            if (equippable && Section("Equipamento"))
            {
                P("slot");
                if (type == ItemType.Weapon) P("twoHanded");
                P("element");
                P("levelReq"); P("classReq");
                P("durabilityMax");
                P("model3D");
            }

            if ((equippable || gem) && Section(gem ? "Atributos (ao encaixar)" : "Atributos"))
            {
                P("statsBase");
                if (equippable) { P("rollsPossible"); P("maxRolls"); }
            }

            if (type == ItemType.Consumable && Section("Efeitos"))
            {
                EditorGUILayout.HelpBox("Lista de efeitos ao usar. Instantâneo: Restore* (duração 0). Buff: defina a duração em segundos.", MessageType.None);
                P("effects");
            }

            if (equippable && Section("Evolução"))
            {
                P("tierMax"); P("enhanceMax"); P("socketsMax");
            }

            if (Section(equippable ? "Economia" : "Pilha & Economia"))
            {
                if (!equippable) P("stackMax");
                P("weight"); P("sellable"); P("tradeable"); P("npcPrice");
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void P(string prop)
        {
            var p = serializedObject.FindProperty(prop);
            if (p != null) EditorGUILayout.PropertyField(p, true);
        }

        /// <summary>A collapsible section header (state remembered per title). Returns whether the section is open.</summary>
        private static bool Section(string title)
        {
            if (!Fold.TryGetValue(title, out bool open)) open = true;

            EditorGUILayout.Space(5);
            var rect = EditorGUILayout.GetControlRect(false, 20);
            EditorGUI.DrawRect(rect, new Color(0.30f, 0.34f, 0.46f, 0.28f));
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 3, rect.height), new Color(0.45f, 0.62f, 1f));

            var style = new GUIStyle(EditorStyles.foldout) { fontStyle = FontStyle.Bold };
            style.normal.textColor = style.onNormal.textColor = new Color(0.72f, 0.80f, 1f);
            open = EditorGUI.Foldout(new Rect(rect.x + 8, rect.y, rect.width - 8, rect.height), open, title, true, style);

            Fold[title] = open;
            if (open) EditorGUILayout.Space(2);
            return open;
        }

        private static void DrawCard(ItemDefinitionSO so)
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox, GUILayout.Height(76)))
            {
                var box = GUILayoutUtility.GetRect(64, 64, GUILayout.Width(64), GUILayout.Height(64));
                EditorGUI.DrawRect(box, new Color(0, 0, 0, 0.25f));
                if (so.icon != null)
                {
                    var tex = AssetPreview.GetAssetPreview(so.icon);
                    if (tex == null) tex = so.icon.texture;
                    if (tex != null) GUI.DrawTexture(box, tex, ScaleMode.ScaleToFit);
                }

                using (new EditorGUILayout.VerticalScope())
                {
                    string name = string.IsNullOrEmpty(so.displayName) ? so.name : so.displayName;
                    var title = new GUIStyle(EditorStyles.boldLabel) { fontSize = 15 };
                    title.normal.textColor = RarityColor(so.rarity);
                    EditorGUILayout.LabelField(name, title);

                    string slot = so.slot != EquipSlot.None ? $" · {so.slot}{(so.twoHanded ? " (2H)" : "")}" : "";
                    string elem = so.element != ElementType.None ? $" · {so.element}" : "";
                    EditorGUILayout.LabelField($"{so.rarity} {so.type}{slot}{elem}", EditorStyles.miniLabel);

                    string reqs = $"Lv {so.levelReq}";
                    if (!string.IsNullOrEmpty(so.classReq)) reqs += $" · {so.classReq}";
                    if (so.stackMax > 1) reqs += $" · stack {so.stackMax}";
                    EditorGUILayout.LabelField(reqs, EditorStyles.miniLabel);

                    if (string.IsNullOrWhiteSpace(so.id))
                        EditorGUILayout.HelpBox("Sem 'id' — preencha antes de sincronizar.", MessageType.Warning);
                }
            }
        }

        private static Color RarityColor(ItemRarity rarity) => rarity switch
        {
            ItemRarity.Uncommon  => new Color(0.49f, 0.99f, 0.49f),
            ItemRarity.Rare      => new Color(0.40f, 0.70f, 1.00f),
            ItemRarity.Epic      => new Color(0.75f, 0.45f, 0.95f),
            ItemRarity.Legendary => new Color(1.00f, 0.65f, 0.20f),
            ItemRarity.Mythic    => new Color(1.00f, 0.35f, 0.35f),
            _                    => new Color(0.85f, 0.85f, 0.85f),
        };
    }
}
