// Wires the default renderer pipeline: specific kinds first, catch-all last.
using System;

namespace UnityMCP.Editor.Chat
{
    public static class ChatBlockRendererFactory
    {
        /// <summary>
        /// Registration order matters — first CanRender wins.
        /// Mermaid and Image are specific; MarkdownBlockRenderer is the catch-all.
        /// </summary>
        internal static ChatBlockRendererRegistry CreateDefault(
            ChatRefResolver resolver = null,
            Action<string> addToContext = null)
        {
            var reg = new ChatBlockRendererRegistry();
            reg.Register(new MermaidBlockRenderer());
            reg.Register(new ImageBlockRenderer());
            reg.Register(new MarkdownBlockRenderer());
            if (resolver != null)
                reg.SetLinkSupport(resolver, addToContext);
            return reg;
        }
    }
}
