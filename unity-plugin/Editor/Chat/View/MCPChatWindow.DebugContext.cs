// Partial MCPChatWindow — debug context injection.
// When in Play Mode with active watches, BuildDebugContext() returns
// a compact summary that can be prepended to outgoing messages.
using System.Text;
using UnityEditor;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Chat
{
    public partial class MCPChatWindow
    {
        /// <summary>
        /// Returns a compact debug context string when in Play Mode with active watches.
        /// Format: "[debug] field1=val1 (!) field2=val2"
        /// Returns null when not applicable.
        /// </summary>
        internal string BuildDebugContext()
        {
            if (!EditorApplication.isPlaying) return null;
            if (WatchRegistry.All.Count == 0) return null;

            var sb = new StringBuilder("[debug]");
            foreach (var (_, entry) in WatchRegistry.All)
            {
                sb.Append(' ');
                sb.Append(entry.Field);
                sb.Append('=');
                sb.Append(entry.LastValue?.ToString() ?? "?");
                if (entry.Triggered) sb.Append("(!)");
            }
            return sb.ToString();
        }
    }
}
