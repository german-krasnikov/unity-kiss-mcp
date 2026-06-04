// Reflection probe: queries MCPChatWindow.IsChatBackendRunning() without a compile-time
// dependency on the Chat assembly. Returns false when the Chat module is absent or disabled.
// MethodInfo is resolved per-call (not cached) so stale refs after domain reload can't
// cause false negatives (status bar showing Listen instead of ChatActive).
using System;
using System.Reflection;

namespace UnityMCP.Editor
{
    internal static class ChatBackendProbe
    {
        internal static bool IsChatBackendRunning()
        {
            try
            {
                var method = Type.GetType("UnityMCP.Editor.Chat.MCPChatWindow, UnityMCP.Editor.Chat")
                    ?.GetMethod("IsChatBackendRunning", BindingFlags.Public | BindingFlags.Static);
                return method != null && (bool)method.Invoke(null, null);
            }
            catch { return false; }
        }
    }
}
