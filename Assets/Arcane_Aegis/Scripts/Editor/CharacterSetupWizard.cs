using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Arcane_Aegis.Content;

namespace Arcane_Aegis.EditorTools
{
    /// <summary>
    /// Invector-style one-click setup for a HUMANOID character. You drop a Humanoid model + pick race/class/gender +
    /// the shared Humanoid controller, and it: (1) builds a MODEL PREFAB whose Animator uses the shared controller +
    /// the model's Humanoid avatar, and (2) creates/updates the matching <see cref="CharacterTemplateSO"/> entry and
    /// registers it in the <see cref="ContentLibrary"/>. No gameplay prefab per character — the runtime mounts this
    /// model onto the shared character prefab at spawn (EntityManager.ResolveModel). Menu: ArcaneMMO ▸ Characters ▸ Setup Wizard.
    /// </summary>
    public class CharacterSetupWizard : EditorWindow
    {
        [SerializeField] private GameObject model;            // the Humanoid FBX/model
        [SerializeField] private RaceDefinitionSO race;
        [SerializeField] private ClassDefinitionSO characterClass;
        [SerializeField] private GenderDefinitionSO gender;
        [SerializeField] private AnimatorController sharedController; // from the Humanoid Controller Generator
        [SerializeField] private ContentLibrary library;
        [SerializeField] private string modelOutputDir = "Assets/Arcane_Aegis/Prefabs/Characters";
        [SerializeField] private string templateOutputDir = "Assets/Arcane_Aegis/Content/Templates";
        [SerializeField] private bool createSockets = true; // auto-create Socket_MainHand (right hand) + Socket_Back (chest)

        [MenuItem("ArcaneMMO/Characters/Setup Wizard")]
        public static void Open() => GetWindow<CharacterSetupWizard>("Character Setup");

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "Configura um personagem humanoide de uma vez:\n" +
                "• prepara o MODELO (Animator + controller compartilhado + avatar Humanoid)\n" +
                "• cria/atualiza o CharacterTemplate (raça+classe) com o modelo desse gênero\n" +
                "• registra o template na ContentLibrary\n\n" +
                "Pré-requisito: o modelo (FBX) deve estar importado como Rig ▸ Humanoid, e o controller vem do " +
                "ArcaneMMO ▸ Animation ▸ Humanoid Controller Generator.",
                MessageType.Info);

            model = (GameObject)EditorGUILayout.ObjectField("Modelo (FBX)", model, typeof(GameObject), false);
            race = (RaceDefinitionSO)EditorGUILayout.ObjectField("Raça", race, typeof(RaceDefinitionSO), false);
            characterClass = (ClassDefinitionSO)EditorGUILayout.ObjectField("Classe", characterClass, typeof(ClassDefinitionSO), false);
            gender = (GenderDefinitionSO)EditorGUILayout.ObjectField("Gênero", gender, typeof(GenderDefinitionSO), false);
            sharedController = (AnimatorController)EditorGUILayout.ObjectField("Controller compartilhado", sharedController, typeof(AnimatorController), false);
            library = (ContentLibrary)EditorGUILayout.ObjectField("ContentLibrary", library, typeof(ContentLibrary), false);

            EditorGUILayout.Space();
            modelOutputDir = EditorGUILayout.TextField("Pasta dos modelos", modelOutputDir);
            templateOutputDir = EditorGUILayout.TextField("Pasta dos templates", templateOutputDir);
            createSockets = EditorGUILayout.Toggle("Criar sockets de arma", createSockets);

            EditorGUILayout.Space();
            bool ready = model && race && characterClass && gender && sharedController && library;
            using (new EditorGUI.DisabledScope(!ready))
                if (GUILayout.Button("Configurar Personagem", GUILayout.Height(30)))
                    Setup();
        }

        private void Setup()
        {
            // 1. Warn (don't auto-modify the importer) if the model isn't a Humanoid rig — retargeting needs it.
            var probe = model.GetComponentInChildren<Animator>();
            if (probe == null || probe.avatar == null || !probe.avatar.isHuman)
                Debug.LogWarning($"[Characters] '{model.name}' não parece estar como Rig ▸ Humanoid (avatar Humanoid ausente). " +
                                 "O controller compartilhado retargeta via Humanoid — ajuste o import do FBX (Rig ▸ Animation Type ▸ Humanoid) e rode de novo.");

            // 2. Build the model prefab (Animator + shared controller + the model's own avatar).
            EnsureFolder(modelOutputDir);
            string baseName = $"{race.id}_{characterClass.id}_{gender.id}";
            string modelPath = $"{modelOutputDir}/{baseName}.prefab";

            var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
            GameObject modelPrefab;
            try
            {
                var anim = inst.GetComponentInChildren<Animator>();
                if (anim == null) anim = inst.AddComponent<Animator>();
                anim.runtimeAnimatorController = sharedController; // avatar stays as imported (Humanoid)
                anim.applyRootMotion = false;                      // server-validated movement drives the transform, not root motion
                if (createSockets) CreateWeaponSockets(inst, anim); // Socket_MainHand / Socket_Back on the rig's bones
                modelPrefab = PrefabUtility.SaveAsPrefabAsset(inst, modelPath);
            }
            finally
            {
                DestroyImmediate(inst);
            }

            // 3. Find or create the CharacterTemplate for this race+class.
            CharacterTemplateSO tpl = library.templates.Find(t => t != null && t.race == race && t.characterClass == characterClass);
            if (tpl == null)
            {
                EnsureFolder(templateOutputDir);
                tpl = ScriptableObject.CreateInstance<CharacterTemplateSO>();
                tpl.id = $"{race.id}_{characterClass.id}";
                tpl.displayName = $"{race.name} {characterClass.name}";
                tpl.race = race;
                tpl.characterClass = characterClass;
                string tplPath = AssetDatabase.GenerateUniqueAssetPath($"{templateOutputDir}/Template_{tpl.id}.asset");
                AssetDatabase.CreateAsset(tpl, tplPath);
                library.templates.Add(tpl);
                EditorUtility.SetDirty(library);
            }

            // 4. Add or replace this gender's model on the template.
            var gm = tpl.genders.Find(g => g != null && g.gender == gender);
            if (gm == null) { gm = new GenderModel { gender = gender }; tpl.genders.Add(gm); }
            gm.model = modelPrefab;
            EditorUtility.SetDirty(tpl);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = tpl;
            EditorGUIUtility.PingObject(tpl);
            Debug.Log($"[Characters] OK: {race.name} {characterClass.name} ({gender.name}) → modelo {modelPath} + template '{tpl.id}'. " +
                      "Já entra na criação/seleção e no spawn no mundo. (Repita pros outros gêneros/classes.)");
        }

        // Auto-create the weapon sockets on the Humanoid rig (WeaponVisual finds them by name; ItemDefinitionSO.AttachPoint
        // can nudge per weapon). Socket_MainHand → right hand, Socket_Back → chest (fallback spine). Skips if they exist.
        private static void CreateWeaponSockets(GameObject root, Animator anim)
        {
            if (anim.avatar == null || !anim.avatar.isHuman)
            {
                Debug.LogWarning("[Characters] modelo sem avatar Humanoid → não dá pra localizar os ossos; sockets NÃO criados (ajuste o Rig p/ Humanoid ou coloque-os à mão).");
                return;
            }
            Transform hand = FindBone(anim, root, HumanBodyBones.RightHand, new[] { "righthand", "hand_r", "r_hand", "handr" });
            Transform back = FindBone(anim, root, HumanBodyBones.Chest, new[] { "chest", "upperchest", "spine2", "spine_02" })
                          ?? FindBone(anim, root, HumanBodyBones.Spine, new[] { "spine" });
            EnsureSocket(root, hand, "Socket_MainHand");
            EnsureSocket(root, back, "Socket_Back");
        }

        private static Transform FindBone(Animator anim, GameObject root, HumanBodyBones bone, string[] hints)
        {
            Transform t = null;
            try { t = anim.GetBoneTransform(bone); } catch { /* avatar not initialized in editor → fall back to name search */ }
            if (t != null) return t;
            foreach (var tr in root.GetComponentsInChildren<Transform>())
            {
                string n = tr.name.ToLowerInvariant();
                foreach (var h in hints) if (n.Contains(h)) return tr;
            }
            return null;
        }

        private static void EnsureSocket(GameObject root, Transform parent, string name)
        {
            if (parent == null) { Debug.LogWarning($"[Characters] osso para {name} não encontrado → socket não criado (coloque-o à mão)."); return; }
            foreach (var tr in root.GetComponentsInChildren<Transform>()) if (tr.name == name) return; // already present
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
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
