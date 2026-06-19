// Writes/merges unity-mcp entry into external AI tool config files.
using System;
using System.IO;
using System.Text;

namespace UnityMCP.Editor.Wizard
{
    internal static class WizardConfigWriter
    {
        internal static void Write(string toolName, string configPath, int port)
        {
            try
            {
                var dir = Path.GetDirectoryName(configPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                string merged;
                if (File.Exists(configPath))
                {
                    File.Copy(configPath, configPath + ".bak", overwrite: true);
                    var existing = File.ReadAllText(configPath, Encoding.UTF8);
                    merged = Merge(existing, port);
                }
                else
                {
                    merged = Fresh(port);
                }

                File.WriteAllText(configPath, merged, new UTF8Encoding(false));
                UnityEditor.EditorUtility.DisplayDialog(
                    $"{toolName} — Config Written",
                    $"unity-mcp added to:\n{configPath}", "OK");
            }
            catch (Exception ex)
            {
                UnityEditor.EditorUtility.DisplayDialog(
                    "Write Failed", $"Could not write config:\n{ex.Message}", "OK");
            }
        }

        internal static string Fresh(int port) =>
            "{\n" +
            "  \"mcpServers\": {\n" +
            "    " + Entry(port) + "\n" +
            "  }\n" +
            "}\n";

        internal static string Merge(string existing, int port)
        {
            var entry = Entry(port);

            if (existing.Contains("\"unity-mcp\""))
            {
                var freshValue = entry.Substring(entry.IndexOf('{'));
                return ReplaceEntry(existing, "unity-mcp", freshValue) ?? existing;
            }

            if (existing.Contains("\"mcpServers\""))
            {
                var idx      = existing.IndexOf("\"mcpServers\"", StringComparison.Ordinal);
                var braceIdx = existing.IndexOf('{', idx + "\"mcpServers\"".Length);
                if (braceIdx < 0) return Fresh(port);
                var after = existing.Substring(braceIdx + 1).TrimStart();
                var sep   = after.StartsWith("}") ? "" : ",";
                return existing.Substring(0, braceIdx + 1)
                     + "\n    " + entry + sep
                     + existing.Substring(braceIdx + 1);
            }

            var lastBrace = existing.LastIndexOf('}');
            if (lastBrace >= 0)
            {
                var comma = existing.Substring(0, lastBrace).TrimEnd().EndsWith("{") ? "" : ",";
                return existing.Substring(0, lastBrace)
                     + comma
                     + "\n  \"mcpServers\": {\n    " + entry + "\n  }\n}";
            }

            return Fresh(port);
        }

        private static string Entry(int port) =>
            "\"unity-mcp\": {\n" +
            "      \"command\": \"uvx\",\n" +
            "      \"args\": [\"unity-mcp\"],\n" +
            $"      \"env\": {{ \"UNITY_MCP_PORT\": \"{port}\" }}\n" +
            "    }";

        private static string ReplaceEntry(string json, string key, string newValue)
        {
            if (string.IsNullOrEmpty(json)) return null;
            var keyIdx = json.IndexOf("\"" + key + "\"", StringComparison.Ordinal);
            if (keyIdx < 0) return null;
            var braceStart = json.IndexOf('{', keyIdx + key.Length + 2);
            if (braceStart < 0) return null;
            int depth = 1, pos = braceStart + 1;
            while (pos < json.Length && depth > 0)
            {
                if (json[pos] == '{') depth++;
                else if (json[pos] == '}') depth--;
                pos++;
            }
            return json.Substring(0, braceStart) + newValue + json.Substring(pos);
        }
    }
}
