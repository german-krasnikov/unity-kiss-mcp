// Pure static helper for Approve & Execute logic — no EditorWindow dependency.
namespace UnityMCP.Editor.Chat
{
    internal static class ApproveHelper
    {
        internal const string ExecutePrompt = "Execute the plan above.";

        /// <summary>Returns the approve prompt when sessionId is valid; null otherwise.</summary>
        internal static string BuildPromptOrNull(string sessionId)
        {
            if (string.IsNullOrEmpty(sessionId)) return null;
            return ExecutePrompt;
        }
    }
}
