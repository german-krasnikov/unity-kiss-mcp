// In-session + persistent (EditorPrefs) tool approval cache.
// CLI assembly — no UIElements deps.
using System.Collections.Generic;
using UnityEditor;

namespace UnityMCP.Editor.Chat
{
    internal sealed class SessionAllowlist
    {
        private readonly HashSet<string> _session = new HashSet<string>();
        private const string Prefix = "MCPChat.AlwaysAllow.";

        public bool IsAutoApproved(string toolName)
            => _session.Contains(toolName) || IsAlwaysAllowed(toolName);

        public void AddSession(string toolName) => _session.Add(toolName);

        public void AddAlways(string toolName)
        {
            _session.Add(toolName);
            EditorPrefs.SetBool(Prefix + toolName, true);
        }

        public bool IsAlwaysAllowed(string toolName)
            => EditorPrefs.GetBool(Prefix + toolName, false);

        public void RemoveAlways(string toolName)
            => EditorPrefs.DeleteKey(Prefix + toolName);

        public void ClearSession() => _session.Clear();
    }
}
