// Preview builder for 3D models: asset preview + filename/extension metadata.
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat
{
    internal sealed class ModelPreviewBuilder : IPreviewBuilder
    {
        public bool CanBuild(string kindKey, string path)
        {
            if (kindKey == ChipKindKeys.Model) return true;
            return PreviewPathResolver.IsModelFile(path);
        }

        public VisualElement Build(PreviewRequest request, IPreviewContext context)
        {
            var container = new VisualElement();
            container.AddToClassList("chip-preview-model");

            var name = Path.GetFileName(request.Path);
            var ext  = Path.GetExtension(request.Path).ToUpperInvariant();
            var lbl  = new Label($"{name}  ({ext} model)");
            lbl.AddToClassList("chip-preview-model-label");
            container.Add(lbl);

            if (InlinePreviewBuilder.AssetPreviewLoader != null)
            {
                var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(request.Path);
                var tex = InlinePreviewBuilder.AssetPreviewLoader(asset);
                if (tex != null)
                {
                    lbl.RemoveFromHierarchy();
                    container.Add(MakeSharedImage(tex, request.MaxHeight));
                }
                return container;
            }

            context.PreviewService.RequestPreview(request.Path, tex =>
            {
                if (tex == null || container.parent == null) return;
                lbl.RemoveFromHierarchy();
                container.Add(MakeSharedImage(tex, request.MaxHeight));
            }, context.CancellationToken);

            return container;
        }

        static Image MakeSharedImage(Texture2D tex, int maxHeight)
        {
            var img = new Image { image = tex, scaleMode = ScaleMode.ScaleToFit };
            img.style.height = maxHeight;
            img.style.width  = StyleKeyword.Auto;
            return img;
        }
    }
}
