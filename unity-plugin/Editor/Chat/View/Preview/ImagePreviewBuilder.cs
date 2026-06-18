// Preview builder for external images, textures and sprites.
// Creates an Owned Texture2D for files on disk; uses UnityShared for AssetDatabase textures.
using System;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat
{
    internal sealed class ImagePreviewBuilder : IPreviewBuilder
    {
        public bool CanBuild(string kindKey, string path)
        {
            if (kindKey == ChipKindKeys.Image || kindKey == ChipKindKeys.Texture)
                return true;
            return PreviewPathResolver.IsImageFile(path);
        }

        public VisualElement Build(PreviewRequest request, IPreviewContext context)
        {
            var loader = InlinePreviewBuilder.TextureLoader;
            var tex = (loader ?? LoadTexture)(request.Path);
            if (tex == null)
                return ErrorLabel(request.Path);

            var handle = new TextureHandle(tex, loader != null ? TextureOwnership.UnityShared : TextureOwnership.Owned);
            return MakeImageElement(tex, handle, request.MaxHeight);
        }

        static Texture2D LoadTexture(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;

            try
            {
                var resolved = PreviewPathResolver.Resolve(path);
                if (File.Exists(resolved))
                {
                    var bytes = File.ReadAllBytes(resolved);
                    var tex = new Texture2D(2, 2);
                    tex.LoadImage(bytes);
                    return tex;
                }

                if (PreviewPathResolver.IsAssetPath(path))
                    return PreviewPathResolver.LoadAssetTexture(path);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ImagePreviewBuilder] {e.Message} — {path}");
            }
            return null;
        }

        static VisualElement MakeImageElement(Texture2D tex, TextureHandle handle, int maxHeight)
        {
            var img = new Image { image = tex, scaleMode = ScaleMode.ScaleToFit };
            img.AddToClassList("chip-preview-image");
            img.style.height = maxHeight;
            img.style.width  = StyleKeyword.Auto;

            img.RegisterCallback<DetachFromPanelEvent>(_ => handle.Dispose());

            var container = new VisualElement();
            container.AddToClassList("chip-preview-image-container");
            container.Add(img);
            return container;
        }

        static VisualElement ErrorLabel(string path)
        {
            var lbl = new Label($"[image not found] {Path.GetFileName(path)}");
            lbl.AddToClassList("chip-preview-error");
            return lbl;
        }
    }
}
