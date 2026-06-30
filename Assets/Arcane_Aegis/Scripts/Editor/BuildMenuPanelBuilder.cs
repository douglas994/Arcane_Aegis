using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Arcane_Aegis.Content;
using Arcane_Aegis.Controllers;
using Arcane_Aegis.UI;

namespace Arcane_Aegis.EditorTools
{
    /// <summary>
    /// Builds the open-world building UI into the open scene (real, editable objects) + a shared BuildRow prefab, and
    /// wires the refs: • BuildMenu (lists the building pieces + cost; click → start placing) • a BuildSystem object with
    /// the BuildModeController (the ghost/place/demolish engine), with the menu wired so it shows/hides with build mode
    /// (key B). Finds the first ContentLibrary and assigns it. Restyle by hand after.
    /// Menu: ArcaneMMO ▸ UI ▸ Create Build Menu.
    /// </summary>
    public static class BuildMenuPanelBuilder
    {
        private const string RowPrefabPath = "Assets/Arcane_Aegis/Prefabs/UI/BuildRow.prefab";

        [MenuItem("ArcaneMMO/UI/Create Build Menu")]
        public static void Create()
        {
            Canvas canvas = FindOrCreateCanvas();
            EnsureEventSystem();
            var library = FindLibrary();
            GameObject rowPrefab = BuildRowPrefab();

            // ── BuildMenu panel — left side. Starts INACTIVE: the BuildModeController shows it in build mode (B). ──
            var panel = NewUI("BuildMenu", canvas.transform);
            var prt = panel.GetComponent<RectTransform>();
            prt.anchorMin = prt.anchorMax = prt.pivot = new Vector2(0, 0.5f);
            prt.sizeDelta = new Vector2(360, 560);
            prt.anchoredPosition = new Vector2(20, 0);
            panel.AddComponent<Image>().color = new Color(0.05f, 0.06f, 0.09f, 0.95f);
            var title = CreateText("Title", panel.transform, "Construção (B)", 22, TextAlignmentOptions.Center, FontStyles.Bold);
            Anchor(title.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -14), new Vector2(-32, 36), 36);
            var hint = CreateText("Hint", panel.transform, "Q/E gira · roda do mouse aproxima · clique coloca · X demole", 12, TextAlignmentOptions.Center, FontStyles.Italic);
            Anchor(hint.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -52), new Vector2(-24, 28), 28);
            hint.enableWordWrapping = true;
            var list = BuildList("List", panel.transform, topOffset: -84);

            var menu = panel.AddComponent<BuildMenuUI>();

            // ── BuildSystem object with the engine (find an existing one or make it). ──
            var controller = UnityEngine.Object.FindFirstObjectByType<BuildModeController>();
            if (controller == null)
            {
                var sys = new GameObject("BuildSystem");
                controller = sys.AddComponent<BuildModeController>();
                Undo.RegisterCreatedObjectUndo(sys, "Create Build Menu");
            }

            // Wire the controller (library + the menu panel it toggles with build mode).
            var co = new SerializedObject(controller);
            co.FindProperty("library").objectReferenceValue = library;
            co.FindProperty("menuPanel").objectReferenceValue = panel;
            co.ApplyModifiedPropertiesWithoutUndo();

            // Wire the menu (library + the controller it drives + the row prefab + list parent).
            var mo = new SerializedObject(menu);
            mo.FindProperty("library").objectReferenceValue = library;
            mo.FindProperty("builder").objectReferenceValue = controller;
            mo.FindProperty("rowPrefab").objectReferenceValue = rowPrefab;
            mo.FindProperty("listParent").objectReferenceValue = list.transform;
            mo.ApplyModifiedPropertiesWithoutUndo();

            panel.SetActive(false); // hidden until build mode opens it

            Undo.RegisterCreatedObjectUndo(panel, "Create Build Menu");
            Selection.activeGameObject = panel;
            EditorSceneManager.MarkSceneDirty(panel.scene);
            if (library == null) Debug.LogWarning("[UI] Nenhuma ContentLibrary encontrada — arraste-a no campo 'library' do BuildMenu e do BuildModeController.");
            Debug.Log("[UI] BuildMenu + BuildModeController criados (+ BuildRow em " + RowPrefabPath + "). Ajuste o 'Ground Mask' do BuildModeController pra layer do terreno. Aperte B no jogo pra abrir.");
        }

        // A vertical list that grows with its rows (VerticalLayoutGroup + ContentSizeFitter), anchored under the header.
        private static GameObject BuildList(string name, Transform parent, float topOffset)
        {
            var go = NewUI(name, parent);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(0.5f, 1);
            rt.anchoredPosition = new Vector2(0, topOffset);
            rt.offsetMin = new Vector2(12, rt.offsetMin.y); rt.offsetMax = new Vector2(-12, topOffset);
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, 0);
            var vlg = go.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 8; vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true; vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
            var fit = go.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return go;
        }

        // One building-piece row (CraftRow): an icon, the piece name, the "have/need" cost line, and a "Construir" button.
        private static GameObject BuildRowPrefab()
        {
            var go = new GameObject("BuildRow", typeof(RectTransform), typeof(Image));
            go.GetComponent<Image>().color = new Color(0.12f, 0.14f, 0.20f, 0.95f);
            var vlg = go.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 4; vlg.padding = new RectOffset(10, 10, 8, 8);
            vlg.childControlWidth = true; vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
            var fit = go.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Header: icon + name (horizontal).
            var header = NewUI("Header", go.transform);
            var hlg = header.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 8; hlg.childControlWidth = true; hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false; hlg.childAlignment = TextAnchor.MiddleLeft;
            AddLayoutMinHeight(header, 36);

            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            iconGo.transform.SetParent(header.transform, false);
            var iconLe = iconGo.GetComponent<LayoutElement>(); iconLe.minWidth = iconLe.preferredWidth = 36; iconLe.minHeight = iconLe.preferredHeight = 36;
            var icon = iconGo.GetComponent<Image>(); icon.preserveAspect = true; icon.enabled = false;

            var name = CreateText("Name", header.transform, "Peça", 18, TextAlignmentOptions.Left, FontStyles.Bold);
            var nameLe = name.gameObject.AddComponent<LayoutElement>(); nameLe.flexibleWidth = 1;

            var status = CreateText("Status", go.transform, "Madeira 0/3", 13, TextAlignmentOptions.Left, FontStyles.Normal);
            status.enableWordWrapping = true;

            var btnGo = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            btnGo.transform.SetParent(go.transform, false);
            btnGo.GetComponent<Image>().color = new Color(0.55f, 0.35f, 0.18f, 1f); // earthy brown (build)
            var le = btnGo.GetComponent<LayoutElement>(); le.minHeight = 30; le.preferredHeight = 30;
            var label = CreateText("Label", btnGo.transform, "Construir", 15, TextAlignmentOptions.Center, FontStyles.Normal);
            Stretch(label.rectTransform);

            var row = go.AddComponent<CraftRow>();
            var so = new SerializedObject(row);
            so.FindProperty("icon").objectReferenceValue = icon;
            so.FindProperty("nameLabel").objectReferenceValue = name;
            so.FindProperty("ingredientsLabel").objectReferenceValue = status;
            so.FindProperty("craftButton").objectReferenceValue = btnGo.GetComponent<Button>();
            so.ApplyModifiedPropertiesWithoutUndo();

            EnsureFolder(Path.GetDirectoryName(RowPrefabPath).Replace('\\', '/'));
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, RowPrefabPath);
            UnityEngine.Object.DestroyImmediate(go);
            return prefab;
        }

        // ── helpers (mirrors QuestPanelBuilder) ──
        private static ContentLibrary FindLibrary()
        {
            if (ContentLibrary.Active != null) return ContentLibrary.Active;
            var guids = AssetDatabase.FindAssets("t:ContentLibrary");
            return guids.Length > 0 ? AssetDatabase.LoadAssetAtPath<ContentLibrary>(AssetDatabase.GUIDToAssetPath(guids[0])) : null;
        }

        private static void Anchor(RectTransform rt, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size, float height)
        {
            rt.anchorMin = aMin; rt.anchorMax = aMax; rt.pivot = pivot;
            rt.anchoredPosition = pos; rt.sizeDelta = new Vector2(size.x, height);
            rt.offsetMin = new Vector2(16, rt.offsetMin.y); rt.offsetMax = new Vector2(-16, rt.offsetMax.y);
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        private static void AddLayoutMinHeight(GameObject go, float h)
        {
            var le = go.AddComponent<LayoutElement>(); le.minHeight = h; le.preferredHeight = h;
        }

        private static Canvas FindOrCreateCanvas()
        {
            var existing = UnityEngine.Object.FindFirstObjectByType<Canvas>();
            if (existing != null) return existing;
            var go = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            Undo.RegisterCreatedObjectUndo(go, "Create Canvas");
            return canvas;
        }

        private static void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem));
            var moduleType = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (moduleType != null) go.AddComponent(moduleType);
            else go.AddComponent<StandaloneInputModule>();
            Undo.RegisterCreatedObjectUndo(go, "Create EventSystem");
        }

        private static GameObject NewUI(string n, Transform parent)
        {
            var go = new GameObject(n, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            return go;
        }

        private static TMP_Text CreateText(string n, Transform parent, string content, float size, TextAlignmentOptions align, FontStyles style)
        {
            var go = NewUI(n, parent);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = content; t.fontSize = size; t.alignment = align; t.fontStyle = style; t.color = Color.white;
            return t;
        }

        private static void EnsureFolder(string dir)
        {
            if (string.IsNullOrEmpty(dir) || AssetDatabase.IsValidFolder(dir)) return;
            string parent = Path.GetDirectoryName(dir).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(dir));
        }
    }
}
