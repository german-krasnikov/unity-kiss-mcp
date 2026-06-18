// Preview builder for prefabs: asset preview + child/component count metadata.
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat
{
    internal sealed class PrefabPreviewBuilder : IPreviewBuilder
    {
        public bool CanBuild(string kindKey, string path)
            => kindKey == ChipKindKeys.Prefab || path?.EndsWith(".prefab") == true;

        public VisualElement Build(PreviewRequest request, IPreviewContext context)
        {
            var container = new VisualElement();
            container.AddToClassList("chip-preview-prefab");

            var name = Path.GetFileName(request.Path);
            var meta = GetPrefabMeta(request.Path);
            var lbl  = new Label(string.IsNullOrEmpty(meta) ? name : $"{name}  ({meta})");
            lbl.AddToClassList("chip-preview-prefab-label");
            container.Add(lbl);

            if (InlinePreviewBuilder.AssetPreviewLoader != null)
            {
                var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(request.Path);
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

        static string GetPrefabMeta(string path)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null) return "";
            int children = go.transform.childCount;
            int comps    = go.GetComponents<Component>().Length;
            return $"{children} children, {comps} components";
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
