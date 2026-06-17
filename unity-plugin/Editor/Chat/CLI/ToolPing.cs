// Ping the GameObject referenced by a tool call's args, if any.
// No-ops gracefully on bad/null input. No side effects beyond the editor highlight.
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor.Chat
{
    internal static class ToolPing
    {
        /// <summary>
        /// Returns the value of "path"/"paths" key, falling back to "parent", or null if none present.
        /// </summary>
        internal static string ExtractPath(string argsJson)
        {
            if (string.IsNullOrEmpty(argsJson)) return null;
            return JsonHelper.ExtractString(argsJson, "path")
                ?? JsonHelper.ExtractString(argsJson, "paths")
                ?? JsonHelper.ExtractString(argsJson, "parent");
        }

        /// <summary>
        /// Pings the GameObject found at the path encoded in rec.ArgsJson.
        /// Returns false when path is absent, object not found, object is destroyed, or path is ambiguous.
        /// </summary>
        internal static bool TryPing(ToolCallRecord rec)
        {
            var path = ExtractPath(rec.ArgsJson);
            if (string.IsNullOrEmpty(path)) return false;

            try
            {
                var go = ComponentSerializer.FindObject(path);
                if (go == null || !go) return false;
                EditorGUIUtility.PingObject(go);
                return true;
            }
            catch (System.Exception)
            {
                return false;
            }
        }
    }
}
