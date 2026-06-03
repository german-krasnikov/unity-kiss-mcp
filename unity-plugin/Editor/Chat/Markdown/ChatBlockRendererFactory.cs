// Wires the default renderer pipeline: specific kinds first, catch-all last.
namespace UnityMCP.Editor.Chat
{
    internal static class ChatBlockRendererFactory
    {
        /// <summary>
        /// Registration order matters — first CanRender wins.
        /// Mermaid and Image are specific; MarkdownBlockRenderer is the catch-all.
        /// </summary>
        internal static ChatBlockRendererRegistry CreateDefault()
        {
            var reg = new ChatBlockRendererRegistry();
            reg.Register(new MermaidBlockRenderer());
            reg.Register(new ImageBlockRenderer());
            reg.Register(new MarkdownBlockRenderer());
            return reg;
        }
    }
}
