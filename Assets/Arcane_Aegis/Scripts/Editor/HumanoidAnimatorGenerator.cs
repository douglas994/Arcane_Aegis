using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Arcane_Aegis.EditorTools
{
    /// <summary>
    /// One-click generator for the SHARED Humanoid AnimatorController used by EVERY humanoid character (players).
    /// Because the models are imported as Mecanim Humanoid, ONE controller retargets to all of them — author the clip
    /// set ONCE here and every character reuses it (no per-character animator). It wires the EXACT parameters that
    /// <see cref="Arcane_Aegis.Controllers.CharacterAnimator"/> drives: Speed(float), Grounded(bool), Attack(trigger),
    /// Hit(trigger), Dead(bool), Mounted(bool), Gathering(bool), GatherType(int). Missing optional clips are skipped.
    /// Menu: ArcaneMMO ▸ Animation ▸ Humanoid Controller Generator.
    /// </summary>
    public class HumanoidAnimatorGenerator : EditorWindow
    {
        // Locomotion blend (by Speed, 0..1)
        [SerializeField] private AnimationClip idle, walk, run, dash;
        // One-shots / states
        [SerializeField] private AnimationClip jump, attack, hit, death, sit;
        // Gather — one clip per profession (GatherType byte: 0 chop, 1 mine, 2 herb, 3 skin, 4 fish, 5 farm).
        [SerializeField] private AnimationClip gatherChop, gatherMine, gatherHerb, gatherSkin, gatherFish, gatherFarm;
        [SerializeField] private string outputPath = "Assets/Arcane_Aegis/Animation/HumanoidLocomotion.controller";

        [MenuItem("ArcaneMMO/Animation/Humanoid Controller Generator")]
        public static void Open() => GetWindow<HumanoidAnimatorGenerator>("Humanoid Controller");

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "Gera o AnimatorController COMPARTILHADO dos humanoides. Atribua os clips (idle/walk/run são obrigatórios; o resto é opcional) e clique em Gerar.\n" +
                "Todo personagem humanoide reusa este mesmo controller via retargeting Humanoid — então gere uma vez só.",
                MessageType.Info);

            EditorGUILayout.LabelField("Locomoção (blend por Speed)", EditorStyles.boldLabel);
            idle = Clip("Idle (0.0)", idle);
            walk = Clip("Walk (~0.4)", walk);
            run  = Clip("Run (~0.75)", run);
            dash = Clip("Dash (1.0) — opcional", dash);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Ações / estados (opcionais)", EditorStyles.boldLabel);
            jump   = Clip("Jump/Airborne", jump);
            attack = Clip("Attack", attack);
            hit    = Clip("Hit (flinch)", hit);
            death  = Clip("Death", death);
            sit    = Clip("Mounted/Sit", sit);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Gather por profissão (GatherType)", EditorStyles.boldLabel);
            gatherChop = Clip("0 — Lenhador (chop)", gatherChop);
            gatherMine = Clip("1 — Minerador (mine)", gatherMine);
            gatherHerb = Clip("2 — Herborista (pick)", gatherHerb);
            gatherSkin = Clip("3 — Esfolador (skin)", gatherSkin);
            gatherFish = Clip("4 — Pescador (fish)", gatherFish);
            gatherFarm = Clip("5 — Fazendeiro (farm)", gatherFarm);

            EditorGUILayout.Space();
            outputPath = EditorGUILayout.TextField("Output", outputPath);

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(idle == null || walk == null || run == null))
                if (GUILayout.Button("Gerar Controller", GUILayout.Height(30)))
                    Generate();
        }

        private static AnimationClip Clip(string label, AnimationClip cur) =>
            (AnimationClip)EditorGUILayout.ObjectField(label, cur, typeof(AnimationClip), false);

        private void Generate()
        {
            string dir = Path.GetDirectoryName(outputPath).Replace('\\', '/');
            EnsureFolder(dir);

            // Overwrite cleanly so "regenerate" works (the params are final; only clips change).
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(outputPath) != null)
                AssetDatabase.DeleteAsset(outputPath);

            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(outputPath);

            ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("Grounded", AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("Hit", AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("Dead", AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("Mounted", AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("Gathering", AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("GatherType", AnimatorControllerParameterType.Int);
            // Start GROUNDED so a freshly-spawned character (or a preview where CharacterAnimator is disabled) doesn't
            // briefly play the Airborne/jump anim before the first frame sets it → "nasce pulando".
            SetBoolDefault(ctrl, "Grounded", true);

            var sm = ctrl.layers[0].stateMachine;

            // ── Locomotion: 1D blend by Speed (idle → walk → run [→ dash]) ──
            // NOTE: this overload RETURNS the AnimatorState and OUTs the BlendTree.
            AnimatorState loco = ctrl.CreateBlendTreeInController("Locomotion", out BlendTree tree, 0);
            tree.blendType = BlendTreeType.Simple1D;
            tree.blendParameter = "Speed";
            tree.useAutomaticThresholds = false;
            tree.AddChild(idle, 0f);
            tree.AddChild(walk, 0.4f);
            tree.AddChild(run, dash != null ? 0.75f : 1f);
            if (dash != null) tree.AddChild(dash, 1f);
            sm.defaultState = loco;

            // ── Jump / airborne: while NOT grounded ──
            if (jump != null)
            {
                var s = sm.AddState("Airborne");
                s.motion = jump;
                AnyTo(sm, s, ("Grounded", AnimatorConditionMode.IfNot, 0));
                Back(s, loco, ("Grounded", AnimatorConditionMode.If, 0));
            }

            // ── One-shot triggers: Attack, Hit (return on exit-time) ──
            if (attack != null) OneShot(sm, loco, "Attack", attack, "Attack");
            if (hit != null) OneShot(sm, loco, "Hit", hit, "Hit");

            // ── Held bools: Death, Mounted ──
            if (death != null) Held(sm, loco, "Death", death, "Dead");
            if (sit != null) Held(sm, loco, "Mounted", sit, "Mounted");

            // ── Gather: one looping state per profession, gated on Gathering==true && GatherType==N ──
            (AnimationClip clip, int type, string name)[] gathers =
            {
                (gatherChop, 0, "Chop"), (gatherMine, 1, "Mine"), (gatherHerb, 2, "Herb"),
                (gatherSkin, 3, "Skin"), (gatherFish, 4, "Fish"), (gatherFarm, 5, "Farm"),
            };
            foreach (var g in gathers)
                if (g.clip != null) GatherState(sm, loco, $"Gather_{g.name}", g.clip, g.type);

            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = ctrl;
            EditorGUIUtility.PingObject(ctrl);
            Debug.Log($"[Animation] Controller humanoide gerado em {outputPath}. " +
                      "Atribua-o nos modelos via o Character Setup Wizard (ou no Animator do modelo). " +
                      "Para um Attack/Gather que não corte a locomoção por baixo, depois dá pra promover esses estados a uma layer de Upper Body.");
        }

        // AnyState → state on a single condition; never self-interrupt.
        private static void AnyTo(AnimatorStateMachine sm, AnimatorState to, (string p, AnimatorConditionMode m, float v) cond)
        {
            var t = sm.AddAnyStateTransition(to);
            t.duration = 0.1f;
            t.hasExitTime = false;
            t.canTransitionToSelf = false;
            t.AddCondition(cond.m, cond.v, cond.p);
        }

        // state → back, on a single condition.
        private static void Back(AnimatorState from, AnimatorState to, (string p, AnimatorConditionMode m, float v) cond)
        {
            var t = from.AddTransition(to);
            t.duration = 0.1f;
            t.hasExitTime = false;
            t.AddCondition(cond.m, cond.v, cond.p);
        }

        // Trigger-driven one-shot (plays once, returns to locomotion when the clip finishes).
        private static void OneShot(AnimatorStateMachine sm, AnimatorState loco, string name, AnimationClip clip, string trigger)
        {
            var s = sm.AddState(name);
            s.motion = clip;
            AnyTo(sm, s, (trigger, AnimatorConditionMode.If, 0));
            var back = s.AddTransition(loco);
            back.hasExitTime = true;
            back.exitTime = 0.9f;
            back.duration = 0.1f;
        }

        // Bool-held state (stays while the bool is true, returns when false).
        private static void Held(AnimatorStateMachine sm, AnimatorState loco, string name, AnimationClip clip, string boolParam)
        {
            var s = sm.AddState(name);
            s.motion = clip;
            AnyTo(sm, s, (boolParam, AnimatorConditionMode.If, 0));
            Back(s, loco, (boolParam, AnimatorConditionMode.IfNot, 0));
        }

        // Per-profession gather: looping state entered when Gathering==true AND GatherType==N, exits when Gathering==false.
        private static void GatherState(AnimatorStateMachine sm, AnimatorState loco, string name, AnimationClip clip, int gatherType)
        {
            var s = sm.AddState(name);
            s.motion = clip;
            var enter = sm.AddAnyStateTransition(s);
            enter.duration = 0.1f;
            enter.hasExitTime = false;
            enter.canTransitionToSelf = false;
            enter.AddCondition(AnimatorConditionMode.If, 0, "Gathering");
            enter.AddCondition(AnimatorConditionMode.Equals, gatherType, "GatherType");
            Back(s, loco, ("Gathering", AnimatorConditionMode.IfNot, 0));
        }

        // Sets a bool parameter's DEFAULT value (AddParameter always defaults to false).
        private static void SetBoolDefault(AnimatorController ctrl, string name, bool value)
        {
            var ps = ctrl.parameters;
            for (int i = 0; i < ps.Length; i++) if (ps[i].name == name) { ps[i].defaultBool = value; break; }
            ctrl.parameters = ps;
        }

        private static void EnsureFolder(string dir)
        {
            if (string.IsNullOrEmpty(dir) || AssetDatabase.IsValidFolder(dir)) return;
            string parent = Path.GetDirectoryName(dir).Replace('\\', '/');
            string leaf = Path.GetFileName(dir);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
