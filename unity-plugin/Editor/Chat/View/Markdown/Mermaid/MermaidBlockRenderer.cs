// Renders a Mermaid block: parses+lays out into MermaidView, or falls back to a code-box.
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat
{
    internal sealed class MermaidBlockRenderer : IChatBlockRenderer
    {
        public bool CanRender(in MdBlock block) => block.Kind == MdBlockKind.Mermaid;

        public VisualElement Render(in MdBlock block)
        {
            var lines = block.Lines != null ? block.Lines.ToArray() : System.Array.Empty<string>();
            var graph = MermaidParser.TryParse(lines);

            if (graph == null)
                return CodeBoxFallback(block);

            var layout  = MermaidLayout.Compute(graph);
            var view    = new MermaidView(graph, layout);
            var scroller = new ScrollView(ScrollViewMode.Horizontal);
            scroller.AddToClassList("mermaid-scroll");
            scroller.Add(view);

            var container = new VisualElement();
            container.AddToClassList("mermaid-container");
            var expandBtn = new Button(() => MermaidViewerWindow.Show(graph, lines)) { text = "↗" };
            expandBtn.AddToClassList("mermaid-expand-btn");
            expandBtn.tooltip = "Open in viewer";
            container.Add(expandBtn);
            container.Add(scroller);
            return container;
        }

        private static VisualElement CodeBoxFallback(in MdBlock block)
        {
            var box  = new VisualElement(); box.AddToClassList("md-codebox");
            var lang = new Label("mermaid"); lang.AddToClassList("md-code-lang");
            box.Add(lang);
            var body = block.Lines != null ? string.Join("\n", block.Lines) : "";
            var code = new Label(body);
            code.enableRichText = false;
            code.AddToClassList("md-code");
            box.Add(code);
            return box;
        }
    }
}
