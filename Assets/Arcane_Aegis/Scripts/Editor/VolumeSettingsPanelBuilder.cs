using System;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Arcane_Aegis.Audio;

namespace Arcane_Aegis.EditorTools
{
    /// <summary>
    /// Builds a working volume-settings panel straight into the open scene (real, editable UI objects — restyle them by
    /// hand after) and wires the sliders into a <see cref="VolumeSettings"/> component. Reuses the scene's Canvas/EventSystem
    /// if present. Menu: ArcaneMMO ▸ UI ▸ Create Volume Settings Panel.
    /// </summary>
    public static class VolumeSettingsPanelBuilder
    {
        [MenuItem("ArcaneMMO/UI/Create Volume Settings Panel")]
        public static void Create()
        {
            Canvas canvas = FindOrCreateCanvas();
            EnsureEventSystem();

            // ── Panel ──
            var panel = NewUI("VolumeSettingsPanel", canvas.transform);
            var prt = panel.GetComponent<RectTransform>();
            prt.anchorMin = prt.anchorMax = prt.pivot = new Vector2(0.5f, 0.5f);
            prt.sizeDelta = new Vector2(480, 300);
            prt.anchoredPosition = Vector2.zero;
            var panelImg = panel.AddComponent<Image>();
            panelImg.color = new Color(0.06f, 0.07f, 0.10f, 0.92f);

            CreateText("Title", panel.transform, "Áudio", 26, TextAlignmentOptions.Center, new Vector2(0, 110), new Vector2(440, 40), FontStyles.Bold);

            // ── Rows: label + slider ──
            CreateText("SfxLabel", panel.transform, "SFX", 18, TextAlignmentOptions.Left, new Vector2(-120, 40), new Vector2(140, 30));
            var (_, sfxSlider) = CreateSlider("SfxSlider", panel.transform, new Vector2(240, 20), new Vector2(80, 40));

            CreateText("MusicLabel", panel.transform, "Música", 18, TextAlignmentOptions.Left, new Vector2(-120, -10), new Vector2(140, 30));
            var (_, musicSlider) = CreateSlider("MusicSlider", panel.transform, new Vector2(240, 20), new Vector2(80, -10));

            CreateText("Hint", panel.transform, "(arraste pra ajustar — salva sozinho)", 12, TextAlignmentOptions.Center, new Vector2(0, -110), new Vector2(440, 24));

            // ── Wire the VolumeSettings component ──
            var vs = panel.AddComponent<VolumeSettings>();
            var so = new SerializedObject(vs);
            so.FindProperty("sfxSlider").objectReferenceValue = sfxSlider;
            so.FindProperty("musicSlider").objectReferenceValue = musicSlider;
            so.ApplyModifiedPropertiesWithoutUndo();

            Undo.RegisterCreatedObjectUndo(panel, "Create Volume Settings Panel");
            Selection.activeGameObject = panel;
            EditorSceneManager.MarkSceneDirty(panel.scene);
            Debug.Log("[UI] Painel de volume criado + ligado ao VolumeSettings. Restile à vontade (cores/fontes/posição).");
        }

        // ── Canvas / EventSystem ──
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
            // New Input System: add InputSystemUIInputModule by reflection so this editor script has no hard package dep.
            var moduleType = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (moduleType != null) go.AddComponent(moduleType);
            else go.AddComponent<StandaloneInputModule>(); // fallback (legacy input)
            Undo.RegisterCreatedObjectUndo(go, "Create EventSystem");
        }

        // ── UI builders ──
        private static GameObject NewUI(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            return go;
        }

        private static TMP_Text CreateText(string name, Transform parent, string content, float size, TextAlignmentOptions align,
                                           Vector2 pos, Vector2 sizeDelta, FontStyles style = FontStyles.Normal)
        {
            var go = NewUI(name, parent);
            var rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = sizeDelta;
            var t = go.AddComponent<TextMeshProUGUI>(); // auto-assigns TMP_Settings.defaultFontAsset (import TMP Essentials once if blank)
            t.text = content;
            t.fontSize = size;
            t.alignment = align;
            t.fontStyle = style;
            t.color = Color.white;
            t.enableWordWrapping = false;
            return t;
        }

        // Builds Unity's canonical Slider hierarchy (Background / Fill Area→Fill / Handle Slide Area→Handle) so it's a
        // real, fully-functional slider you can skin.
        private static (GameObject go, Slider slider) CreateSlider(string name, Transform parent, Vector2 size, Vector2 pos)
        {
            var go = NewUI(name, parent);
            var rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var slider = go.AddComponent<Slider>();

            var bg = NewUI("Background", go.transform);
            var bgrt = bg.GetComponent<RectTransform>();
            bgrt.anchorMin = new Vector2(0, 0.25f); bgrt.anchorMax = new Vector2(1, 0.75f);
            bgrt.offsetMin = Vector2.zero; bgrt.offsetMax = Vector2.zero;
            bg.AddComponent<Image>().color = new Color(0.16f, 0.16f, 0.18f, 1f);

            var fillArea = NewUI("Fill Area", go.transform);
            var fart = fillArea.GetComponent<RectTransform>();
            fart.anchorMin = new Vector2(0, 0.25f); fart.anchorMax = new Vector2(1, 0.75f);
            fart.offsetMin = new Vector2(5, 0); fart.offsetMax = new Vector2(-15, 0);
            var fill = NewUI("Fill", fillArea.transform);
            var fillrt = fill.GetComponent<RectTransform>();
            fillrt.anchorMin = new Vector2(0, 0); fillrt.anchorMax = new Vector2(1, 1);
            fillrt.offsetMin = Vector2.zero; fillrt.offsetMax = Vector2.zero;
            fillrt.sizeDelta = new Vector2(10, 0);
            fill.AddComponent<Image>().color = new Color(0.30f, 0.68f, 1f, 1f);

            var hsa = NewUI("Handle Slide Area", go.transform);
            var hsart = hsa.GetComponent<RectTransform>();
            hsart.anchorMin = new Vector2(0, 0); hsart.anchorMax = new Vector2(1, 1);
            hsart.offsetMin = new Vector2(10, 0); hsart.offsetMax = new Vector2(-10, 0);
            var handle = NewUI("Handle", hsa.transform);
            var hrt = handle.GetComponent<RectTransform>();
            hrt.anchorMin = new Vector2(0, 0); hrt.anchorMax = new Vector2(0, 1);
            hrt.sizeDelta = new Vector2(20, 0);
            var handleImg = handle.AddComponent<Image>();
            handleImg.color = Color.white;

            slider.fillRect = fillrt;
            slider.handleRect = hrt;
            slider.targetGraphic = handleImg;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f; slider.maxValue = 1f; slider.value = 1f;
            return (go, slider);
        }
    }
}
