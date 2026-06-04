// Reflection probe: queries MCPChatWindow.IsChatBackendRunning() without a compile-time
// dependency on the Chat assembly. Returns false when the Chat module is absent or disabled.
using System;
using System.Reflection;

namespace UnityMCP.Editor
{
    internal static class ChatBackendProbe
    {
        private static readonly MethodInfo _method = Resolve();

        private static MethodInfo Resolve()
        {
            var type = Type.GetType("UnityMCP.Editor.Chat.MCPChatWindow, UnityMCP.Editor.Chat");
            return type?.GetMethod("IsChatBackendRunning",
                BindingFlags.Public | BindingFlags.Static);
        }

        internal static bool IsChatBackendRunning()
        {
            if (_method == null) return false;
            try { return (bool)_method.Invoke(null, null); }
            catch { return false; }
        }
    }
}
