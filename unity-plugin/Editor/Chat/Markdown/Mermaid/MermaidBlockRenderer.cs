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

            var layout = MermaidLayout.Compute(graph);
            return new MermaidView(graph, layout);
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
