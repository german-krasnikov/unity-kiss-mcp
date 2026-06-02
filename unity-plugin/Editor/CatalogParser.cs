using System;
using System.Collections.Generic;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Parses the Python-pushed catalog JSON into category→tools map.
    /// Uses JsonHelper primitives — no external JSON dependency.
    ///
    /// Expected shape: {"categories": {"CAT": ["tool", ...], ...}, "core": ["tool", ...]}
    /// </summary>
    internal static class CatalogParser
    {
        public static Dictionary<string, string[]> Parse(string json)
        {
            var result = new Dictionary<string, string[]>();
            if (string.IsNullOrEmpty(json)) return result;

            var categoriesJson = JsonHelper.ExtractObject(json, "categories");
            if (string.IsNullOrEmpty(categoriesJson) || categoriesJson == "{}") return result;

            // Walk top-level keys of categoriesJson
            int i = 1; // skip '{'
            int len = categoriesJson.Length;
            while (i < len - 1)
            {
                // Skip whitespace and commas
                while (i < len && (categoriesJson[i] == ' ' || categoriesJson[i] == ',' ||
                                   categoriesJson[i] == '\n' || categoriesJson[i] == '\r')) i++;
                if (i >= len - 1) break;
                if (categoriesJson[i] != '"') { i++; continue; }

                // Read key
                var keyEnd = categoriesJson.IndexOf('"', i + 1);
                if (keyEnd < 0) break;
                var key = categoriesJson.Substring(i + 1, keyEnd - i - 1);
                i = keyEnd + 1;

                // Skip colon
                while (i < len && categoriesJson[i] != '[' && categoriesJson[i] != '{') i++;
                if (i >= len || categoriesJson[i] != '[') { i++; continue; }

                // Extract array
                var tools = ExtractStringArray(categoriesJson, ref i);
                result[key] = tools;
            }
            return result;
        }

        private static string[] ExtractStringArray(string json, ref int i)
        {
            var list = new List<string>();
            if (i >= json.Length || json[i] != '[') return Array.Empty<string>();

            i++; // skip '['
            while (i < json.Length && json[i] != ']')
            {
                // skip whitespace, commas
                while (i < json.Length && (json[i] == ' ' || json[i] == ',' ||
                                           json[i] == '\n' || json[i] == '\r')) i++;
                if (i >= json.Length || json[i] == ']') break;
                if (json[i] != '"') { i++; continue; }

                i++; // skip opening quote
                var end = json.IndexOf('"', i);
                if (end < 0) break;
                list.Add(json.Substring(i, end - i));
                i = end + 1;
            }
            if (i < json.Length) i++; // skip ']'
            return list.ToArray();
        }
    }
}
