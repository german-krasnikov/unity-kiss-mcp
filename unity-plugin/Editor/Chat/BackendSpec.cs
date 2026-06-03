// Immutable descriptor for a backend / agent choice shown in the selector dropdown.
namespace UnityMCP.Editor.Chat
{
    internal readonly struct BackendSpec
    {
        internal readonly string DisplayName; // shown in UI
        internal readonly string AgentName;   // passed to --agent (null = default Claude)
        internal readonly bool   Enabled;     // false = placeholder (Codex soon)

        internal BackendSpec(string displayName, string agentName, bool enabled)
        {
            DisplayName = displayName;
            AgentName   = agentName;
            Enabled     = enabled;
        }
    }
}
