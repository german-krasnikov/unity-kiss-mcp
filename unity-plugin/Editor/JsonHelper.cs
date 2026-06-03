using System.Text;

namespace UnityMCP.Editor
{
    public static class JsonHelper
    {
        private static int FindKeyIndex(string json, string needle)
        {
            bool inString = false;
            int depth = 0;
            for (int i = 0; i < json.Length; i++)
            {
                char c = json[i];
                if (inString)
                {
                    if (c == '\\') { i++; continue; }
                    if (c == '"') inString = false;
                    continue;
                }
                if (c == '{' || c == '[') { depth++; continue; }
                if (c == '}' || c == ']') { depth--; continue; }
                if (c == '"')
                {
                    if (depth <= 1 && i + needle.Length <= json.Length &&
                        string.CompareOrdinal(json, i, needle, 0, needle.Length) == 0)
                    {
                        int j = i + needle.Length;
                        while (j < json.Length && json[j] == ' ') j++;
                        if (j < json.Length && json[j] == ':')
                            return i;
                    }
                    inString = true;
                }
            }
            return -1;
        }

        public static string ExtractString(string json, string key)
        {
            if (string.IsNullOrEmpty(json)) return null;
            var needle = $"\"{key}\"";
            var idx = FindKeyIndex(json, needle);
            if (idx == -1) return null;

            var colon = json.IndexOf(':', idx + needle.Length);
            if (colon == -1) return null;

            var i = colon + 1;
            while (i < json.Length && json[i] == ' ') i++;
            if (i >= json.Length) return null;

            if (i + 4 <= json.Length && json.Substring(i, 4) == "null")
                return null;

            if (json[i] == '"')
            {
                i++;
                var end = i;
                while (end < json.Length)
                {
                    if (json[end] == '"')
                    {
                        int backslashes = 0;
                        int b = end - 1;
                        while (b >= i && json[b] == '\\') { backslashes++; b--; }
                        if (backslashes % 2 == 0) break;
                    }
                    end++;
                }
                if (end >= json.Length) return null;
                return UnescapeJsonString(json.Substring(i, end - i));
            }

            var endIdx = i;
            while (endIdx < json.Length && json[endIdx] != ',' && json[endIdx] != '}')
                endIdx++;
            return json.Substring(i, endIdx - i).Trim();
        }

        internal static string ExtractObject(string json, string key)
        {
            if (string.IsNullOrEmpty(json)) return "{}";
            var needle = $"\"{key}\"";
            var idx = FindKeyIndex(json, needle);
            if (idx == -1) return "{}";

            var braceStart = json.IndexOf('{', idx + needle.Length);
            if (braceStart == -1) return "{}";

            int depth = 0;
            for (int i = braceStart; i < json.Length; i++)
            {
                if (json[i] == '{') depth++;
                else if (json[i] == '}') depth--;
                if (depth == 0) return json.Substring(braceStart, i - braceStart + 1);
            }
            return "{}";
        }

        internal static string ExtractArray(string json, string key)
        {
            if (string.IsNullOrEmpty(json)) return "[]";
            var needle = $"\"{key}\"";
            var idx = FindKeyIndex(json, needle);
            if (idx == -1) return "[]";
            var bracketStart = json.IndexOf('[', idx + needle.Length);
            if (bracketStart == -1) return "[]";
            int depth = 0;
            for (int i = bracketStart; i < json.Length; i++)
            {
                if (json[i] == '[') depth++;
                else if (json[i] == ']') depth--;
                if (depth == 0) return json.Substring(bracketStart, i - bracketStart + 1);
            }
            return "[]";
        }

        public static string UnescapeJsonString(string s)
        {
            if (s == null || s.IndexOf('\\') < 0) return s;
            var sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] == '\\' && i + 1 < s.Length)
                {
                    switch (s[i + 1])
                    {
                        case '"':  sb.Append('"');  i++; break;
                        case '\\': sb.Append('\\'); i++; break;
                        case 'n':  sb.Append('\n'); i++; break;
                        case 'r':  sb.Append('\r'); i++; break;
                        case 't':  sb.Append('\t'); i++; break;
                        default:   sb.Append(s[i]); break;
                    }
                }
                else sb.Append(s[i]);
            }
            return sb.ToString();
        }

        internal static string EscapeJson(string str)
        {
            if (string.IsNullOrEmpty(str)) return "";
            return str
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t");
        }

        internal static string FormatResponse(string id, bool ok, string data, string error)
        {
            var sb = new StringBuilder();
            sb.Append("{\"id\":\"").Append(EscapeJson(id ?? "")).Append("\"");
            sb.Append(",\"ok\":").Append(ok ? "true" : "false");
            if (ok)
                sb.Append(",\"data\":\"").Append(EscapeJson(data ?? "")).Append("\"");
            else
                sb.Append(",\"err\":\"").Append(EscapeJson(error ?? "")).Append("\"");
            sb.Append("}");
            return sb.ToString();
        }

        internal static string FormatFileResponse(string id, string filePath)
        {
            var sb = new StringBuilder();
            sb.Append("{\"id\":\"").Append(EscapeJson(id ?? "")).Append("\"");
            sb.Append(",\"ok\":true,\"data\":\"\",\"file\":\"");
            sb.Append(EscapeJson(filePath)).Append("\"}");
            return sb.ToString();
        }

        internal static string FormatFileResponseWithData(string id, string filePath, string data)
        {
            var sb = new StringBuilder();
            sb.Append("{\"id\":\"").Append(EscapeJson(id ?? "")).Append("\"");
            sb.Append(",\"ok\":true,\"data\":\"").Append(EscapeJson(data ?? "")).Append("\"");
            sb.Append(",\"file\":\"").Append(EscapeJson(filePath)).Append("\"}");
            return sb.ToString();
        }

        internal static string FormatBusyResponse(string id, string message, int retryMs)
        {
            return $"{{\"id\":\"{EscapeJson(id)}\",\"ok\":false,\"err\":\"{EscapeJson(message)}\",\"retry\":{retryMs}}}";
        }

        /// <summary>Extract the first balanced JSON object from an array string like [{"a":1},{"b":2}].</summary>
        internal static string ExtractFirstArrayObject(string arrayJson)
        {
            if (string.IsNullOrEmpty(arrayJson)) return null;
            var start = arrayJson.IndexOf('{');
            if (start == -1) return null;
            int depth = 0; bool inStr = false, esc = false;
            for (int i = start; i < arrayJson.Length; i++)
            {
                char c = arrayJson[i];
                if (esc) { esc = false; continue; }
                if (inStr) { if (c == '\\') esc = true; else if (c == '"') inStr = false; continue; }
                if (c == '"')  { inStr = true; continue; }
                if (c == '{')  { depth++; continue; }
                if (c == '}') { depth--; if (depth == 0) return arrayJson.Substring(start, i - start + 1); }
            }
            return null;
        }
    }
}
