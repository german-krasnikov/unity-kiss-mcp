// Renders an Image block: loads PNG/JPG from disk into a Texture2D with proper lifecycle.
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat
{
    internal sealed class ImageBlockRenderer : IChatBlockRenderer
    {
        private const float MaxWidth = 360f;

        public bool CanRender(in MdBlock block) => block.Kind == MdBlockKind.Image;

        public VisualElement Render(in MdBlock block)
        {
            var src = block.Src ?? "";
            var alt = block.Alt ?? "";

            try
            {
                var path = ResolvePath(src);
                if (!File.Exists(path))
                    return AltLabel(alt);

                return BuildImageElement(path, alt);
            }
            catch (Exception)
            {
                return AltLabel(alt);
            }
        }

        private static VisualElement BuildImageElement(string path, string alt)
        {
            var bytes = File.ReadAllBytes(path);
            var tex   = new Texture2D(2, 2);
            tex.LoadImage(bytes);

            float w = Mathf.Min(MaxWidth, tex.width);
            float h = tex.width > 0 ? w * tex.height / tex.width : w;

            var img = new Image { image = tex, scaleMode = ScaleMode.ScaleToFit };
            img.AddToClassList("md-image");
            img.style.width  = w;
            img.style.height = h;

            // MANDATORY: destroy texture when the element leaves the panel.
            img.RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                if (tex != null) UnityEngine.Object.DestroyImmediate(tex);
            });

            img.RegisterCallback<ClickEvent>(_ => EditorUtility.OpenWithDefaultApp(path));

            var container = new VisualElement();
            container.AddToClassList("md-image-container");
            container.Add(img);

            if (!string.IsNullOrEmpty(alt))
            {
                var caption = ChatLabel.Selectable(alt);
                caption.AddToClassList("md-image-alt");
                container.Add(caption);
            }

            return container;
        }

        private static VisualElement AltLabel(string alt)
        {
            var lbl = new Label(string.IsNullOrEmpty(alt) ? "[image]" : alt);
            lbl.AddToClassList("md-image-alt");
            return lbl;
        }

        /// <summary>Returns an absolute path. Supports absolute or project-relative paths.</summary>
        private static string ResolvePath(string src)
        {
            if (Path.IsPathRooted(src)) return src;
            return Path.Combine(Directory.GetCurrentDirectory(), src);
        }
    }
}
