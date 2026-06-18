// Generic asset preview builder: delegates to IAssetPreviewService for the thumbnail.
// Falls back to a filename label if no preview is available.
using System;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat
{
    internal sealed class AssetPreviewBuilder : IPreviewBuilder
    {
        static readonly string[] HandledKinds =
        {
            ChipKindKeys.Texture, ChipKindKeys.Material, ChipKindKeys.Prefab,
            ChipKindKeys.Model, ChipKindKeys.ScriptableObject, ChipKindKeys.Scene,
            ChipKindKeys.Folder, ChipKindKeys.Script, ChipKindKeys.Asset
        };

        public bool CanBuild(string kindKey, string path)
        {
            foreach (var k in HandledKinds)
                if (k == kindKey) return true;
            return false;
        }

        public VisualElement Build(PreviewRequest request, IPreviewContext context)
        {
            var container = new VisualElement();
            container.AddToClassList("chip-preview-asset");

            var lbl = new Label(Path.GetFileName(request.Path));
            lbl.AddToClassList("chip-preview-asset-label");
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
