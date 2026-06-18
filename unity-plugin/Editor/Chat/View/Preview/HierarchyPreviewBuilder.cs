// Preview builder for hierarchy/scene object references: lightweight mini-inspector.
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat
{
    internal sealed class HierarchyPreviewBuilder : IPreviewBuilder
    {
        public bool CanBuild(string kindKey, string path)
            => kindKey == ChipKindKeys.Hierarchy;

        public VisualElement Build(PreviewRequest request, IPreviewContext context)
        {
            var href = HierarchyReference.Parse(request.Path);
            var resolver = new HierarchyResolver();
            var go = resolver.Resolve(href);

            var container = new VisualElement();
            container.AddToClassList("chip-preview-hierarchy");

            if (go == null)
            {
                var lbl = new Label("[not found in scene]");
                lbl.AddToClassList("chip-preview-hierarchy-missing");
                container.Add(lbl);
                return container;
            }

            var header = new Label($"{go.name}  {(go.activeSelf ? "●" : "○")}");
            header.AddToClassList("chip-preview-hierarchy-name");
            container.Add(header);

            var t = go.transform;
            container.Add(new Label($"Pos {t.position}\nRot {t.rotation.eulerAngles}\nScl {t.localScale}"));

            var components = go.GetComponents<Component>();
            if (components.Length > 0)
            {
                var sb = new StringBuilder("Components:");
                foreach (var c in components)
                {
                    if (c == null) continue; // missing script
                    sb.Append("\n• ");
                    sb.Append(c.GetType().Name);
                }
                container.Add(new Label(sb.ToString()));
            }

            return container;
        }
    }
}
