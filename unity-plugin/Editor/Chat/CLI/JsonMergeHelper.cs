// Shared JSON string-surgery helper for MCP config merging.
// Pure static — fully NUnit-testable without Unity.
using System;

namespace UnityMCP.Editor.Chat
{
    internal static class JsonMergeHelper
    {
        /// <summary>
        /// Replace the object value of <paramref name="key"/> in <paramref name="json"/>
        /// with <paramref name="newValue"/> using brace-depth matching.
        /// Returns null if the key is not found.
        /// </summary>
        internal static string ReplaceEntry(string json, string key, string newValue)
        {
            if (string.IsNullOrEmpty(json)) return null;

            var searchKey = "\"" + key + "\"";
            var keyIdx = json.IndexOf(searchKey, StringComparison.Ordinal);
            if (keyIdx < 0) return null;

            var braceStart = json.IndexOf('{', keyIdx + searchKey.Length);
            if (braceStart < 0) return null;

            // Walk braces to find the matching closing '}'
            int depth = 1, pos = braceStart + 1;
            while (pos < json.Length && depth > 0)
            {
                if (json[pos] == '{') depth++;
                else if (json[pos] == '}') depth--;
                pos++;
            }
            // pos is now one past the closing '}' — correct slice position

            return json.Substring(0, braceStart) + newValue + json.Substring(pos);
        }
    }
}
