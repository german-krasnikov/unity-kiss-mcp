// Public interface for extensible toolbar button contributions.
// Implement and call ToolbarButtonRegistry.Register() from [InitializeOnLoad].

namespace UnityMCP.Editor.Chat
{
    /// <summary>
    /// Adds a button to the MCPChatWindow footer bar (left of the spacer).
    /// Implement and call ToolbarButtonRegistry.Register() from [InitializeOnLoad].
    /// </summary>
    public interface IToolbarButtonProvider
    {
        /// <summary>Unique lowercase key. Must match ^[a-z0-9_]+$.</summary>
        string Key { get; }

        /// <summary>Lower = further left in the footer bar.</summary>
        int Order { get; }

        string ButtonLabel { get; }
        string Tooltip { get; }

        /// <summary>
        /// When true, omit from the footer toolbar and show only in the session menu.
        /// Defaults to false for backward compatibility.
        /// </summary>
        bool MenuOnly => false;

        /// <summary>Called when the user clicks the button. Receives the host window.</summary>
        void OnClick(UnityEditor.EditorWindow window);
    }
}
