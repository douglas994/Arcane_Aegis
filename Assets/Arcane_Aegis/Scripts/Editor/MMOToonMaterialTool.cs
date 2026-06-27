using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Arcane_Aegis.EditorTools
{
    /// <summary>
    /// Batch material styler for the MMOToon shader. Pick a REFERENCE material with the look you want, select many
    /// materials (in the Project) and/or GameObjects/prefabs (their renderers' materials are gathered), and click apply:
    /// it copies the STYLE props (cel/shadow/rim/specular/outline/light) from the reference onto all selected, while
    /// PRESERVING each material's own identity (base map/color, normal/emission/occlusion maps, IsFace) — so every
    /// character shares the same toon look but keeps its own textures. Menu: ArcaneMMO ▸ Characters ▸ MMOToon Material Tool.
    /// </summary>
    public class MMOToonMaterialTool : EditorWindow
    {
        [SerializeField] private Material reference;
        [SerializeField] private bool switchShader = true; // also set each material's shader to the reference's (MMOToon)

        // STYLE properties copied from the reference (the "look"). Identity props (_BaseMap/_BaseColor/_BumpMap/
        // _EmissionMap/_OcclusionMap/_IsFace/_Cutoff and their toggles) are intentionally NOT in this list → preserved.
        private static readonly string[] StyleFloats =
        {
            "_RampSteps", "_CelMidPoint", "_CelSoftness", "_DirectLightMultiplier", "_AdditionalLightMultiplier",
            "_IndirectLightMultiplier", "_LightMinLimit", "_LightMaxLimit", "_ReceiveShadowMappingAmount",
            "_ReceiveShadowMappingPosOffset", "_UseRimLight", "_RimMin", "_RimMax", "_RimAlignLight",
            "_UseToonSpecular", "_SpecularSize", "_SpecularSoftness", "_OutlineWidth", "_OutlineColorMulBaseColor",
            "_OutlineFadeStart", "_OutlineFadeEnd", "_OutlineZOffset", "_OutlineZOffsetMaskRemapStart", "_OutlineZOffsetMaskRemapEnd",
        };
        private static readonly string[] StyleColors =
        {
            "_ShadowTint", "_IndirectLightMinColor", "_RimColor", "_SpecularColor", "_OutlineColor",
        };

        [MenuItem("ArcaneMMO/Characters/MMOToon Material Tool")]
        public static void Open() => GetWindow<MMOToonMaterialTool>("MMOToon Tool");

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "Iguala o ESTILO toon de vários materiais a partir de um de referência.\n" +
                "1) Selecione um material de referência (com o look pronto).\n" +
                "2) Selecione no Project os materiais (ou GameObjects/prefabs) que quer igualar — pode vários.\n" +
                "3) Clique em Aplicar. As texturas/cores de cada material são PRESERVADAS; só o estilo é copiado.",
                MessageType.Info);

            reference = (Material)EditorGUILayout.ObjectField("Material de referência", reference, typeof(Material), false);
            switchShader = EditorGUILayout.Toggle("Trocar shader p/ o da referência", switchShader);

            var targets = GatherSelectedMaterials();
            EditorGUILayout.LabelField($"Materiais selecionados: {targets.Count}");

            using (new EditorGUI.DisabledScope(reference == null || targets.Count == 0))
                if (GUILayout.Button("Aplicar estilo aos selecionados", GUILayout.Height(30)))
                    Apply(targets);
        }

        // Re-gather on selection change so the count updates live.
        private void OnSelectionChange() => Repaint();

        private static List<Material> GatherSelectedMaterials()
        {
            var set = new HashSet<Material>();
            foreach (var obj in Selection.objects)
            {
                if (obj is Material m) set.Add(m);
                else if (obj is GameObject go)
                    foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                        foreach (var sm in r.sharedMaterials)
                            if (sm != null) set.Add(sm);
            }
            return new List<Material>(set);
        }

        private void Apply(List<Material> targets)
        {
            int changed = 0;
            Undo.RecordObjects(targets.ToArray(), "Apply MMOToon style");
            foreach (var t in targets)
            {
                if (t == null || t == reference) continue;
                if (switchShader && reference.shader != null) t.shader = reference.shader;
                CopyStyle(reference, t);
                EditorUtility.SetDirty(t);
                changed++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[MMOToon] estilo aplicado em {changed} material(is) (texturas/cores preservadas).");
        }

        private static void CopyStyle(Material src, Material dst)
        {
            foreach (var p in StyleFloats)
                if (src.HasProperty(p) && dst.HasProperty(p)) dst.SetFloat(p, src.GetFloat(p));
            foreach (var p in StyleColors)
                if (src.HasProperty(p) && dst.HasProperty(p)) dst.SetColor(p, src.GetColor(p));

            // Keep the style toggles' keywords in sync with their float (rim/specular are part of the look).
            SetKeyword(dst, "_RIMLIGHT", dst.HasProperty("_UseRimLight") && dst.GetFloat("_UseRimLight") > 0.5f);
            SetKeyword(dst, "_TOONSPECULAR", dst.HasProperty("_UseToonSpecular") && dst.GetFloat("_UseToonSpecular") > 0.5f);
        }

        private static void SetKeyword(Material m, string keyword, bool on)
        {
            if (on) m.EnableKeyword(keyword); else m.DisableKeyword(keyword);
        }
    }
}
