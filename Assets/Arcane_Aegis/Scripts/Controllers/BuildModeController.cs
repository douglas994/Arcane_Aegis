using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using ArcaneShared.Enums;
using Arcane_Aegis.Content;
using Arcane_Aegis.Entities;
using Arcane_Aegis.Items;
using Arcane_Aegis.Network;
using Arcane_Aegis.UI;

namespace Arcane_Aegis.Controllers
{
    /// <summary>Open-world building. Toggle build mode (B): a GHOST of the selected piece follows the MOUSE CURSOR on the
    /// ground; rotate it (scroll / Q-E), raise-lower it (R/F), and place it (left-click) — the server validates
    /// cost/cap/spot and spawns the real structure. Snaps to nearby pieces' sockets (hold Ctrl to free-place). The ghost
    /// is GREEN when you can afford it, RED when not. Demolish what you're near (X). While building, COMBAT is suppressed
    /// (left-click builds, doesn't attack). Drop this on ONE scene object; the BuildMenuUI calls <see cref="SelectPiece"/>.</summary>
    public sealed class BuildModeController : MonoBehaviour
    {
        /// <summary>True while build mode is open — PlayerCombat reads this to suppress casting (so clicking places, not attacks).</summary>
        public static bool IsBuilding { get; private set; }

        [Header("Refs")]
        [SerializeField] private ContentLibrary library;     // the building-piece catalog (falls back to ContentLibrary.Active)
        [SerializeField] private LayerMask groundMask = ~0;  // what the ghost snaps onto (set to your Terrain/Ground layer)
        [SerializeField] private GameObject menuPanel;       // optional: the BuildMenuUI panel — shown/hidden with build mode

        [Header("Placement")]
        [Tooltip("Mira com o CURSOR do mouse (raycast da câmera no chão). Desligue pra colocar à frente do player.")]
        [SerializeField] private bool aimWithCursor = true;
        [SerializeField] private float placeDistance = 3.5f; // fallback (no cursor aim): how far in front of the player
        [SerializeField] private float rotateStep = 15f;     // degrees per scroll notch / Q-E tap
        [SerializeField] private float heightSpeed = 2f;     // m/s raise-lower (R/F) for stacking
        [SerializeField] private float demolishRange = 4f;   // how close to demolish a structure

        [Header("Snapping (connect walls/floors)")]
        [Tooltip("Encaixe por sockets (BuildSnapPoint nas peças). Segure a tecla de desativar pra colocar livre.")]
        [SerializeField] private bool snapEnabled = true;
        [Tooltip("Distância máx. entre sockets pra encaixar de LADO (bordas/paredes em fila). Maior = gruda mais fácil.")]
        [SerializeField] private float snapRadius = 1.2f;
        [Tooltip("Alcance do encaixe VERTICAL (empilhar / topo-base) — mais folgado, pra não precisar mirar exato no topo.")]
        [SerializeField] private float stackReach = 2.2f;
        [Tooltip("Raio de busca por peças vizinhas pra encaixar (≈ tamanho da peça + folga).")]
        [SerializeField] private float snapSearchRadius = 6f;
        [Tooltip("Quantiza o ÂNGULO da peça em passos (90 = só cardeais → paredes sempre retas, cantos fecham). 0 = livre. Segure Ctrl pra ignorar.")]
        [SerializeField] private float yawSnap = 90f;
        [Tooltip("Quando NÃO há peça vizinha pra grudar, encaixa a POSIÇÃO numa GRADE — assim o mouse não precisa ser preciso e tudo fica alinhado. Ligado por padrão.")]
        [SerializeField] private bool gridSnap = true;
        [Tooltip("Tamanho da grade. AJUSTE pro tamanho do seu CHÃO (ex: chão 4×4 → ponha 4). Aí chão e parede caem todos na mesma malha e alinham. 0 = sem grade.")]
        [SerializeField] private float gridSize = 2f;

        [Header("Input")]
        [SerializeField] private Key toggleKey = Key.B;
        [SerializeField] private Key rotateLeftKey = Key.Q;
        [SerializeField] private Key rotateRightKey = Key.E;
        [SerializeField] private Key raiseKey = Key.R;
        [SerializeField] private Key lowerKey = Key.F;
        [SerializeField] private Key demolishKey = Key.X;
        [SerializeField] private Key cancelKey = Key.Escape;
        [Tooltip("Segure pra desativar o encaixe e colocar livre.")]
        [SerializeField] private Key disableSnapKey = Key.LeftCtrl;

        private EntityManager _entities;
        private Camera _cam;
        private BuildingDefinitionSO _selected;
        private GameObject _ghost;
        private float _yaw;
        private float _heightOffset;
        private bool _active;

        // Cached connection points of the current ghost (local to its root) + reused scan buffers (zero per-frame alloc).
        private struct GhostSocket { public Vector3 localPos; public Vector3 localNormal; public bool vertical; public BuildSnapPoint.Kind kind; }
        private GhostSocket[] _ghostSockets = System.Array.Empty<GhostSocket>();
        private float _baseOffset;     // distance from the piece's pivot down to its bottom (so it sits ON the ground, not sunk)
        private float _pieceExtentXZ;  // half the larger horizontal size → side-snap reach scales with the piece (long walls grab from afar)
        private float _pieceHeight;    // full height → stacking reach scales with the piece
        private Vector3 _pieceCenterLocal; // pivot→bounds-center XZ offset → grid snaps the VISIBLE center (pivot-independent)
        private readonly System.Collections.Generic.List<EntityView> _nearbyBuildings = new();
        private readonly System.Collections.Generic.List<BuildSnapPoint> _worldSockets = new();
        private Renderer[] _ghostRenderers = System.Array.Empty<Renderer>();
        private int _lastValid = -1; // -1 unknown, 0 red, 1 green (avoid re-tinting every frame)
        private static readonly RaycastHit[] _aimHits = new RaycastHit[16]; // reused cursor-raycast buffer (no per-frame alloc)

        public bool Active => _active;

        private ContentLibrary Lib => library != null ? library : ContentLibrary.Active;

        private void Awake() => _entities = FindAnyObjectByType<EntityManager>();

        /// <summary>Picks the piece to place (called by the hand-built build menu). Opens build mode if needed.</summary>
        public void SelectPiece(BuildingDefinitionSO def)
        {
            _selected = def;
            if (!_active) SetBuildMode(true);
            RebuildGhost();
        }

        /// <summary>Opens/closes build mode (wire to a HUD button if you like).</summary>
        public void SetBuildMode(bool on)
        {
            _active = on;
            IsBuilding = on; // PlayerCombat suppresses casting while this is true
            if (menuPanel != null) menuPanel.SetActive(on);
            if (!on) DestroyGhost();
            else { _heightOffset = 0f; if (_selected == null) AutoSelectFirst(); }
        }

        private void Update()
        {
            if (_entities == null) { _entities = FindAnyObjectByType<EntityManager>(); }
            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb[toggleKey].wasPressedThisFrame && !UiFocus.IsTyping) SetBuildMode(!_active);

            if (!_active) return;
            if (kb[cancelKey].wasPressedThisFrame) { SetBuildMode(false); return; }

            var local = _entities != null ? _entities.Local : null;
            if (local == null) return;

            // Demolish the nearest structure within range (no piece needed).
            if (kb[demolishKey].wasPressedThisFrame && !UiFocus.IsTyping)
            {
                var b = _entities.NearestBuilding(local.transform.position, demolishRange);
                if (b != null && NetClient.Instance != null) NetClient.Instance.SendRemoveBuilding(b.Id);
                return;
            }

            // Rotate (Q/E + scroll) and raise/lower (R/F, for stacking). When angle-snap is on, each tap is a clean step.
            float step = (snapEnabled && yawSnap > 0f && !kb[disableSnapKey].isPressed) ? yawSnap : rotateStep;
            if (kb[rotateLeftKey].wasPressedThisFrame) _yaw -= step;
            if (kb[rotateRightKey].wasPressedThisFrame) _yaw += step;
            float scroll = Mouse.current != null ? Mouse.current.scroll.ReadValue().y : 0f;
            if (Mathf.Abs(scroll) > 0.01f) _yaw += Mathf.Sign(scroll) * step;
            if (kb[raiseKey].isPressed) _heightOffset += heightSpeed * Time.deltaTime;
            if (kb[lowerKey].isPressed) _heightOffset -= heightSpeed * Time.deltaTime;

            if (_selected == null || _selected.prefab == null) { DestroyGhost(); return; }

            // Ghost follows the mouse cursor on the ground (camera raycast), + a manual height offset for stacking.
            Vector3 target = AimGroundPoint(local);
            target.y += _heightOffset;

            // Snap the ghost to a nearby piece's socket (or a grid), unless the disable key is held → free placement.
            float yaw = _yaw;
            if (snapEnabled && !kb[disableSnapKey].isPressed)
            {
                if (yawSnap > 0f) yaw = Mathf.Round(yaw / yawSnap) * yawSnap;                          // lock rotation to 90° FIRST…
                if (!TrySnapToSockets(ref target, yaw) && gridSnap) SnapPositionToGrid(ref target, yaw); // …then pull position flush at that final rotation
            }

            if (_ghost == null) RebuildGhost();
            if (_ghost != null) _ghost.transform.SetPositionAndRotation(target, Quaternion.Euler(0f, yaw, 0f));

            bool affordable = CanAfford(_selected);
            TintGhost(affordable); // green = can build, red = missing materials (server still validates cap/spot)

            // Place (left-click) — not while typing and not when clicking the build menu / UI.
            bool place = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
            if (place && !UiFocus.IsTyping && !IsPointerOverUI() && NetClient.Instance != null && _entities != null)
            {
                Vector3 serverLocal = _entities.ToServer(target);
                NetClient.Instance.SendPlaceBuilding(_selected.id, serverLocal, yaw);
            }
        }

        /// <summary>The build point: where the mouse cursor hits a SURFACE (terrain OR a placed piece — so you can aim at a
        /// wall's TOP to stack), or—if cursor-aim is off / nothing hit—a point in front of the player on the ground. The
        /// piece's BOTTOM is seated on the surface (<see cref="_baseOffset"/>). Hits on the player itself are skipped.</summary>
        private Vector3 AimGroundPoint(EntityView local)
        {
            if (aimWithCursor)
            {
                var cam = Cam();
                var mouse = Mouse.current;
                if (cam != null && mouse != null)
                {
                    Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());
                    // Hit ANY surface (not just the ground layer) so aiming at a wall top stacks onto it; skip the player.
                    int n = Physics.RaycastNonAlloc(ray, _aimHits, 300f, ~0, QueryTriggerInteraction.Ignore);
                    Transform selfRoot = local.transform.root;
                    float bestD = float.MaxValue; bool got = false; Vector3 pt = default;
                    for (int i = 0; i < n; i++)
                    {
                        var t = _aimHits[i].transform;
                        if (t == null || t.root == selfRoot) continue; // ignore the player's own colliders
                        if (_aimHits[i].distance < bestD) { bestD = _aimHits[i].distance; pt = _aimHits[i].point; got = true; }
                    }
                    if (got) return new Vector3(pt.x, pt.y + _baseOffset, pt.z); // seat the piece's bottom on that surface
                }
            }
            Vector3 flatFwd = local.transform.forward; flatFwd.y = 0f;
            if (flatFwd.sqrMagnitude < 0.001f) flatFwd = Vector3.forward;
            flatFwd.Normalize();
            Vector3 p = local.transform.position + flatFwd * placeDistance;
            if (Physics.Raycast(p + Vector3.up * 50f, Vector3.down, out var h, 100f, groundMask, QueryTriggerInteraction.Ignore)) p.y = h.point.y;
            else p.y = local.transform.position.y;
            p.y += _baseOffset; // sit the piece's bottom on the ground
            return p;
        }

        private Camera Cam()
        {
            if (_cam == null)
            {
                var mmo = FindAnyObjectByType<MMO.Scripts.Controllers.MMOCamera>();
                _cam = mmo != null && mmo.Camera != null ? mmo.Camera : Camera.main;
            }
            return _cam;
        }

        private static bool IsPointerOverUI()
            => EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        /// <summary>POSITION-ONLY snap: moves the ghost so one of its sockets lands exactly on the nearest compatible socket
        /// of a placed piece — pulling the piece flush. Rotation is NOT touched here (the caller already locked it to 90°),
        /// so the socket lands exactly where the piece will actually be → no leftover step/offset. Picks the closest pair.</summary>
        private bool TrySnapToSockets(ref Vector3 pos, float yaw)
        {
            if (_ghostSockets.Length == 0 || _entities == null) return false;
            _entities.CollectBuildings(_nearbyBuildings, pos, snapSearchRadius);
            if (_nearbyBuildings.Count == 0) return false;

            Quaternion rot = Quaternion.Euler(0f, yaw, 0f); // the piece's FINAL rotation (already quantized by the caller)
            float bestScore = float.MaxValue;
            bool found = false;
            Vector3 bestPos = pos;

            for (int b = 0; b < _nearbyBuildings.Count; b++)
            {
                var view = _nearbyBuildings[b];
                if (view == null) continue;
                view.GetComponentsInChildren(_worldSockets); // reused buffer (no alloc)
                for (int wi = 0; wi < _worldSockets.Count; wi++)
                {
                    var w = _worldSockets[wi];
                    Vector3 wPos = w.transform.position;
                    Vector3 wNormal = w.transform.forward;
                    bool wVertical = Mathf.Abs(wNormal.y) > 0.7f; // top/bottom socket (stacking / wall-on-floor)

                    for (int gi = 0; gi < _ghostSockets.Length; gi++)
                    {
                        var g = _ghostSockets[gi];
                        if (!BuildSnapPoint.Compatible(g.kind, w.kind)) continue;
                        // Reach scales with the piece so it grabs MAGNETICALLY when near (no fine adjusting): a long wall
                        // connects end-to-end from ~its own length away; stacking reaches ~a piece-height up.
                        float reach = (g.vertical || wVertical)
                            ? Mathf.Max(stackReach, _pieceHeight)
                            : Mathf.Max(snapRadius, _pieceExtentXZ);
                        Vector3 gWorldNow = pos + rot * g.localPos;             // this ghost socket at the final rotation
                        float d = Vector3.Distance(gWorldNow, wPos);
                        if (d > reach) continue;                                  // out of this pair's reach
                        // PREFER a true FACE-JOIN: the two sockets' arrows point INTO each other (edge↔edge in a row,
                        // top↔bottom stacking) → faces sit flush. Mismatched/corner pairs (arrows not opposing) get a big
                        // penalty so they only win when there's no clean join — this kills the "emenda desencontrada".
                        float align = Vector3.Dot(rot * g.localNormal, wNormal); // -1 = perfectly opposing (clean join)
                        float score = d + (align < -0.3f ? 0f : 100f);
                        if (score >= bestScore) continue;
                        bestScore = score; found = true;
                        bestPos = wPos - rot * g.localPos;                       // move the piece so its socket lands ON wPos
                    }
                }
            }

            if (found) pos = bestPos;
            return found;
        }

        /// <summary>Rounds the piece's VISIBLE CENTER (pivot-independent) to the build grid, so the mouse needn't be precise
        /// and pieces with different pivots still align. Y (height) is left free for stacking/raising.</summary>
        private void SnapPositionToGrid(ref Vector3 pos, float yaw)
        {
            if (gridSize <= 0.01f) return;
            Quaternion rot = Quaternion.Euler(0f, yaw, 0f);
            Vector3 center = pos + rot * _pieceCenterLocal;       // the piece's real center in the world
            center.x = Mathf.Round(center.x / gridSize) * gridSize;
            center.z = Mathf.Round(center.z / gridSize) * gridSize;
            Vector3 snapped = center - rot * _pieceCenterLocal;   // back to the pivot
            pos.x = snapped.x; pos.z = snapped.z;
        }

        private void AutoSelectFirst()
        {
            var lib = Lib;
            if (lib != null && lib.buildingDefs != null)
                foreach (var d in lib.buildingDefs)
                    if (d != null && d.prefab != null) { _selected = d; break; }
            RebuildGhost();
        }

        private void RebuildGhost()
        {
            DestroyGhost();
            if (!_active || _selected == null || _selected.prefab == null) return;
            _ghost = Instantiate(_selected.prefab);
            _ghost.name = "BuildGhost";
            // Visual-only: strip colliders/rigidbodies so the ghost doesn't collide or block raycasts.
            foreach (var c in _ghost.GetComponentsInChildren<Collider>()) Destroy(c);
            foreach (var rb in _ghost.GetComponentsInChildren<Rigidbody>()) Destroy(rb);
            CacheGhostSockets();
            _ghostRenderers = _ghost.GetComponentsInChildren<Renderer>();
            ComputeGhostBounds();
            _heightOffset = 0f; // a fresh piece starts grounded
            _lastValid = -1; // force a re-tint
        }

        /// <summary>From the combined renderer bounds: the base offset (pivot→bottom, so the piece sits flush on a surface)
        /// and the piece's horizontal/vertical size (so the snap reach scales with the piece — a long wall grabs from afar).</summary>
        private void ComputeGhostBounds()
        {
            _baseOffset = 0f; _pieceExtentXZ = 0f; _pieceHeight = 0f;
            if (_ghostRenderers == null || _ghostRenderers.Length == 0) return;
            bool any = false;
            Bounds b = default;
            for (int i = 0; i < _ghostRenderers.Length; i++)
            {
                var r = _ghostRenderers[i];
                if (r == null) continue;
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            if (!any) return;
            Vector3 pivot = _ghost.transform.position;
            _baseOffset = pivot.y - b.min.y; // pivot.y − bottom.y
            _pieceExtentXZ = Mathf.Max(b.extents.x, b.extents.z); // half the larger footprint side
            _pieceHeight = b.size.y;
            _pieceCenterLocal = new Vector3(b.center.x - pivot.x, 0f, b.center.z - pivot.z); // pivot→center (identity pose)
        }

        /// <summary>Records the ghost's BuildSnapPoints as offsets local to its root (constant regardless of where the
        /// ghost is moved), so snapping can solve placement each frame without re-reading the transforms.</summary>
        private void CacheGhostSockets()
        {
            var pts = _ghost.GetComponentsInChildren<BuildSnapPoint>();
            _ghostSockets = pts.Length == 0 ? System.Array.Empty<GhostSocket>() : new GhostSocket[pts.Length];
            var root = _ghost.transform;
            Quaternion inv = Quaternion.Inverse(root.rotation);
            for (int i = 0; i < pts.Length; i++)
            {
                Vector3 fwd = (inv * pts[i].transform.forward).normalized; // socket's connection direction, in piece-local space
                _ghostSockets[i] = new GhostSocket
                {
                    localPos = inv * (pts[i].transform.position - root.position),
                    localNormal = fwd,
                    vertical = Mathf.Abs(fwd.y) > 0.7f, // top/bottom socket → stacking reach
                    kind = pts[i].kind,
                };
            }
        }

        private void DestroyGhost()
        {
            if (_ghost != null) Destroy(_ghost);
            _ghost = null;
            _ghostSockets = System.Array.Empty<GhostSocket>();
            _ghostRenderers = System.Array.Empty<Renderer>();
            _lastValid = -1;
        }

        /// <summary>Client-side affordability hint (the server still validates cost/cap/spot): do I have the materials?</summary>
        private static bool CanAfford(BuildingDefinitionSO def)
        {
            var store = InventoryStore.Instance;
            if (store == null || def == null || def.ingredients == null) return true;
            foreach (var ing in def.ingredients)
            {
                if (ing.item == null) continue;
                int have = 0;
                var items = store.Items;
                for (int i = 0; i < items.Count; i++)
                    if (items[i].Container == ItemContainer.Bag && items[i].TemplateId == ing.item.id) have += items[i].Quantity;
                if (have < ing.qty) return false;
            }
            return true;
        }

        /// <summary>Tints the whole ghost translucent GREEN (can build) or RED (can't), only when the state flips. Sets the
        /// base color directly — fine for a preview. If a material lacks a color property it just stays as-is.</summary>
        private void TintGhost(bool ok)
        {
            int want = ok ? 1 : 0;
            if (want == _lastValid) return;
            _lastValid = want;
            Color tint = ok ? new Color(0.45f, 1f, 0.5f, 0.5f) : new Color(1f, 0.4f, 0.4f, 0.5f);
            for (int r = 0; r < _ghostRenderers.Length; r++)
            {
                var rend = _ghostRenderers[r];
                if (rend == null) continue;
                var mats = rend.materials;
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    if (m == null) continue;
                    if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", tint);
                    else if (m.HasProperty("_Color")) m.color = tint;
                }
            }
        }

        private void OnDisable() { DestroyGhost(); IsBuilding = false; }
    }
}
