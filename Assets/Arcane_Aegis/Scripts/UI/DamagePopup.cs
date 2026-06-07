using UnityEngine;

namespace Arcane_Aegis.UI
{
    /// <summary>
    /// Spawns a floating damage/heal number in the world. Uses a runtime TextMesh (no prefab/setup needed) —
    /// swap for TextMeshPro later. White = hit, yellow = crit, green = heal.
    /// </summary>
    public static class DamagePopup
    {
        private static Font _font;

        public static void Spawn(Vector3 worldPos, int amount, bool crit, bool heal)
        {
            if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var go = new GameObject("DamagePopup");
            go.transform.position = worldPos;

            var tm = go.AddComponent<TextMesh>();
            tm.font = _font;
            tm.fontSize = 64;
            tm.characterSize = 0.06f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.text = heal ? $"+{amount}" : amount.ToString();
            tm.color = heal ? new Color(0.4f, 1f, 0.4f) : (crit ? new Color(1f, 0.85f, 0.2f) : Color.white);

            if (_font != null) go.GetComponent<MeshRenderer>().sharedMaterial = _font.material;

            go.AddComponent<FloatingText>();
        }
    }
}
