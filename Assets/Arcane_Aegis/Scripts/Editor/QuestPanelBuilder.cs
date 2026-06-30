using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Arcane_Aegis.Content;
using Arcane_Aegis.UI;

namespace Arcane_Aegis.EditorTools
{
    /// <summary>
    /// Builds the quest UI into the open scene (real, editable objects) + a shared QuestRow prefab, and wires the refs:
    /// • QuestPanel (QuestGiver: offer/turn-in) — opened by the NpcController. • QuestLogPanel (HUD, toggle J).
    /// Finds the first ContentLibrary and assigns it. Restyle by hand after. Menu: ArcaneMMO ▸ UI ▸ Create Quest Panels.
    /// </summary>
    public static class QuestPanelBuilder
    {
        private const string RowPrefabPath = "Assets/Arcane_Aegis/Prefabs/UI/QuestRow.prefab";

        [MenuItem("ArcaneMMO/UI/Create Quest Panels")]
        public static void Create()
        {
            Canvas canvas = FindOrCreateCanvas();
            EnsureEventSystem();
            var library = FindLibrary();
            GameObject rowPrefab = BuildQuestRowPrefab();

            // ── QuestPanel (giver: offer / turn-in) — centre-right ──
            var giver = NewUI("QuestPanel", canvas.transform);
            var grt = giver.GetComponent<RectTransform>();
            grt.anchorMin = grt.anchorMax = grt.pivot = new Vector2(0.5f, 0.5f);
            grt.sizeDelta = new Vector2(520, 560);
            grt.anchoredPosition = Vector2.zero;
            giver.AddComponent<Image>().color = new Color(0.05f, 0.06f, 0.09f, 0.95f);
            var gTitle = CreateText("Title", giver.transform, "Missões", 24, TextAlignmentOptions.Center, FontStyles.Bold);
            Anchor(gTitle.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -16), new Vector2(-32, 40), 32);
            var gList = BuildList("List", giver.transform, topOffset: -64);

            var qp = giver.AddComponent<QuestPanel>();
            var qpo = new SerializedObject(qp);
            qpo.FindProperty("library").objectReferenceValue = library;
            qpo.FindProperty("panel").objectReferenceValue = giver;
            qpo.FindProperty("titleLabel").objectReferenceValue = gTitle;
            qpo.FindProperty("rowPrefab").objectReferenceValue = rowPrefab;
            qpo.FindProperty("listParent").objectReferenceValue = gList.transform;
            qpo.ApplyModifiedPropertiesWithoutUndo();

            // ── QuestLogPanel (HUD, toggle J) — top-right. Component on an ALWAYS-ACTIVE root; a child panel toggles. ──
            var logRoot = NewUI("QuestLog", canvas.transform);
            var lrt = logRoot.GetComponent<RectTransform>();
            lrt.anchorMin = lrt.anchorMax = lrt.pivot = new Vector2(1, 1);
            lrt.sizeDelta = new Vector2(360, 520);
            lrt.anchoredPosition = new Vector2(-20, -20);
            var logPanel = NewUI("LogPanel", logRoot.transform);
            var lprt = logPanel.GetComponent<RectTransform>();
            Stretch(lprt);
            logPanel.AddComponent<Image>().color = new Color(0.05f, 0.06f, 0.09f, 0.85f);
            var lTitle = CreateText("Title", logPanel.transform, "Diário de Missões (J)", 18, TextAlignmentOptions.Center, FontStyles.Bold);
            Anchor(lTitle.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -10), new Vector2(-20, 28), 28);
            var lList = BuildList("List", logPanel.transform, topOffset: -44);

            var lp = logRoot.AddComponent<QuestLogPanel>();
            var lpo = new SerializedObject(lp);
            lpo.FindProperty("library").objectReferenceValue = library;
            lpo.FindProperty("panel").objectReferenceValue = logPanel;
            lpo.FindProperty("rowPrefab").objectReferenceValue = rowPrefab;
            lpo.FindProperty("listParent").objectReferenceValue = lList.transform;
            lpo.ApplyModifiedPropertiesWithoutUndo();

            Undo.RegisterCreatedObjectUndo(giver, "Create Quest Panels");
            Undo.RegisterCreatedObjectUndo(logRoot, "Create Quest Panels");
            Selection.activeGameObject = giver;
            EditorSceneManager.MarkSceneDirty(giver.scene);
            if (library == null) Debug.LogWarning("[UI] Nenhuma ContentLibrary encontrada — arraste-a no campo 'library' dos dois painéis.");
            Debug.Log("[UI] QuestPanel + QuestLogPanel criados (+ QuestRow em " + RowPrefabPath + "). Adicione o NpcController numa cena se ainda não houver.");
        }

        // A vertical list that grows with its rows (VerticalLayoutGroup + ContentSizeFitter), anchored under the title.
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

        private static GameObject BuildQuestRowPrefab()
        {
            var go = new GameObject("QuestRow", typeof(RectTransform), typeof(Image));
            go.GetComponent<Image>().color = new Color(0.12f, 0.14f, 0.20f, 0.95f);
            var vlg = go.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 4; vlg.padding = new RectOffset(10, 10, 8, 8);
            vlg.childControlWidth = true; vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
            var fit = go.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var title = CreateText("Title", go.transform, "Missão", 18, TextAlignmentOptions.Left, FontStyles.Bold);
            AddLayoutMinHeight(title.gameObject, 24);
            var body = CreateText("Body", go.transform, "Descrição + objetivos…", 14, TextAlignmentOptions.TopLeft, FontStyles.Normal);
            body.enableWordWrapping = true;

            var btnGo = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            btnGo.transform.SetParent(go.transform, false);
            btnGo.GetComponent<Image>().color = new Color(0.20f, 0.45f, 0.85f, 1f);
            var le = btnGo.GetComponent<LayoutElement>(); le.minHeight = 30; le.preferredHeight = 30;
            var label = CreateText("Label", btnGo.transform, "Aceitar", 15, TextAlignmentOptions.Center, FontStyles.Normal);
            Stretch(label.rectTransform);

            var row = go.AddComponent<QuestRow>();
            var so = new SerializedObject(row);
            so.FindProperty("titleText").objectReferenceValue = title;
            so.FindProperty("bodyText").objectReferenceValue = body;
            so.FindProperty("actionButton").objectReferenceValue = btnGo.GetComponent<Button>();
            so.FindProperty("actionLabel").objectReferenceValue = label;
            so.ApplyModifiedPropertiesWithoutUndo();

            EnsureFolder(Path.GetDirectoryName(RowPrefabPath).Replace('\\', '/'));
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, RowPrefabPath);
            UnityEngine.Object.DestroyImmediate(go);
            return prefab; // the row root (the Button lives on a child); QuestPanel.rowPrefab is a GameObject
        }

        // ── helpers ──
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
