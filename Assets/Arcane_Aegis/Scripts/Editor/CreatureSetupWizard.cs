using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using KinematicCharacterController;
using Arcane_Aegis.Content;
using Arcane_Aegis.Controllers;
using Arcane_Aegis.Controllers.Locomotion;
using Arcane_Aegis.Entities;

namespace Arcane_Aegis.EditorTools
{
    /// <summary>
    /// Invector-style one-click setup for a NON-humanoid creature — MONSTER, PET, VENDOR (NPC) or MOUNT. It builds a
    /// generic AnimatorController from the clips you drop (locomotion blend by Speed + optional Attack/Hit/Death), then:
    /// • Monster/Pet/Vendor — bakes a <c>model3D</c> prefab whose root has the Animator + a <see cref="CharacterAnimator"/>
    ///   (the model IS the entity at runtime — EntityManager instantiates it directly, no shared shell).
    /// • Mount — assembles the full rideable RIG (KinematicCharacterMotor + MountController + MountView + a CapsuleCollider,
    ///   with "RiderSeat"/"Target" children and the model under "Model" carrying the Animator + CharacterAnimator).
    /// Finally it creates/updates the matching Definition SO, assigns the prefab and registers it in the ContentLibrary.
    /// The param contract matches what the client drives; CharacterAnimator ignores params a creature lacks (no gather).
    /// Menu: ArcaneMMO ▸ Characters ▸ Creature Setup Wizard.
    /// </summary>
    public class CreatureSetupWizard : EditorWindow
    {
        private enum Kind { Monster, Pet, Mount, Npc }

        [SerializeField] private Kind kind = Kind.Monster;
        [SerializeField] private GameObject model;            // the creature FBX/model (generic rig)
        [SerializeField] private ContentLibrary library;

        // Existing definition to update (optional, by kind). If null, a new one is created from id/displayName.
        [SerializeField] private MonsterDefinitionSO monsterDef;
        [SerializeField] private PetDefinitionSO petDef;
        [SerializeField] private MountDefinitionSO mountDef;
        [SerializeField] private NpcDefinitionSO npcDef;
        [SerializeField] private string newId = "";
        [SerializeField] private string newDisplayName = "";

        // Npc only: its type + a seeded greeting (stock + the rest of the dialogue are edited in the NpcDefinition after).
        [SerializeField] private NpcDefinitionSO.NpcType npcType = NpcDefinitionSO.NpcType.Townsfolk;
        [SerializeField] private string npcGreeting = "";

        // Clips — idle is the minimum; a movement clip makes it blend; the rest are optional.
        [SerializeField] private AnimationClip idle, walk, run, attack, hit, death;
        [SerializeField] private bool mountCanFly = false; // mount only: flips MountController.canFly

        [SerializeField] private string prefabOutputDir = "Assets/Arcane_Aegis/Prefabs/Creatures";
        [SerializeField] private string controllerOutputDir = "Assets/Arcane_Aegis/Animation/Creatures";

        [MenuItem("ArcaneMMO/Characters/Creature Setup Wizard")]
        public static void Open() => GetWindow<CreatureSetupWizard>("Creature Setup");

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "Configura um MONSTRO, PET, NPC (falante/vendedor/guarda) ou MONTARIA (rig genérico) de uma vez:\n" +
                "• gera um AnimatorController das anims (locomoção por Speed + Attack/Hit/Death opcionais)\n" +
                "• Monstro/Pet/NPC → prefab model3D (Animator + CharacterAnimator) — esse modelo É a entidade\n" +
                "• NPC → semeia um diálogo inicial (saudação + 'Loja' se Tipo=Vendor) na NpcDefinition; o ESTOQUE você põe na NpcDefinition\n" +
                "• Montaria → rig completo montável (KCC + MountController + RiderSeat/Target + modelo em 'Model')\n" +
                "• cria/atualiza a Definition + assina o prefab + registra na ContentLibrary\n\n" +
                "Cada bicho tem clips próprios → 1 controller por bicho (gerado aqui). NPC precisa de um SpawnMarker.",
                MessageType.Info);

            kind = (Kind)EditorGUILayout.EnumPopup("Tipo", kind);
            model = (GameObject)EditorGUILayout.ObjectField("Modelo (FBX)", model, typeof(GameObject), false);
            library = (ContentLibrary)EditorGUILayout.ObjectField("ContentLibrary", library, typeof(ContentLibrary), false);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Definição (deixe vazio p/ criar nova)", EditorStyles.boldLabel);
            switch (kind)
            {
                case Kind.Monster: monsterDef = (MonsterDefinitionSO)EditorGUILayout.ObjectField("Monster Definition", monsterDef, typeof(MonsterDefinitionSO), false); break;
                case Kind.Pet:     petDef = (PetDefinitionSO)EditorGUILayout.ObjectField("Pet Definition", petDef, typeof(PetDefinitionSO), false); break;
                case Kind.Mount:   mountDef = (MountDefinitionSO)EditorGUILayout.ObjectField("Mount Definition", mountDef, typeof(MountDefinitionSO), false); break;
                case Kind.Npc:     npcDef = (NpcDefinitionSO)EditorGUILayout.ObjectField("NPC Definition", npcDef, typeof(NpcDefinitionSO), false); break;
            }

            bool hasDef = HasDef();
            using (new EditorGUI.DisabledScope(hasDef))
            {
                newId = EditorGUILayout.TextField("  Novo id (ex.: 'wolf')", newId);
                newDisplayName = EditorGUILayout.TextField("  Novo nome", newDisplayName);
            }

            if (kind == Kind.Npc)
            {
                npcType = (NpcDefinitionSO.NpcType)EditorGUILayout.EnumPopup("  Tipo de NPC", npcType);
                if (npcType == NpcDefinitionSO.NpcType.Vendor)
                    EditorGUILayout.HelpBox("Vendor: adicione o ESTOQUE (lista de itens) na NpcDefinition depois. O diálogo já ganha 'Loja'.", MessageType.None);
                npcGreeting = EditorGUILayout.TextField(new GUIContent("  Saudação", "Texto inicial do diálogo (semeado; edite o resto na NpcDefinition)."), npcGreeting);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Animações (idle obrigatório; movimento recomendado)", EditorStyles.boldLabel);
            idle = Clip("Idle (0.0)", idle);
            walk = Clip("Walk (~0.4)", walk);
            run  = Clip("Run (1.0) — opcional", run);
            if (kind == Kind.Mount)
            {
                mountCanFly = EditorGUILayout.Toggle("Montaria voadora (canFly)", mountCanFly);
            }
            else if (kind != Kind.Npc) // a talking NPC doesn't fight → no attack/hit/death
            {
                attack = Clip("Attack — opcional", attack);
                hit    = Clip("Hit (flinch) — opcional", hit);
                death  = Clip("Death — opcional", death);
            }

            EditorGUILayout.Space();
            prefabOutputDir = EditorGUILayout.TextField("Pasta dos prefabs", prefabOutputDir);
            controllerOutputDir = EditorGUILayout.TextField("Pasta dos controllers", controllerOutputDir);

            EditorGUILayout.Space();
            bool ready = model && library && idle != null && (hasDef || !string.IsNullOrWhiteSpace(newId));
            using (new EditorGUI.DisabledScope(!ready))
                if (GUILayout.Button("Configurar Criatura", GUILayout.Height(30)))
                    Setup();
        }

        private bool HasDef() => kind switch
        {
            Kind.Monster => monsterDef != null,
            Kind.Pet => petDef != null,
            Kind.Mount => mountDef != null,
            Kind.Npc => npcDef != null,
            _ => false,
        };

        private string DefId() => kind switch
        {
            Kind.Monster => monsterDef?.id,
            Kind.Pet => petDef?.id,
            Kind.Mount => mountDef?.id,
            Kind.Npc => npcDef?.id,
            _ => null,
        } ?? newId.Trim();

        private static AnimationClip Clip(string label, AnimationClip cur) =>
            (AnimationClip)EditorGUILayout.ObjectField(label, cur, typeof(AnimationClip), false);

        private void Setup()
        {
            string id = DefId();
            if (string.IsNullOrWhiteSpace(id)) { Debug.LogError("[Creature] id vazio — defina um id ou arraste uma definição."); return; }

            // 1. Per-creature generic controller from the clips.
            EnsureFolder(controllerOutputDir);
            string controllerPath = $"{controllerOutputDir}/{kind}_{id}.controller";
            var ctrl = BuildController(controllerPath);

            // 2. Build the prefab (model-as-entity for Monster/Pet/Vendor; full rig for Mount).
            EnsureFolder(prefabOutputDir);
            string prefabPath = $"{prefabOutputDir}/{kind}_{id}.prefab";
            GameObject prefab = kind == Kind.Mount ? BuildMountRig(prefabPath, ctrl) : BuildModelEntity(prefabPath, ctrl);
            if (prefab == null) return;

            // 3. Find/create the definition, assign the prefab, register in the library.
            switch (kind)
            {
                case Kind.Monster: UpsertMonster(id, prefab); break;
                case Kind.Pet:     UpsertPet(id, prefab); break;
                case Kind.Mount:   UpsertMount(id, prefab); break;
                case Kind.Npc:     UpsertNpc(id, prefab); break;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[Creature] OK: {kind} '{id}' → controller {controllerPath} + prefab {prefabPath}. " +
                      "Já resolve no spawn (exporte/sincronize o conteúdo se o id for novo; monstro precisa de um SpawnMarker).");
        }

        // Monster/Pet/Vendor: the model itself becomes the entity prefab (Animator + CharacterAnimator on the root).
        private GameObject BuildModelEntity(string prefabPath, AnimatorController ctrl)
        {
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
            try
            {
                var anim = inst.GetComponentInChildren<Animator>() ?? inst.AddComponent<Animator>();
                anim.runtimeAnimatorController = ctrl;   // avatar stays as imported (generic)
                anim.applyRootMotion = false;            // server-validated movement drives the transform
                WireCharacterAnimator(inst, anim);
                return PrefabUtility.SaveAsPrefabAsset(inst, prefabPath);
            }
            finally { DestroyImmediate(inst); }
        }

        // Mount: a complete rideable rig — root (KCC motor disabled + CapsuleCollider + MountController + MountView) with
        // children RiderSeat / Target / Model; the visual model goes under "Model" and carries the Animator + CharacterAnimator.
        private GameObject BuildMountRig(string prefabPath, AnimatorController ctrl)
        {
            var root = new GameObject($"Mount_{DefId()}");
            try
            {
                var mc = root.AddComponent<MountController>();              // RequireComponent pulls in KCC + CapsuleCollider
                mc.canFly = mountCanFly;
                var kcm = root.GetComponent<KinematicCharacterMotor>();
                if (kcm != null) kcm.enabled = false;                       // runtime enables it for the LOCAL rider
                root.AddComponent<MountView>();                            // snapshot interpolation for remotes

                var capsule = root.GetComponent<CapsuleCollider>();        // sane default so it isn't a unit capsule at origin
                if (capsule != null) { capsule.height = 2f; capsule.radius = 0.6f; capsule.center = new Vector3(0f, 1f, 0f); }

                Transform seat = NewChild(root.transform, "RiderSeat", new Vector3(0f, 1.2f, 0f));
                Transform target = NewChild(root.transform, "Target", new Vector3(0f, 1.6f, -0.2f));
                Transform modelHolder = NewChild(root.transform, "Model", Vector3.zero);
                mc.riderSeat = seat;
                mc.cameraTarget = target;

                var modelInst = (GameObject)PrefabUtility.InstantiatePrefab(model);
                modelInst.transform.SetParent(modelHolder, false);
                var anim = modelInst.GetComponentInChildren<Animator>() ?? modelInst.AddComponent<Animator>();
                anim.runtimeAnimatorController = ctrl;
                anim.applyRootMotion = false;
                WireCharacterAnimator(modelInst, anim);

                return PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally { DestroyImmediate(root); }
        }

        // Adds CharacterAnimator (what EntityView/MountView push Speed/State into) and points it at the Animator.
        private static void WireCharacterAnimator(GameObject go, Animator anim)
        {
            var ca = go.GetComponent<CharacterAnimator>() ?? go.AddComponent<CharacterAnimator>();
            var so = new SerializedObject(ca);
            var p = so.FindProperty("animator");
            if (p != null) { p.objectReferenceValue = anim; so.ApplyModifiedPropertiesWithoutUndo(); }
        }

        private void UpsertMonster(string id, GameObject prefab)
        {
            var def = monsterDef;
            if (def == null) { def = NewAsset<MonsterDefinitionSO>(id, "Monster"); def.id = id; def.displayName = DisplayName(id); }
            def.model3D = prefab; EditorUtility.SetDirty(def);
            Register(library.monsters, def);
            Reveal(def);
        }

        private void UpsertPet(string id, GameObject prefab)
        {
            var def = petDef;
            if (def == null) { def = NewAsset<PetDefinitionSO>(id, "Pet"); def.id = id; def.displayName = DisplayName(id); }
            def.model3D = prefab; EditorUtility.SetDirty(def);
            Register(library.pets, def);
            Reveal(def);
        }

        private void UpsertNpc(string id, GameObject prefab)
        {
            var def = npcDef;
            if (def == null) { def = NewAsset<NpcDefinitionSO>(id, "Npc"); def.id = id; def.displayName = DisplayName(id); }
            def.model3D = prefab;
            def.type = npcType;
            bool sells = npcType == NpcDefinitionSO.NpcType.Vendor;
            // Seed a starter dialogue only if none authored yet (a greeting + "Loja" if it sells + "Adeus").
            if (def.nodes == null || def.nodes.Count == 0)
            {
                string greet = string.IsNullOrWhiteSpace(npcGreeting) ? $"Olá, viajante. Sou {DisplayName(id)}." : npcGreeting.Trim();
                var node = new NpcDefinitionSO.Node { id = "start", text = greet, options = new System.Collections.Generic.List<NpcDefinitionSO.Option>() };
                if (sells)
                    node.options.Add(new NpcDefinitionSO.Option { label = "Loja", action = NpcDefinitionSO.DialogueAction.OpenShop });
                node.options.Add(new NpcDefinitionSO.Option { label = "Adeus", action = NpcDefinitionSO.DialogueAction.End });
                def.nodes = new System.Collections.Generic.List<NpcDefinitionSO.Node> { node };
            }
            EditorUtility.SetDirty(def);
            Register(library.npcs, def);
            Reveal(def);
        }

        private void UpsertMount(string id, GameObject prefab)
        {
            var def = mountDef;
            if (def == null) { def = NewAsset<MountDefinitionSO>(id, "Mount"); def.id = id; def.displayName = DisplayName(id); }
            def.mountPrefab = prefab; EditorUtility.SetDirty(def);
            Register(library.mounts, def);
            Reveal(def);
        }

        private string DisplayName(string id) => string.IsNullOrWhiteSpace(newDisplayName) ? id : newDisplayName.Trim();

        private T NewAsset<T>(string id, string prefix) where T : ScriptableObject
        {
            var def = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(def, AssetDatabase.GenerateUniqueAssetPath($"{prefabOutputDir}/{prefix}_{id}.asset"));
            return def;
        }

        private void Register<T>(System.Collections.Generic.List<T> list, T def) where T : Object
        {
            if (!list.Contains(def)) { list.Add(def); EditorUtility.SetDirty(library); }
        }

        private static void Reveal(Object o) { Selection.activeObject = o; EditorGUIUtility.PingObject(o); }

        // A generic creature controller: only the params the client drives for a mob/pet/vendor/mount (Speed, Grounded,
        // Attack, Hit, Dead). No gather/mount params — CharacterAnimator no-ops the ones a creature doesn't have.
        private AnimatorController BuildController(string path)
        {
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(path) != null) AssetDatabase.DeleteAsset(path);
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);

            ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("Grounded", AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("Hit", AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("Dead", AnimatorControllerParameterType.Bool);

            var sm = ctrl.layers[0].stateMachine;

            // Locomotion: 1D blend by Speed. idle is required; walk and/or run fill the upper end (idle-only = always idle).
            AnimatorState loco = ctrl.CreateBlendTreeInController("Locomotion", out BlendTree tree, 0);
            tree.blendType = BlendTreeType.Simple1D;
            tree.blendParameter = "Speed";
            tree.useAutomaticThresholds = false;
            tree.AddChild(idle, 0f);
            if (walk != null) tree.AddChild(walk, run != null ? 0.4f : 1f);
            if (run != null) tree.AddChild(run, 1f);
            sm.defaultState = loco;

            if (attack != null) OneShot(sm, loco, "Attack", attack, "Attack");
            if (hit != null) OneShot(sm, loco, "Hit", hit, "Hit");
            if (death != null) Held(sm, loco, "Death", death, "Dead");

            EditorUtility.SetDirty(ctrl);
            return ctrl;
        }

        // Trigger-driven one-shot (plays once, returns to locomotion when the clip finishes).
        private static void OneShot(AnimatorStateMachine sm, AnimatorState loco, string name, AnimationClip clip, string trigger)
        {
            var s = sm.AddState(name);
            s.motion = clip;
            var enter = sm.AddAnyStateTransition(s);
            enter.duration = 0.1f; enter.hasExitTime = false; enter.canTransitionToSelf = false;
            enter.AddCondition(AnimatorConditionMode.If, 0, trigger);
            var back = s.AddTransition(loco);
            back.hasExitTime = true; back.exitTime = 0.9f; back.duration = 0.1f;
        }

        // Bool-held state (stays while the bool is true, returns when false).
        private static void Held(AnimatorStateMachine sm, AnimatorState loco, string name, AnimationClip clip, string boolParam)
        {
            var s = sm.AddState(name);
            s.motion = clip;
            var enter = sm.AddAnyStateTransition(s);
            enter.duration = 0.1f; enter.hasExitTime = false; enter.canTransitionToSelf = false;
            enter.AddCondition(AnimatorConditionMode.If, 0, boolParam);
            var back = s.AddTransition(loco);
            back.duration = 0.1f; back.hasExitTime = false;
            back.AddCondition(AnimatorConditionMode.IfNot, 0, boolParam);
        }

        private static Transform NewChild(Transform parent, string name, Vector3 localPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            return go.transform;
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
