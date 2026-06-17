// Builds a clickable 80px thumbnail for a plain-text image path in a paragraph.
// Missing file → placeholder Label. No caching (v1).
using System;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat
{
    internal static class InlineImageThumbnail
    {
        internal const float ThumbHeight = 80f;

        /// <summary>
        /// True if token looks like an image path (absolute or relative with image ext).
        /// Pure: no I/O. Strips surrounding backticks before checking.
        /// </summary>
        internal static bool IsImagePath(string token)
        {
            if (string.IsNullOrEmpty(token)) return false;
            var t = token.Trim();
            if (t.Length >= 2 && t[0] == '`' && t[t.Length - 1] == '`')
                t = t.Substring(1, t.Length - 2);
            return ImageBlockRenderer.IsImageFile(t);
        }

        /// <summary>
        /// Returns a thumbnail VisualElement (Image) or fallback Label. Never null.
        /// </summary>
        internal static VisualElement Build(string absolutePath)
        {
            if (!ImageBlockRenderer.IsImageFile(absolutePath) || !File.Exists(absolutePath))
                return MissingLabel(absolutePath);

            try
            {
                var bytes = File.ReadAllBytes(absolutePath);
                var tex   = new Texture2D(2, 2);
                tex.LoadImage(bytes);

                var img = new Image { image = tex, scaleMode = ScaleMode.ScaleToFit };
                img.AddToClassList("md-image-thumb");
                img.style.height = ThumbHeight;
                img.style.width  = StyleKeyword.Auto;

                img.RegisterCallback<DetachFromPanelEvent>(_ =>
                {
                    if (tex != null) Object.DestroyImmediate(tex);
                });

                var captured = absolutePath;
                img.RegisterCallback<ClickEvent>(_ => ImageViewerWindow.Show(captured));

                return img;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[InlineImage] {e.Message} — path: {absolutePath}");
                return MissingLabel(absolutePath);
            }
        }

        private static Label MissingLabel(string path)
        {
            var lbl = new Label("\U0001f4f7 " + Path.GetFileName(path));
            lbl.AddToClassList("md-image-thumb--missing");
            return lbl;
        }
    }
}
