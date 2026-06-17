// Partial: toolbar button injection from ToolbarButtonRegistry.
// BuildPluginButtons is called from BuildFooterBar (MCPChatWindow.FlowBar.cs)
// after the mode segment and before the spacer.
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat
{
    public partial class MCPChatWindow
    {
        /// <summary>
        /// Inject registered plugin buttons into the footer bar, before the spacer.
        /// Wraps OnClick in try/catch so a misbehaving plugin never crashes the window.
        /// </summary>
        internal void BuildPluginButtons(VisualElement bar)
        {
            foreach (var p in ToolbarButtonRegistry.All)
            {
                var provider = p; // capture for lambda
                var btn = new Button(() =>
                {
                    try { provider.OnClick(this); }
                    catch (System.Exception e) { Debug.LogException(e); }
                })
                {
                    text    = provider.ButtonLabel,
                    tooltip = provider.Tooltip,
                };
                btn.AddToClassList("chat-btn");
                btn.AddToClassList("chat-btn--plugin");
                bar.Add(btn);
            }
        }
    }
}
