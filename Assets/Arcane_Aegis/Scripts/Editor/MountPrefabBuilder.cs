using UnityEditor;
using UnityEngine;
using KinematicCharacterController;
using Arcane_Aegis.Controllers;
using Arcane_Aegis.Controllers.Locomotion;
using Arcane_Aegis.Entities;

namespace Arcane_Aegis.EditorTools
{
    /// <summary>
    /// One-click generator for a MOUNT rig prefab SKELETON — wires every component the runtime expects so you only have
    /// to drop the model in + nudge the seat/camera/collider. Menu: ArcaneMMO ▸ Mounts ▸ Create Mount Prefab Skeleton.
    /// Builds: root (KinematicCharacterMotor disabled + CapsuleCollider + MountController + MovementSender + MountView)
    /// with children "RiderSeat" (sit pose), "Target" (camera), "Model" (drop the 3D model here). Saves to
    /// Assets/Arcane_Aegis/Prefabs/Mounts and selects it.
    /// </summary>
    public static class MountPrefabBuilder
    {
        private const string Dir = "Assets/Arcane_Aegis/Prefabs/Mounts";

        [MenuItem("ArcaneMMO/Mounts/Create Mount Prefab Skeleton")]
        public static void CreateSkeleton()
        {
            var root = new GameObject("Mount_New");
            try
            {
                // MountController pulls in KinematicCharacterMotor (which pulls in a CapsuleCollider) via RequireComponent.
                var mc = root.AddComponent<MountController>();
                var kcm = root.GetComponent<KinematicCharacterMotor>();
                if (kcm != null) kcm.enabled = false; // the runtime enables it for the local rider; disabled avoids a null-controller NRE
                var sender = root.AddComponent<MovementSender>();
                root.AddComponent<MountView>();

                // Children: seat (where the player sits), camera target, and a holder for the visual model.
                Transform seat = NewChild(root.transform, "RiderSeat", new Vector3(0f, 1.2f, 0f));
                Transform target = NewChild(root.transform, "Target", new Vector3(0f, 1.6f, -0.2f));
                NewChild(root.transform, "Model", Vector3.zero); // ← drop the 3D model under here

                // Wire the controller's refs (public) + the sender's mount source (private serialized).
                mc.riderSeat = seat;
                mc.cameraTarget = target;
                var so = new SerializedObject(sender);
                var mountProp = so.FindProperty("mount");
                if (mountProp != null) { mountProp.objectReferenceValue = mc; so.ApplyModifiedProperties(); }

                EnsureFolder(Dir);
                string path = AssetDatabase.GenerateUniqueAssetPath(Dir + "/Mount_New.prefab");
                var prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(root, path, InteractionMode.UserAction);

                Selection.activeObject = prefab;
                EditorGUIUtility.PingObject(prefab);
                Debug.Log($"[Mounts] Esqueleto criado em {path}. Próximos passos: solte o modelo 3D dentro de 'Model', ajuste 'RiderSeat'/'Target' e o CapsuleCollider, marque 'canFly' no MountController se for voadora, e arraste o prefab no MountDefinitionSO ▸ mountPrefab.");
            }
            finally
            {
                Object.DestroyImmediate(root); // the scene temp is now saved as a prefab asset
            }
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
            if (AssetDatabase.IsValidFolder(dir)) return;
            string parent = System.IO.Path.GetDirectoryName(dir).Replace('\\', '/');
            string leaf = System.IO.Path.GetFileName(dir);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
