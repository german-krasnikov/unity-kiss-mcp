// Shared JSON string-surgery helpers for MCP config merging.
// Pure static — fully NUnit-testable without Unity.
using System;
using System.Collections.Generic;
using System.Text;

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

        /// <summary>
        /// Finds the index of the closing '}' of the object value for <paramref name="key"/>
        /// within <paramref name="json"/>. Matches the FIRST occurrence of the key — assumes
        /// the key is at the top level of the JSON structure (graceful fallback if not found).
        /// Returns -1 if the key or its block cannot be located.
        /// </summary>
        internal static int FindBlockClose(string json, string key)
        {
            if (string.IsNullOrEmpty(json)) return -1;
            // Matches FIRST "key" occurrence — assumes top-level placement.
            var keyIdx = json.IndexOf("\"" + key + "\"", StringComparison.Ordinal);
            if (keyIdx < 0) return -1;
            var braceStart = json.IndexOf('{', keyIdx + key.Length + 2);
            if (braceStart < 0) return -1;
            int depth = 1, pos = braceStart + 1;
            while (pos < json.Length && depth > 0)
            {
                if (json[pos] == '{') depth++;
                else if (json[pos] == '}') { depth--; if (depth == 0) return pos; }
                pos++;
            }
            return -1;
        }

        /// <summary>
        /// Extracts key-object entries from <paramref name="blockContent"/> (the inner text of
        /// a JSON object, excluding surrounding braces). Entries whose values are NOT JSON objects
        /// (e.g. arrays, strings, numbers) are silently skipped — MCP server entries are always
        /// objects by spec, and non-object fields at the same level are safely ignored.
        /// Entries whose keys pass <paramref name="keyFilter"/> returning false are excluded.
        /// Returns list of raw "\"key\": {...}" strings ready for injection.
        /// </summary>
        internal static List<string> ExtractObjectEntries(
            string blockContent, Func<string, bool> keyFilter)
        {
            var entries = new List<string>();
            int i = 0;
            while (i < blockContent.Length)
            {
                // Skip whitespace and commas between entries
                while (i < blockContent.Length
                       && (blockContent[i] == ',' || char.IsWhiteSpace(blockContent[i])))
                    i++;
                if (i >= blockContent.Length) break;
                if (blockContent[i] != '"') break;

                // Parse key
                int keyStart = i + 1;
                int keyEnd   = blockContent.IndexOf('"', keyStart);
                if (keyEnd < 0) break;
                var key = blockContent.Substring(keyStart, keyEnd - keyStart);
                i = keyEnd + 1;

                // Skip whitespace and colon to find value start
                while (i < blockContent.Length
                       && blockContent[i] != '{' && blockContent[i] != '[' && blockContent[i] != '"'
                       && blockContent[i] != ':')
                    i++;
                // Skip the colon itself if present
                if (i < blockContent.Length && blockContent[i] == ':') i++;
                while (i < blockContent.Length && char.IsWhiteSpace(blockContent[i])) i++;

                if (i >= blockContent.Length) break;

                char valueStart = blockContent[i];
                if (valueStart == '{')
                {
                    // Object value — brace-depth walk
                    int valStart = i;
                    int d = 1; i++;
                    while (i < blockContent.Length && d > 0)
                    {
                        if (blockContent[i] == '{') d++;
                        else if (blockContent[i] == '}') d--;
                        i++;
                    }
                    var value = blockContent.Substring(valStart, i - valStart);
                    if (keyFilter(key))
                        entries.Add($"\"{key}\": {value}");
                }
                else if (valueStart == '[')
                {
                    // Array value — bracket-depth walk to skip cleanly (not an MCP server object)
                    int d = 1; i++;
                    while (i < blockContent.Length && d > 0)
                    {
                        if (blockContent[i] == '[') d++;
                        else if (blockContent[i] == ']') d--;
                        i++;
                    }
                    // Non-object entry — skip (not included in output)
                }
                else if (valueStart == '"')
                {
                    // String value — skip escape-aware to avoid stopping at '}' inside the string
                    i++; // skip opening "
                    while (i < blockContent.Length && blockContent[i] != '"')
                    {
                        if (blockContent[i] == '\\') i++; // skip escaped char
                        i++;
                    }
                    if (i < blockContent.Length) i++; // skip closing "
                    // Non-object entry — skip
                }
                else
                {
                    // number / bool / null — scan to next comma or end
                    while (i < blockContent.Length && blockContent[i] != ',' && blockContent[i] != '}')
                        i++;
                    // Non-object entry — skip
                }
            }
            return entries;
        }

        /// <summary>
        /// Injects <paramref name="extraEntries"/> before the closing '}' of the named key's
        /// block in <paramref name="baseJson"/>. Returns baseJson unchanged if block not found.
        /// </summary>
        internal static string InjectBeforeBlockClose(
            string baseJson, string key, IList<string> extraEntries)
        {
            if (extraEntries == null || extraEntries.Count == 0) return baseJson;
            var closeIdx = FindBlockClose(baseJson, key);
            if (closeIdx < 0) return baseJson;
            var sb = new StringBuilder(baseJson.Substring(0, closeIdx));
            foreach (var entry in extraEntries)
                sb.Append(",\n    ").Append(entry);
            sb.Append(baseJson.Substring(closeIdx));
            return sb.ToString();
        }
    }
}
