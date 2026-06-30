using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Arcane_Aegis.UI;

namespace Arcane_Aegis.EditorTools
{
    /// <summary>
    /// Builds a working NPC dialogue box into the open scene (real, editable UI — restyle by hand after) and wires the
    /// <see cref="DialoguePanel"/> refs: name + body TMP texts, an options container (VerticalLayoutGroup) and a saved
    /// option-button prefab. Reuses the scene's Canvas/EventSystem. Menu: ArcaneMMO ▸ UI ▸ Create Dialogue Panel.
    /// </summary>
    public static class DialoguePanelBuilder
    {
        private const string ButtonPrefabPath = "Assets/Arcane_Aegis/Prefabs/UI/DialogueOptionButton.prefab";

        [MenuItem("ArcaneMMO/UI/Create Dialogue Panel")]
        public static void Create()
        {
            Canvas canvas = FindOrCreateCanvas();
            EnsureEventSystem();

            // ── Panel (classic bottom-center dialogue box) ──
            var panel = NewUI("DialoguePanel", canvas.transform);
            var prt = panel.GetComponent<RectTransform>();
            prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0f);
            prt.pivot = new Vector2(0.5f, 0f);
            prt.sizeDelta = new Vector2(900, 260);
            prt.anchoredPosition = new Vector2(0, 40);
            panel.AddComponent<Image>().color = new Color(0.05f, 0.06f, 0.09f, 0.94f);

            var name = CreateText("Name", panel.transform, "Nome do NPC", 24, TextAlignmentOptions.Left, FontStyles.Bold);
            var nrt = name.rectTransform;
            nrt.anchorMin = new Vector2(0, 1); nrt.anchorMax = new Vector2(0, 1); nrt.pivot = new Vector2(0, 1);
            nrt.anchoredPosition = new Vector2(24, -14); nrt.sizeDelta = new Vector2(500, 34);

            var body = CreateText("Body", panel.transform, "Texto do diálogo…", 18, TextAlignmentOptions.TopLeft, FontStyles.Normal);
            var brt = body.rectTransform;
            brt.anchorMin = new Vector2(0, 1); brt.anchorMax = new Vector2(1, 1); brt.pivot = new Vector2(0.5f, 1);
            brt.anchoredPosition = new Vector2(0, -56); brt.offsetMin = new Vector2(24, brt.offsetMin.y); brt.offsetMax = new Vector2(-24, brt.offsetMax.y);
            brt.sizeDelta = new Vector2(brt.sizeDelta.x, 96);
            body.enableWordWrapping = true;

            // ── Options container (vertical stack at the bottom) ──
            var options = NewUI("Options", panel.transform);
            var ort = options.GetComponent<RectTransform>();
            ort.anchorMin = new Vector2(0, 0); ort.anchorMax = new Vector2(1, 0); ort.pivot = new Vector2(0.5f, 0);
            ort.anchoredPosition = new Vector2(0, 14); ort.offsetMin = new Vector2(24, 14); ort.offsetMax = new Vector2(-24, 14);
            ort.sizeDelta = new Vector2(ort.sizeDelta.x, 96);
            var vlg = options.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 6; vlg.childAlignment = TextAnchor.LowerCenter;
            vlg.childControlWidth = true; vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;

            Button optionPrefab = BuildOptionButtonPrefab();

            // ── Wire the DialoguePanel ──
            var dp = panel.AddComponent<DialoguePanel>();
            var so = new SerializedObject(dp);
            so.FindProperty("root").objectReferenceValue = panel;            // the panel toggles itself on/off
            so.FindProperty("nameText").objectReferenceValue = name;
            so.FindProperty("bodyText").objectReferenceValue = body;
            so.FindProperty("optionsContainer").objectReferenceValue = options.transform;
            so.FindProperty("optionButtonPrefab").objectReferenceValue = optionPrefab;
            so.ApplyModifiedPropertiesWithoutUndo();

            Undo.RegisterCreatedObjectUndo(panel, "Create Dialogue Panel");
            Selection.activeGameObject = panel;
            EditorSceneManager.MarkSceneDirty(panel.scene);
            Debug.Log("[UI] Painel de diálogo criado + ligado ao DialoguePanel. Botão salvo em " + ButtonPrefabPath + ". Lembre de adicionar o NpcController numa cena.");
        }

        // Build + save the option-button prefab (Button + TMP label), then return the prefab's Button.
        private static Button BuildOptionButtonPrefab()
        {
            var go = new GameObject("DialogueOptionButton", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(860, 34);
            var img = go.GetComponent<Image>();
            img.color = new Color(0.18f, 0.22f, 0.32f, 0.95f);
            go.GetComponent<Button>().targetGraphic = img;
            var le = go.GetComponent<LayoutElement>();
            le.minHeight = 34; le.preferredHeight = 34;

            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(go.transform, false);
            var lrt = labelGo.GetComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one; lrt.offsetMin = new Vector2(12, 0); lrt.offsetMax = new Vector2(-12, 0);
            var label = labelGo.AddComponent<TextMeshProUGUI>();
            label.text = "Opção"; label.fontSize = 18; label.alignment = TextAlignmentOptions.Left; label.color = Color.white; label.enableWordWrapping = false;

            EnsureFolder(Path.GetDirectoryName(ButtonPrefabPath).Replace('\\', '/'));
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, ButtonPrefabPath);
            UnityEngine.Object.DestroyImmediate(go);
            return prefab.GetComponent<Button>();
        }

        // ── shared UI builders ──
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
