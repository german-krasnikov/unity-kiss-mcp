// Builds an inline preview VisualElement for a chip (texture/image/model/prefab/audio).
// TextureLoader seam allows test injection without file I/O.
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat
{
    internal static class InlinePreviewBuilder
    {
        /// <summary>
        /// Seam: replaces File.ReadAllBytes + LoadImage pipeline. Set in tests to avoid I/O.
        /// When null, falls back to the default file-based loader.
        /// </summary>
        internal static Func<string, Texture2D> TextureLoader;

        /// <summary>
        /// Returns a preview VisualElement for the given kindKey + path, or null if unsupported.
        /// </summary>
        internal static VisualElement Build(string kindKey, string path)
        {
            if (string.IsNullOrEmpty(path)) return null;

            switch (kindKey)
            {
                case ChipKindKeys.Texture:
                case ChipKindKeys.Image:
                    return BuildImagePreview(path);

                case ChipKindKeys.Model:
                case ChipKindKeys.Prefab:
                    return BuildAssetPreview(path);

                case ChipKindKeys.Audio:
                    return BuildAudioInfo(path);

                default:
                    return null;
            }
        }

        // ── private ───────────────────────────────────────────────────────────

        static VisualElement BuildImagePreview(string path)
        {
            var tex = (TextureLoader ?? LoadTexture)(path);
            if (tex == null) return null;

            var img = new Image { image = tex, scaleMode = ScaleMode.ScaleToFit };
            img.AddToClassList("chip-preview-image");
            img.style.height = 120f;
            img.style.width  = StyleKeyword.Auto;

            var container = new VisualElement();
            container.AddToClassList("chip-preview-image-container");
            container.Add(img);
            return container;
        }

        static VisualElement BuildAssetPreview(string path)
        {
            var container = new VisualElement();
            container.AddToClassList("chip-preview-asset");

            // Try Unity asset preview (works in Editor, may return null for missing assets)
            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
            Texture2D preview = asset != null ? AssetPreview.GetAssetPreview(asset) : null;

            if (preview != null)
            {
                var img = new Image { image = preview, scaleMode = ScaleMode.ScaleToFit };
                img.style.height = 120f;
                img.style.width  = StyleKeyword.Auto;
                container.Add(img);
            }
            else
            {
                var lbl = new Label(Path.GetFileName(path));
                lbl.AddToClassList("chip-preview-asset-label");
                container.Add(lbl);
            }

            return container;
        }

        static VisualElement BuildAudioInfo(string path)
        {
            var container = new VisualElement();
            container.AddToClassList("chip-preview-audio");

            var lbl = new Label(Path.GetFileName(path));
            lbl.AddToClassList("chip-preview-audio-label");
            container.Add(lbl);
            return container;
        }

        static Texture2D LoadTexture(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                var bytes = File.ReadAllBytes(path);
                var tex = new Texture2D(2, 2);
                tex.LoadImage(bytes);
                return tex;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[InlinePreviewBuilder] {e.Message} — {path}");
                return null;
            }
        }
    }
}
