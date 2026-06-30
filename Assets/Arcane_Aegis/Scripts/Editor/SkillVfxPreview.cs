using UnityEditor;
using UnityEngine;
using Arcane_Aegis.Content;
using Arcane_Aegis.Combat;
using ArcaneShared.Enums;

namespace Arcane_Aegis.EditorTools
{
    /// <summary>
    /// One-stop skill VFX previewer/tuner. Spawn a test rig (a character model), then either:
    /// • fire any VFX STATICALLY (slash / cast / muzzle / projectile / impact) placed with the EXACT game math
    ///   (<see cref="SkillOrigin"/> + the per-skill Euler) — auto-selected so the Scene's particle preview plays it; or
    /// • PLAY the swing animation and fire the slash at the skill's <see cref="SkillDefinitionSO.releaseTime"/>, with the
    ///   VFX particles simulated on the same clock, to SYNC the arc to the blade.
    /// Tweak releaseTime / Euler / offset here (saved on the skill, client-art → no re-sync) and re-fire until it lines
    /// up — WYSIWYG with the game. Menu: ArcaneMMO ▸ Characters ▸ Skill VFX Preview.
    /// </summary>
    public class SkillVfxPreview : EditorWindow
    {
        [SerializeField] private SkillDefinitionSO skill;
        [SerializeField] private GameObject modelPrefab;            // a character model; needs Animator+controller for the swing
        [SerializeField] private GameObject weaponPrefab;          // optional weapon → attached to the hand (shows blade + trail)
        [SerializeField] private AnimationClip attackClip;         // the swing clip → add the "Release" event onto it
        [SerializeField] private RuntimeAnimatorController controllerOverride; // optional, if the model has none
        [SerializeField] private string attackTrigger = "Attack";
        [SerializeField] private float maxSeconds = 1.6f;          // how long to drive the swing
        [SerializeField] private float speed = 0.4f;               // playback speed (slow-mo to read the sync frame)
        [SerializeField] private bool loop = true;                 // auto-replay the swing

        private GameObject _rig;
        private Animator _animator;
        private GameObject _weapon;
        private TrailRenderer _trail;
        private GameObject _vfx;
        private ParticleSystem[] _vfxPs;
        private SerializedObject _so;
        private Vector2 _scroll;

        private bool _modelAuto = true, _weaponAuto = true, _clipAuto = true; // auto-fill from the skill until the user picks one

        // Projectile flight preview (the bola flying forward like in-game).
        private GameObject _muzzle;
        private bool _flying;
        private Vector3 _projStart, _projDir;
        private float _projSpeed, _projElapsed;

        private bool _playing;
        private bool _paused;   // freeze the swing on a frame to tune the slash's offset/rotation live
        private double _lastTime, _fireAt, _elapsed;
        private bool _fired;

        // Tracks the live static VFX so the offset/rotation sliders below update it in realtime (no re-fire needed).
        private bool _vfxFaceForward;
        private bool _vfxUsesOrigin; // positioned via SkillOrigin (offset slider applies) vs a fixed spot (impact)
        private int _vfxEulerKind;   // 0 = none, 1 = slash euler, 2 = projectile euler

        [MenuItem("ArcaneMMO/Characters/Skill VFX Preview")]
        public static void Open() => GetWindow<SkillVfxPreview>("Skill VFX Preview");

        private void OnEnable() => SceneView.duringSceneGui += OnScene;
        private void OnDisable() { SceneView.duringSceneGui -= OnScene; Tools.hidden = false; Stop(); Cleanup(); }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.HelpBox(
                "Afina os VFX da skill sem entrar em Play.\n" +
                "1) Escolha a Skill → o Modelo (classe que usa a skill) e a Arma (categoria exigida) entram sozinhos.\n" +
                "2) 'Montar rig'.\n" +
                "3a) 'Disparar VFX' = coloca estático (selecionado → use o preview de partículas da Scene).\n" +
                "3b) 'Tocar golpe' = roda a animação e solta o slash no releaseTime (sincroniza com a lâmina).\n" +
                "4) Ajuste releaseTime / Euler / offset embaixo (salva na skill) e refaça.",
                MessageType.Info);

            EditorGUI.BeginChangeCheck();
            skill = (SkillDefinitionSO)EditorGUILayout.ObjectField("Skill", skill, typeof(SkillDefinitionSO), false);
            if (EditorGUI.EndChangeCheck()) { _so = null; AutoResolveFromSkill(); } // picking a skill → pull its class model + required weapon

            EditorGUI.BeginChangeCheck();
            modelPrefab = (GameObject)EditorGUILayout.ObjectField("Modelo", modelPrefab, typeof(GameObject), false);
            if (EditorGUI.EndChangeCheck()) { _modelAuto = false; ResolveAttackClip(); } // manual pick → stop auto-overriding, but refind the clip
            EditorGUI.BeginChangeCheck();
            weaponPrefab = (GameObject)EditorGUILayout.ObjectField("Arma (opcional)", weaponPrefab, typeof(GameObject), false);
            if (EditorGUI.EndChangeCheck()) _weaponAuto = false;
            EditorGUI.BeginChangeCheck();
            attackClip = (AnimationClip)EditorGUILayout.ObjectField("Clip do golpe (p/ evento)", attackClip, typeof(AnimationClip), false);
            if (EditorGUI.EndChangeCheck()) _clipAuto = false;
            EditorGUI.BeginChangeCheck();
            controllerOverride = (RuntimeAnimatorController)EditorGUILayout.ObjectField("Controller (opcional)", controllerOverride, typeof(RuntimeAnimatorController), false);
            if (EditorGUI.EndChangeCheck()) ResolveAttackClip();
            attackTrigger = EditorGUILayout.TextField("Trigger do golpe", attackTrigger);

            using (new EditorGUILayout.HorizontalScope())
            {
                speed = EditorGUILayout.Slider(new GUIContent("Velocidade", "Câmera lenta pra ler o frame da lâmina."), speed, 0.1f, 1.5f);
                loop = EditorGUILayout.ToggleLeft("Loop", loop, GUILayout.Width(60));
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Montar rig")) BuildRig();
                using (new EditorGUI.DisabledScope(_rig == null)) if (GUILayout.Button("Remover rig")) Cleanup();
                using (new EditorGUI.DisabledScope(skill == null))
                    if (GUILayout.Button("🔄 Puxar da skill", GUILayout.Width(130)))
                        { _modelAuto = _weaponAuto = true; AutoResolveFromSkill(); }
            }
            using (new EditorGUI.DisabledScope(modelPrefab == null))
                if (GUILayout.Button("➕ CombatAnimEvents no PREFAB do modelo (pro jogo)"))
                    AddEventsComponentToModel();

            EditorGUILayout.Space();
            if (skill == null) EditorGUILayout.HelpBox("Escolha uma Skill acima.", MessageType.None);
            else
            {
                // ── Animated swing ──
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(_rig == null || _animator == null))
                        if (GUILayout.Button(_playing ? "■ Parar" : "▶ Tocar golpe (anim + slash no releaseTime)", GUILayout.Height(28)))
                            { if (_playing) Stop(); else PlaySwing(); }
                    using (new EditorGUI.DisabledScope(!_playing))
                        if (GUILayout.Button(_paused ? "▶ Continuar" : "⏸ Pausar", GUILayout.Height(28), GUILayout.Width(110)))
                            _paused = !_paused;
                }
                if (_playing)
                    EditorGUILayout.LabelField($"t = {_elapsed:0.00}s    release = {skill.releaseTime:0.00}s {(_fired ? "✓ slash!" : "…")}{(_paused ? "  ⏸ PAUSADO — arraste Origem/Rotação" : "")}", EditorStyles.miniLabel);

                // Frame-exact: bake a "Release" Animation Event onto the clip at the current releaseTime (+ enable useAnimEvent).
                using (new EditorGUI.DisabledScope(attackClip == null))
                    if (GUILayout.Button($"➕ Gravar evento 'Release' no clip @ {skill.releaseTime:0.00}s"))
                        AddReleaseEvent();

                // ── Static fire ──
                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField("Disparar VFX (estático)", EditorStyles.boldLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Slash"))    FireStatic(skill.slashVfx, 1, true);
                    if (GUILayout.Button("Cast"))     FireStatic(skill.castVfx, 3, true);
                    if (GUILayout.Button("Muzzle"))   FireStatic(skill.muzzleVfx, 0, true);
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Projétil (parado)")) FireStatic(skill.projectileVfx, 2, true);
                    if (GUILayout.Button(_flying ? "■ Parar voo" : "Projétil ▶ voar")) { if (_flying) StopFlight(); else FireProjectileFly(); }
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Impacto"))  FireImpact();
                    if (GUILayout.Button("Limpar VFX")) ClearVfx();
                }

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Ajustes (salva na skill • arraste no Scene: W move / E gira)", EditorStyles.boldLabel);
                DrawTuning();
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawTuning()
        {
            if (skill == null) return;
            if (_so == null || _so.targetObject != skill) _so = new SerializedObject(skill);
            _so.Update();
            EditorGUILayout.PropertyField(_so.FindProperty("releaseTime"), new GUIContent("Release (s) — auge do golpe"));
            EditorGUILayout.PropertyField(_so.FindProperty("spawnOffset"), new GUIContent("Origem slash/projétil (offset) x=dir y=cima z=frente"));
            EditorGUILayout.PropertyField(_so.FindProperty("slashVfxEuler"), new GUIContent("Rotação do slash"));
            EditorGUILayout.PropertyField(_so.FindProperty("projectileVfxEuler"), new GUIContent("Rotação do projétil"));
            EditorGUILayout.PropertyField(_so.FindProperty("castOffset"), new GUIContent("Origem do cast (separada) x=dir y=cima z=frente"));
            EditorGUILayout.PropertyField(_so.FindProperty("castVfxEuler"), new GUIContent("Rotação do cast"));
            EditorGUILayout.Space(2);
            EditorGUILayout.PropertyField(_so.FindProperty("slashVfx"), new GUIContent("Slash VFX"));
            EditorGUILayout.PropertyField(_so.FindProperty("castVfx"), new GUIContent("Cast VFX"));
            EditorGUILayout.PropertyField(_so.FindProperty("muzzleVfx"), new GUIContent("Muzzle VFX"));
            EditorGUILayout.PropertyField(_so.FindProperty("projectileVfx"), new GUIContent("Projétil VFX"));
            EditorGUILayout.PropertyField(_so.FindProperty("impactVfx"), new GUIContent("Impacto VFX"));
            EditorGUILayout.PropertyField(_so.FindProperty("animTrigger"), new GUIContent("Trigger de anim da skill"));
            if (_so.ApplyModifiedProperties() && _vfx != null) ApplyLiveTransform(); // slider edit → move/rotate the live VFX
        }

        // Pull the "right" model + weapon straight from the picked skill: the model of a class that can cast it, and an
        // item of the skill's requiredWeapon category — so choosing a skill already fills the rig. Respects manual picks.
        private void AutoResolveFromSkill()
        {
            if (skill == null) return;
            var lib = FindLibrary();
            if (lib == null) return;

            if (_modelAuto && lib.templates != null)
            {
                var tpl = lib.templates.Find(t => t != null && t.characterClass != null
                    && t.characterClass.skillIds != null && t.characterClass.skillIds.Contains(skill.id)
                    && t.genders != null && t.genders.Exists(g => g != null && g.model != null));
                var gm = tpl?.genders.Find(g => g != null && g.model != null);
                if (gm != null) modelPrefab = gm.model;
            }

            string cat = skill.requiredWeapon;
            if (_weaponAuto && lib.items != null && !string.IsNullOrEmpty(cat) && cat != "any" && cat != "none")
            {
                var item = lib.items.Find(i => i != null && i.model3D != null && i.category == cat);
                if (item != null) weaponPrefab = item.model3D;
            }

            ResolveAttackClip();
        }

        // Pull the swing clip from the model's animator controller (or the override): the clip named like the attack
        // trigger, else any "attack"/"slash"/"swing" clip. So you don't hand-pick it just to bake the Release event.
        private void ResolveAttackClip()
        {
            if (!_clipAuto) return;
            RuntimeAnimatorController rac = controllerOverride;
            if (rac == null && modelPrefab != null)
            {
                var a = modelPrefab.GetComponentInChildren<Animator>();
                rac = a != null ? a.runtimeAnimatorController : null;
            }
            if (rac == null) return;
            string trig = string.IsNullOrEmpty(skill != null ? skill.animTrigger : null) ? attackTrigger : skill.animTrigger;
            AnimationClip best = null, attackish = null;
            foreach (var c in rac.animationClips)
            {
                if (c == null) continue;
                string n = c.name.ToLowerInvariant();
                if (!string.IsNullOrEmpty(trig) && n.Contains(trig.ToLowerInvariant())) { best = c; break; }
                if (attackish == null && (n.Contains("attack") || n.Contains("slash") || n.Contains("swing"))) attackish = c;
            }
            attackClip = best ?? attackish ?? attackClip;
        }

        private static ContentLibrary FindLibrary()
        {
            if (ContentLibrary.Active != null) return ContentLibrary.Active;
            var guids = AssetDatabase.FindAssets("t:ContentLibrary");
            return guids.Length > 0 ? AssetDatabase.LoadAssetAtPath<ContentLibrary>(AssetDatabase.GUIDToAssetPath(guids[0])) : null;
        }

        private void BuildRig()
        {
            Cleanup();
            if (modelPrefab != null)
            {
                _rig = (GameObject)PrefabUtility.InstantiatePrefab(modelPrefab);
                _animator = _rig.GetComponentInChildren<Animator>();
                if (_animator != null)
                {
                    if (_animator.runtimeAnimatorController == null) _animator.runtimeAnimatorController = controllerOverride;
                    _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    // Receiver for the "Release" animation event (silences "has no receiver" in the preview).
                    if (_animator.GetComponent<Arcane_Aegis.Entities.CombatAnimEvents>() == null)
                        _animator.gameObject.AddComponent<Arcane_Aegis.Entities.CombatAnimEvents>();
                }
            }
            else _rig = new GameObject("__SkillPreviewDummy"); // no model → static-only dummy (faces +Z)

            _rig.transform.position = Vector3.zero;
            _rig.transform.rotation = Quaternion.identity;
            AttachWeapon();
            Selection.activeGameObject = _rig;
            FrameScene();
        }

        // Attach the weapon to the main-hand socket (or the rig root) so the blade + its WeaponTrail show during the swing.
        private void AttachWeapon()
        {
            if (weaponPrefab == null) return;
            Transform hand = FindDeep(_rig.transform, "Socket_MainHand")
                          ?? (_animator != null && _animator.isHuman ? _animator.GetBoneTransform(HumanBodyBones.RightHand) : null)
                          ?? _rig.transform;
            _weapon = (GameObject)PrefabUtility.InstantiatePrefab(weaponPrefab);
            _weapon.transform.SetParent(hand, false);
            _weapon.transform.localPosition = Vector3.zero;
            _trail = _weapon.GetComponentInChildren<TrailRenderer>(true);
            if (_trail != null) { _trail.emitting = false; _trail.Clear(); }
        }

        private static Transform FindDeep(Transform parent, string name)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform c = parent.GetChild(i);
                if (c.name == name) return c;
                Transform r = FindDeep(c, name);
                if (r != null) return r;
            }
            return null;
        }

        // ── Static placement (matches the game's spawn math) ──
        private void FireStatic(GameObject prefab, int eulerKind, bool faceForward)
        {
            if (_rig == null) BuildRig();
            if (prefab == null) { ShowNotification(new GUIContent("Esse campo de VFX está vazio.")); return; }
            Stop(); ClearVfx();
            _vfxEulerKind = eulerKind; _vfxFaceForward = faceForward; _vfxUsesOrigin = true;
            Vector3 fwd = Forward();
            Vector3 pos = SkillOrigin.Resolve(_rig.transform, OffsetFor(eulerKind));
            Quaternion rot = faceForward ? Quaternion.LookRotation(fwd) * Quaternion.Euler(EulerFor(eulerKind)) : _rig.transform.rotation;
            _vfx = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            _vfx.transform.SetPositionAndRotation(pos, rot);
            Strip(_vfx);
            Selection.activeGameObject = _vfx; // Scene's particle preview overlay plays it
            FrameScene();
        }

        private Vector3 EulerFor(int kind) => kind == 1 ? skill.slashVfxEuler : kind == 2 ? skill.projectileVfxEuler : kind == 3 ? skill.castVfxEuler : Vector3.zero;
        private Vector3 OffsetFor(int kind) => kind == 3 ? skill.castOffset : skill.spawnOffset; // cast has its OWN origin

        // Re-place the live static VFX from the skill's current offset+Euler (called each repaint while the sliders move).
        private void ApplyLiveTransform()
        {
            if (_vfx == null || _rig == null) return;
            bool changed = false;
            if (_vfxUsesOrigin)
            {
                Vector3 pos = SkillOrigin.Resolve(_rig.transform, OffsetFor(_vfxEulerKind));
                if (_vfx.transform.position != pos) { _vfx.transform.position = pos; changed = true; }
            }
            if (_vfxFaceForward)
            {
                Quaternion rot = Quaternion.LookRotation(Forward()) * Quaternion.Euler(EulerFor(_vfxEulerKind));
                if (_vfx.transform.rotation != rot) { _vfx.transform.rotation = rot; changed = true; }
            }
            if (changed) SceneView.RepaintAll();
        }

        // Scene-view manipulation: drag the live VFX with W (move) / E (rotate) and bake the result back into the skill's
        // spawnOffset / Euler — the fastest way to PLACE a VFX. Hides Unity's built-in tool so there's only one handle.
        private void OnScene(SceneView sv)
        {
            if (skill == null || _vfx == null || _rig == null || _flying) { Tools.hidden = false; return; }
            Tools.hidden = true;
            Transform t = _vfx.transform;
            Quaternion facing = Quaternion.LookRotation(Forward());

            if (Tools.current == Tool.Rotate)
            {
                EditorGUI.BeginChangeCheck();
                Quaternion nr = Handles.RotationHandle(t.rotation, t.position);
                if (EditorGUI.EndChangeCheck())
                {
                    t.rotation = nr;
                    if (_vfxFaceForward && _vfxEulerKind != 0) WriteEuler((Quaternion.Inverse(facing) * nr).eulerAngles);
                    Repaint();
                }
            }
            else // move (default / W)
            {
                EditorGUI.BeginChangeCheck();
                Vector3 np = Handles.PositionHandle(t.position, Tools.pivotRotation == PivotRotation.Local ? t.rotation : Quaternion.identity);
                if (EditorGUI.EndChangeCheck())
                {
                    t.position = np;
                    if (_vfxUsesOrigin)
                    {
                        Vector3 base0 = SkillOrigin.Resolve(_rig.transform, Vector3.zero); // origin without the offset
                        WriteOffset(Quaternion.Inverse(facing) * (np - base0));
                    }
                    Repaint();
                }
            }
        }

        private void WriteOffset(Vector3 local)
        {
            string prop = _vfxEulerKind == 3 ? "castOffset" : "spawnOffset"; // cast writes its own origin
            if (_so == null || _so.targetObject != skill) _so = new SerializedObject(skill);
            _so.Update();
            _so.FindProperty(prop).vector3Value = Round(local, 3);
            _so.ApplyModifiedProperties();
        }

        private void WriteEuler(Vector3 euler)
        {
            string prop = _vfxEulerKind == 1 ? "slashVfxEuler" : _vfxEulerKind == 2 ? "projectileVfxEuler" : _vfxEulerKind == 3 ? "castVfxEuler" : null;
            if (prop == null) return;
            if (_so == null || _so.targetObject != skill) _so = new SerializedObject(skill);
            _so.Update();
            _so.FindProperty(prop).vector3Value = Round(euler, 1);
            _so.ApplyModifiedProperties();
        }

        private static Vector3 Round(Vector3 v, int d)
        {
            float m = Mathf.Pow(10, d);
            return new Vector3(Mathf.Round(v.x * m) / m, Mathf.Round(v.y * m) / m, Mathf.Round(v.z * m) / m);
        }

        private void FireImpact()
        {
            if (_rig == null) BuildRig();
            if (skill.impactVfx == null) { ShowNotification(new GUIContent("Impacto VFX vazio.")); return; }
            Stop(); ClearVfx();
            _vfxUsesOrigin = false; _vfxFaceForward = false; // fixed fake-target spot → offset/rotation sliders don't apply
            Vector3 pos = _rig.transform.position + Forward() * 3f + Vector3.up; // a fake target 3m ahead
            _vfx = (GameObject)PrefabUtility.InstantiatePrefab(skill.impactVfx);
            _vfx.transform.position = pos;
            Strip(_vfx);
            Selection.activeGameObject = _vfx;
            FrameScene();
        }

        // ── Projectile flight (the bola flying forward, muzzle flash at launch) — mirrors the game's ProjectileManager ──
        private void FireProjectileFly()
        {
            if (_rig == null) BuildRig();
            if (skill.projectileVfx == null) { ShowNotification(new GUIContent("Projétil VFX vazio.")); return; }
            Stop(); ClearVfx();
            _vfxUsesOrigin = false; _vfxFaceForward = false; // it moves on its own → sliders/handle don't reposition it
            Vector3 fwd = Forward();
            Vector3 pos = SkillOrigin.Resolve(_rig.transform, skill.spawnOffset);

            if (skill.muzzleVfx != null) // brief flash that stays at the muzzle while the bola flies
            {
                _muzzle = (GameObject)PrefabUtility.InstantiatePrefab(skill.muzzleVfx);
                _muzzle.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(fwd));
                Strip(_muzzle);
            }

            _vfx = (GameObject)PrefabUtility.InstantiatePrefab(skill.projectileVfx);
            _vfx.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(fwd) * Quaternion.Euler(skill.projectileVfxEuler));
            Strip(_vfx);
            _vfxPs = _vfx.GetComponentsInChildren<ParticleSystem>(true);

            _projStart = pos; _projDir = fwd; _projElapsed = 0f; _projSpeed = ProjectileSpeed();
            Selection.activeGameObject = _vfx;
            FrameScene();
            _lastTime = EditorApplication.timeSinceStartup;
            if (!_flying) { EditorApplication.update += FlyTick; _flying = true; }
        }

        // Speed = the skill's Projectile effect amount (m/s), same source the server uses; sane default if unset.
        private float ProjectileSpeed()
        {
            if (skill.effects != null)
                foreach (var e in skill.effects)
                    if (e.type == AbilityEffectType.Projectile) return Mathf.Max(1f, e.amount);
            return 12f;
        }

        private void FlyTick()
        {
            if (_vfx == null) { StopFlight(); return; }
            double now = EditorApplication.timeSinceStartup;
            float dt = (float)(now - _lastTime) * Mathf.Max(0.05f, speed); // reuse the slow-mo slider
            _lastTime = now;
            if (dt > 0.1f) dt = 0.1f;

            _projElapsed += dt;
            _vfx.transform.position += _projDir * (_projSpeed * dt);
            if (_vfxPs != null) foreach (var ps in _vfxPs) if (ps != null) ps.Simulate(dt, true, false); // advance the trail/particles

            float traveled = Vector3.Distance(_vfx.transform.position, _projStart);
            float maxDist = Mathf.Max(skill.range, 8f);
            SceneView.RepaintAll(); Repaint();

            if (traveled >= maxDist || _projElapsed > 5f)
            {
                if (!loop && skill.impactVfx != null) // show the hit where it lands (skipped on loop — re-fire would kill it)
                {
                    var imp = (GameObject)PrefabUtility.InstantiatePrefab(skill.impactVfx);
                    imp.transform.position = _vfx.transform.position;
                    Strip(imp);
                    if (_muzzle != null) DestroyImmediate(_muzzle);
                    _muzzle = imp; // reuse the slot so it's cleaned up next fire
                }
                if (loop) FireProjectileFly(); else StopFlight();
            }
        }

        private void StopFlight()
        {
            if (_flying) { EditorApplication.update -= FlyTick; _flying = false; }
        }

        // ── Animated swing ──
        private void PlaySwing()
        {
            if (_animator == null || skill == null) return;
            ClearVfx();
            _paused = false;
            string trig = string.IsNullOrEmpty(skill.animTrigger) ? attackTrigger : skill.animTrigger;
            _animator.Rebind();
            _animator.Update(0f);
            if (HasParam(trig)) _animator.SetTrigger(trig);
            _elapsed = 0; _fired = false;
            if (_trail != null) { _trail.Clear(); _trail.emitting = true; } // blade trail during the swing
            _lastTime = EditorApplication.timeSinceStartup;
            if (!_playing) { EditorApplication.update += Tick; _playing = true; }
        }

        private void Tick()
        {
            if (_animator == null) { Stop(); return; }
            double now = EditorApplication.timeSinceStartup;
            float dt = (float)(now - _lastTime) * Mathf.Max(0.05f, speed); // slow-mo
            _lastTime = now;
            if (dt > 0.1f) dt = 0.1f;
            if (_paused) dt = 0f; // frozen on a frame → tune the slash offset/rotation via the sliders

            if (!_paused)
            {
                _animator.Update(dt);
                _elapsed += dt;
                if (!_fired && _elapsed >= skill.releaseTime) { FireSlashSwing(); _fired = true; _fireAt = _elapsed; }
            }
            // Keep the slash particles frozen at their fire-relative time (stays visible while paused).
            if (_fired && _vfxPs != null)
                foreach (var ps in _vfxPs) if (ps != null) ps.Simulate((float)(_elapsed - _fireAt), true, true);

            SceneView.RepaintAll();
            Repaint();

            if (!_paused && _elapsed >= maxSeconds)
            {
                if (loop) { ClearVfx(); PlaySwing(); }   // auto-replay
                else Stop();
            }
        }

        private void FireSlashSwing()
        {
            if (skill.slashVfx == null) return;
            _vfxEulerKind = 1; _vfxFaceForward = true; _vfxUsesOrigin = true; // so pausing + dragging offset/rotation moves it
            Vector3 fwd = Forward();
            Vector3 pos = SkillOrigin.Resolve(_rig.transform, skill.spawnOffset);
            _vfx = (GameObject)PrefabUtility.InstantiatePrefab(skill.slashVfx);
            _vfx.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(fwd) * Quaternion.Euler(skill.slashVfxEuler));
            Strip(_vfx);
            _vfxPs = _vfx.GetComponentsInChildren<ParticleSystem>(true);
        }

        // ── helpers ──
        private Vector3 Forward()
        {
            Vector3 fwd = _rig.transform.forward; fwd.y = 0f;
            return fwd.sqrMagnitude > 1e-4f ? fwd.normalized : Vector3.forward;
        }

        private static void Strip(GameObject go)
        {
            foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true)) DestroyImmediate(mb);
            foreach (var col in go.GetComponentsInChildren<Collider>(true)) DestroyImmediate(col);
            foreach (var rb in go.GetComponentsInChildren<Rigidbody>(true)) DestroyImmediate(rb);
        }

        private bool HasParam(string name)
        {
            foreach (var p in _animator.parameters) if (p.name == name) return true;
            return false;
        }

        // Adds CombatAnimEvents to the model PREFAB on the Animator's GameObject (where animation events are received),
        // so the "Release" event works in-game too (not just the preview). Persists to the asset.
        private void AddEventsComponentToModel()
        {
            string path = AssetDatabase.GetAssetPath(modelPrefab);
            if (string.IsNullOrEmpty(path) || !path.EndsWith(".prefab"))
            {
                ShowNotification(new GUIContent("Precisa ser um PREFAB do modelo (não um FBX)."));
                return;
            }
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var anim = root.GetComponentInChildren<Animator>();
                var go = anim != null ? anim.gameObject : root;
                if (go.GetComponent<Arcane_Aegis.Entities.CombatAnimEvents>() == null)
                    go.AddComponent<Arcane_Aegis.Entities.CombatAnimEvents>();
                PrefabUtility.SaveAsPrefabAsset(root, path);
                ShowNotification(new GUIContent("CombatAnimEvents adicionado ao modelo. ✓"));
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        // Bake a "Release" Animation Event onto the attack clip at the skill's releaseTime, and turn on useAnimEvent —
        // so the slash fires frame-exact in-game. Handles both an imported FBX clip (via the ModelImporter) and a
        // standalone .anim. Removes any previous "Release" so re-baking just moves it.
        private void AddReleaseEvent()
        {
            if (attackClip == null || skill == null) return;
            float t = Mathf.Clamp(skill.releaseTime, 0f, attackClip.length);
            string path = AssetDatabase.GetAssetPath(attackClip);
            bool ok = false;

            if (AssetImporter.GetAtPath(path) is ModelImporter importer)
            {
                var clips = importer.clipAnimations;
                if (clips == null || clips.Length == 0) clips = importer.defaultClipAnimations;
                foreach (var c in clips)
                {
                    if (c.name != attackClip.name) continue;
                    var list = new System.Collections.Generic.List<AnimationEvent>(c.events ?? new AnimationEvent[0]);
                    list.RemoveAll(e => e.functionName == "Release");
                    list.Add(new AnimationEvent { time = t, functionName = "Release" });
                    c.events = list.ToArray();
                    ok = true;
                }
                if (ok) { importer.clipAnimations = clips; importer.SaveAndReimport(); }
            }
            else // standalone .anim
            {
                var list = new System.Collections.Generic.List<AnimationEvent>(AnimationUtility.GetAnimationEvents(attackClip));
                list.RemoveAll(e => e.functionName == "Release");
                list.Add(new AnimationEvent { time = t, functionName = "Release" });
                AnimationUtility.SetAnimationEvents(attackClip, list.ToArray());
                ok = true;
            }

            if (ok)
            {
                if (_so == null || _so.targetObject != skill) _so = new SerializedObject(skill);
                _so.Update(); _so.FindProperty("useAnimEvent").boolValue = true; _so.ApplyModifiedProperties();
                ShowNotification(new GUIContent($"'Release' @ {t:0.00}s gravado + Use Anim Event ligado."));
            }
            else ShowNotification(new GUIContent($"Clip '{attackClip.name}' não achado no FBX importer."));
        }

        private void Stop()
        {
            if (_playing) { EditorApplication.update -= Tick; _playing = false; }
            _paused = false;
            if (_trail != null) _trail.emitting = false;
        }
        private void ClearVfx()
        {
            StopFlight();
            if (_vfx != null) DestroyImmediate(_vfx); _vfx = null; _vfxPs = null;
            if (_muzzle != null) DestroyImmediate(_muzzle); _muzzle = null;
            Tools.hidden = false;
        }
        private void Cleanup()
        {
            Stop(); ClearVfx();
            if (_rig != null) DestroyImmediate(_rig); // the weapon is a child of the rig → destroyed with it
            _rig = null; _animator = null; _weapon = null; _trail = null;
        }

        private void FrameScene()
        {
            var sv = SceneView.lastActiveSceneView;
            if (sv != null && _rig != null) sv.LookAt(_rig.transform.position + Vector3.up, sv.rotation, 4f);
        }
    }
}
