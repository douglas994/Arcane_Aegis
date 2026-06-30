using UnityEditor;
using UnityEngine;
using UnityEditor.SceneManagement;
using Arcane_Aegis.Controllers;

namespace Arcane_Aegis.EditorTools
{
    /// <summary>Adds connection sockets (<see cref="BuildSnapPoint"/>) to building-piece prefabs/objects automatically, at
    /// the 6 face centers of the piece's bounds (±X / ±Z edges → tile floors + join walls side-to-side; +Y top / −Y bottom
    /// → stack walls + stand on floors). The arrows point OUTWARD (the connect direction). Re-run to regenerate. Works on
    /// a prefab selected in the Project, or an instance in the scene. Menu: ArcaneMMO ▸ Building ▸ Add Snap Sockets.</summary>
    public static class BuildSocketTool
    {
        [MenuItem("ArcaneMMO/Building/Add Snap Sockets to Selection")]
        public static void AddSockets()
        {
            var sel = Selection.gameObjects;
            if (sel == null || sel.Length == 0) { Debug.LogWarning("[Build] Selecione a(s) peça(s) — um prefab no Project ou um objeto na cena."); return; }
            int done = 0;
            foreach (var go in sel)
            {
                string path = AssetDatabase.GetAssetPath(go);
                bool isAsset = !string.IsNullOrEmpty(path) && path.EndsWith(".prefab");
                if (isAsset)
                {
                    GameObject root = PrefabUtility.LoadPrefabContents(path);
                    Generate(root);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    PrefabUtility.UnloadPrefabContents(root);
                }
                else
                {
                    Undo.RegisterFullObjectHierarchyUndo(go, "Add Snap Sockets");
                    Generate(go);
                    EditorUtility.SetDirty(go);
                    if (go.scene.IsValid()) EditorSceneManager.MarkSceneDirty(go.scene);
                }
                done++;
            }
            Debug.Log($"[Build] Sockets adicionados em {done} peça(s). Ajuste/remova manualmente se a peça não for uma caixa simples; mude o Kind se quiser separar chão/parede.");
        }

        private static void Generate(GameObject root)
        {
            // Clear previous auto sockets so a re-run doesn't pile up duplicates.
            var existing = root.GetComponentsInChildren<BuildSnapPoint>(true);
            for (int i = existing.Length - 1; i >= 0; i--)
                if (existing[i] != null && existing[i].gameObject != root) Object.DestroyImmediate(existing[i].gameObject);

            if (!TryLocalBounds(root, out Bounds b)) { Debug.LogWarning($"[Build] '{root.name}' não tem Renderer/Mesh — não dá pra medir as bordas."); return; }

            Vector3 c = b.center, e = b.extents;
            // Face centers: edges (±X/±Z) tile pieces side-to-side + align rotation; top/bottom stack.
            AddSocket(root, "Snap_+X", new Vector3(c.x + e.x, c.y, c.z), Vector3.right);
            AddSocket(root, "Snap_-X", new Vector3(c.x - e.x, c.y, c.z), Vector3.left);
            AddSocket(root, "Snap_+Z", new Vector3(c.x, c.y, c.z + e.z), Vector3.forward);
            AddSocket(root, "Snap_-Z", new Vector3(c.x, c.y, c.z - e.z), Vector3.back);
            AddSocket(root, "Snap_Top", new Vector3(c.x, c.y + e.y, c.z), Vector3.up);
            AddSocket(root, "Snap_Bottom", new Vector3(c.x, c.y - e.y, c.z), Vector3.down);
            // Footprint CORNERS (UP normal → position-only): two perpendicular walls snap quina-com-quina, so 90° corners
            // close cleanly. Mid-height so both walls match on the same floor level.
            AddSocket(root, "Snap_Corner_PP", new Vector3(c.x + e.x, c.y, c.z + e.z), Vector3.up);
            AddSocket(root, "Snap_Corner_PM", new Vector3(c.x + e.x, c.y, c.z - e.z), Vector3.up);
            AddSocket(root, "Snap_Corner_MP", new Vector3(c.x - e.x, c.y, c.z + e.z), Vector3.up);
            AddSocket(root, "Snap_Corner_MM", new Vector3(c.x - e.x, c.y, c.z - e.z), Vector3.up);
        }

        private static void AddSocket(GameObject root, string name, Vector3 localPos, Vector3 localNormal)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = localPos;
            Vector3 up = Mathf.Abs(localNormal.y) > 0.5f ? Vector3.forward : Vector3.up; // valid up for vertical normals
            go.transform.localRotation = Quaternion.LookRotation(localNormal, up);       // forward = outward connection direction
            go.AddComponent<BuildSnapPoint>(); // kind = Any by default
        }

        /// <summary>Combined renderer/mesh bounds expressed in the root's LOCAL space (so socket offsets are pose-independent).</summary>
        private static bool TryLocalBounds(GameObject root, out Bounds bounds)
        {
            bounds = default;
            bool any = false;
            Matrix4x4 toLocal = root.transform.worldToLocalMatrix;
            var filters = root.GetComponentsInChildren<MeshFilter>(true);
            foreach (var mf in filters)
            {
                if (mf == null || mf.sharedMesh == null) continue;
                Bounds mb = mf.sharedMesh.bounds; // local to that mesh
                Matrix4x4 m = toLocal * mf.transform.localToWorldMatrix;
                Encapsulate(ref bounds, ref any, m, mb);
            }
            if (any) return true;

            // Fallback: world-space renderer bounds → into local (approximate for rotated children).
            var rends = root.GetComponentsInChildren<Renderer>(true);
            foreach (var r in rends)
            {
                if (r == null) continue;
                Bounds wb = r.bounds;
                var localCenter = root.transform.InverseTransformPoint(wb.center);
                var lb = new Bounds(localCenter, root.transform.InverseTransformVector(wb.size));
                if (!any) { bounds = lb; any = true; } else bounds.Encapsulate(lb);
            }
            return any;
        }

        private static void Encapsulate(ref Bounds bounds, ref bool any, Matrix4x4 m, Bounds local)
        {
            Vector3 c = local.center, e = local.extents;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = c + new Vector3(
                    (i & 1) == 0 ? -e.x : e.x,
                    (i & 2) == 0 ? -e.y : e.y,
                    (i & 4) == 0 ? -e.z : e.z);
                Vector3 p = m.MultiplyPoint3x4(corner);
                if (!any) { bounds = new Bounds(p, Vector3.zero); any = true; } else bounds.Encapsulate(p);
            }
        }
    }
}
