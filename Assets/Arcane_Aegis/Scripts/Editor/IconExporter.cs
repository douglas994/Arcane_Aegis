using System.IO;
using UnityEditor;
using UnityEngine;
using Arcane_Aegis.Content;

namespace Arcane_Aegis.EditorTools
{
    /// <summary>
    /// Dev tool: exports every <see cref="ItemDefinitionSO"/>'s icon Sprite as a PNG into the web admin's static folder
    /// (<c>ArcaneAdmin/wwwroot/icons/{id}.png</c>), keyed by the ITEM id. The web panel renders <c>&lt;img src="/icons/{id}.png"&gt;</c>,
    /// so item icons show in the inventory + the give-item catalog WITHOUT shipping sprites over the wire (icons stay client-side).
    /// Handles non-readable / atlassed textures by blitting through a temporary RenderTexture.
    /// </summary>
    public static class IconExporter
    {
        [MenuItem("ArcaneMMO/Export Item Icons → Admin")]
        public static void Export()
        {
            // Unity project lives at <root>/Arcane_Aegis; the admin at <root>/ArcaneMMO/ArcaneAdmin (sibling roots).
            string outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "ArcaneMMO", "ArcaneAdmin", "wwwroot", "icons"));
            Directory.CreateDirectory(outDir);

            int ok = 0, skipped = 0;
            string[] guids = AssetDatabase.FindAssets("t:ItemDefinitionSO");
            foreach (string guid in guids)
            {
                var so = AssetDatabase.LoadAssetAtPath<ItemDefinitionSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (so == null || string.IsNullOrWhiteSpace(so.id)) { skipped++; continue; }
                if (so.icon == null) { skipped++; continue; }

                byte[] png = EncodeSprite(so.icon);
                if (png == null) { Debug.LogWarning($"[IconExporter] não consegui ler o sprite de '{so.id}'"); skipped++; continue; }

                File.WriteAllBytes(Path.Combine(outDir, $"{so.id}.png"), png);
                ok++;
            }

            Debug.Log($"[IconExporter] exportados {ok} ícone(s) → {outDir} ({skipped} pulado(s)).");
            EditorUtility.DisplayDialog("Exportar ícones",
                $"{ok} ícone(s) exportado(s) para:\n{outDir}\n\n({skipped} item(ns) sem id/ícone foram pulados.)\n\nSe o admin roda no Docker, ele já lê essa pasta (bind-mount). Senão, rode 'docker compose up --build -d admin'.",
                "OK");
        }

        /// <summary>Encodes a Sprite's pixels to PNG, extracting just the sprite's rect from its (possibly atlassed) texture and
        /// going through a RenderTexture so it works even when the source texture has Read/Write disabled.</summary>
        private static byte[] EncodeSprite(Sprite sprite)
        {
            Texture2D src = sprite.texture;
            if (src == null) return null;

            Rect r = sprite.textureRect;          // the sprite's sub-rect inside the texture (whole texture for a Single sprite)
            int w = Mathf.Max(1, (int)r.width);
            int h = Mathf.Max(1, (int)r.height);

            RenderTexture rt = RenderTexture.GetTemporary(src.width, src.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            RenderTexture prev = RenderTexture.active;
            try
            {
                Graphics.Blit(src, rt);
                RenderTexture.active = rt;

                var readable = new Texture2D(w, h, TextureFormat.RGBA32, false);
                readable.ReadPixels(new Rect(r.x, r.y, w, h), 0, 0); // copy just the sprite's rect
                readable.Apply();

                byte[] png = readable.EncodeToPNG();
                Object.DestroyImmediate(readable);
                return png;
            }
            finally
            {
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);
            }
        }
    }
}
