// Balanced-brace scanner for walking all objects in a JSON array.
// Pure: zero UnityEngine deps, fully NUnit-testable.
namespace UnityMCP.Editor.Chat
{
    internal static class JsonArrayScan
    {
        /// <summary>
        /// Advances <paramref name="pos"/> past the next balanced JSON object
        /// inside an array string. Returns the extracted object, or null when done.
        /// Call repeatedly until null to iterate all objects.
        /// </summary>
        internal static string ExtractNextObject(string arrayJson, ref int pos)
        {
            if (string.IsNullOrEmpty(arrayJson)) return null;

            // Scan forward from pos to find the next '{'.
            while (pos < arrayJson.Length && arrayJson[pos] != '{') pos++;
            if (pos >= arrayJson.Length) return null;

            int start = pos;
            int depth = 0;
            bool inStr = false, esc = false;

            for (; pos < arrayJson.Length; pos++)
            {
                char c = arrayJson[pos];
                if (esc)  { esc = false; continue; }
                if (inStr)
                {
                    if (c == '\\') esc = true;
                    else if (c == '"') inStr = false;
                    continue;
                }
                if (c == '"')  { inStr = true; continue; }
                if (c == '{')  { depth++; continue; }
                if (c == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        pos++;  // advance past the closing brace
                        return arrayJson.Substring(start, pos - start);
                    }
                }
            }
            return null;
        }
    }
}
