// Ordered renderer registry: first CanRender winner takes the block.
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat
{
    internal sealed class ChatBlockRendererRegistry
    {
        private readonly List<IChatBlockRenderer> _renderers = new List<IChatBlockRenderer>();

        internal void Register(IChatBlockRenderer r) => _renderers.Add(r);

        /// <summary>
        /// Returns the first renderer that can handle the block, or a plain rich-text Label fallback.
        /// Never returns null.
        /// </summary>
        internal VisualElement RenderBlock(in MdBlock block)
        {
            foreach (var r in _renderers)
            {
                if (r.CanRender(in block))
                    return r.Render(in block);
            }

            // Fallback: join lines and render as rich-text paragraph.
            var text = block.Lines != null ? string.Join("\n", block.Lines) : "";
            var lbl  = new Label(MarkdownInline.ToRichText(text));
            lbl.enableRichText = true;
            lbl.AddToClassList("md-para");
            return lbl;
        }
    }
}
