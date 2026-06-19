// Build agy (Antigravity CLI) argv. Pure static — no process spawning, fully NUnit-testable.
// MCP config is passed via ~/.gemini/settings.json (same as old Gemini CLI).
// agy outputs plain text — no --output-format flag needed.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace UnityMCP.Editor.Chat
{
    public static class AgyArgBuilder
    {
        private static readonly string[] StripKeys = { "GEMINI_API_KEY" };

        /// <summary>
        /// Build agy exec argv and env keys to strip.
        /// Writes ~/.gemini/settings.json with MCP config before spawn.
        /// </summary>
        public static (string[] args, string[] stripEnvKeys) Build(
            string prompt,
            string model         = null,
            string approvalMode  = null,
            bool   sandbox       = false,
            string extraArgs     = null,
            string settingsDir   = null,  // injectable for tests (avoids real FS writes)
            int    port          = 0)     // 0 = read from MCPServer.ServerChatPort
        {
            var dir = settingsDir ?? DefaultGeminiDir();
#if UNITY_EDITOR
            if (port == 0) port = MCPServer.ServerChatPort;
#endif
            if (port == 0) port = 9500; // fallback for test context
            WriteMcpSettings(dir, port);

            // agy does NOT support --output-format; outputs plain text
            var args = new List<string> { "-p", prompt };

            if (!string.IsNullOrEmpty(model))
            {
                args.Add("--model");
                args.Add(model);
            }

            if (string.Equals(approvalMode, "yolo", StringComparison.OrdinalIgnoreCase))
                args.Add("--dangerously-skip-permissions");

            if (sandbox)
                args.Add("--sandbox");

            if (!string.IsNullOrEmpty(extraArgs))
                foreach (var token in ArgTokenizer.Split(extraArgs))
                    args.Add(token);

            return (args.ToArray(), StripKeys);
        }

        // ── MCP settings ──────────────────────────────────────────────────────

        /// <summary>
        /// Writes ~/.gemini/settings.json with the MCP unity-mcp server config.
        /// If the file already exists, injects unity-mcp into the existing mcpServers block
        /// to avoid clobbering other user settings.
        /// </summary>
        internal static void WriteMcpSettings(string geminiDir, int port = 9500)
        {
            if (string.IsNullOrEmpty(geminiDir)) return;
            if (!Directory.Exists(geminiDir))
                Directory.CreateDirectory(geminiDir);

            var path = Path.Combine(geminiDir, "settings.json");
            var mcpBlock = BuildMcpBlock(port);

            if (!File.Exists(path))
            {
                File.WriteAllText(path, mcpBlock, new UTF8Encoding(false));
                return;
            }

            var existing = File.ReadAllText(path, Encoding.UTF8);

            if (existing.Contains("\"unity-mcp\""))
            {
                if (existing.Contains($"\"UNITY_MCP_PORT\": \"{port}\"")
                    || existing.Contains($"\"UNITY_MCP_PORT\":\"{port}\""))
                    return; // port already correct — skip IO
                var fresh = RewriteWithFreshMcp(existing, port);
                File.WriteAllText(path, fresh, new UTF8Encoding(false));
                return;
            }

            string merged;
            if (existing.Contains("\"mcpServers\""))
            {
                var insertTarget = "\"mcpServers\"";
                var idx = existing.IndexOf(insertTarget, StringComparison.Ordinal);
                var braceIdx = existing.IndexOf('{', idx + insertTarget.Length);
                if (braceIdx >= 0)
                {
                    var unityEntry = BuildUnityMcpEntry(port);
                    var afterBrace = existing.Substring(braceIdx + 1).TrimStart();
                    var sep = afterBrace.StartsWith("}") ? "" : ",";
                    merged = existing.Substring(0, braceIdx + 1)
                           + "\n    " + unityEntry + sep
                           + existing.Substring(braceIdx + 1);
                }
                else
                {
                    merged = mcpBlock;
                }
            }
            else
            {
                var lastBrace = existing.LastIndexOf('}');
                if (lastBrace >= 0)
                {
                    var comma = existing.Substring(0, lastBrace).TrimEnd().EndsWith("{") ? "" : ",";
                    merged = existing.Substring(0, lastBrace)
                           + comma
                           + "\n  \"mcpServers\": {\n    " + BuildUnityMcpEntry(port) + "\n  }\n}";
                }
                else
                {
                    merged = mcpBlock;
                }
            }

            File.WriteAllText(path, merged, new UTF8Encoding(false));
        }

        internal static string BuildMcpBlock(int port = 9500)
        {
            return
                "{\n" +
                "  \"mcpServers\": {\n" +
                "    " + BuildUnityMcpEntry(port) + "\n" +
                "  }\n" +
                "}\n";
        }

        private static string BuildUnityMcpEntry(int port)
        {
            var packageRoot = Path.GetFullPath("Packages/com.unity-mcp.editor");
            var serverDir   = ChatMcpConfigWriter.ResolveServerDir(packageRoot);
            string command, argsJson;

            if (serverDir != null)
            {
                var (cmd, cmdArgs) = ChatMcpConfigWriter.ResolvePythonCommand(serverDir, null);
                command  = JsonHelper.EscapeJson(cmd);
                argsJson = BuildJsonStringArray(cmdArgs);
            }
            else
            {
#if UNITY_EDITOR
                var isMac = UnityEngine.SystemInfo.operatingSystemFamily != UnityEngine.OperatingSystemFamily.Windows;
#else
                var isMac = true;
#endif
                command  = isMac ? "python3" : "python";
                argsJson = "[\"-m\",\"unity_mcp.server\"]";
            }

            return
                "\"unity-mcp\": {\n" +
                $"      \"command\": \"{command}\",\n" +
                $"      \"args\": {argsJson},\n" +
                $"      \"env\": {{ \"UNITY_MCP_PORT\": \"{port}\" }},\n" +
                "      \"trust\": true\n" +
                "    }";
        }

        private static string RewriteWithFreshMcp(string existing, int port)
        {
            var fullEntry = BuildUnityMcpEntry(port);
            var valueStart = fullEntry.IndexOf('{');
            var freshValue = fullEntry.Substring(valueStart);
            return JsonMergeHelper.ReplaceEntry(existing, "unity-mcp", freshValue) ?? existing;
        }

        private static string BuildJsonStringArray(string[] items)
        {
            if (items == null || items.Length == 0) return "[]";
            var sb = new StringBuilder("[");
            for (int i = 0; i < items.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append('"');
                sb.Append(JsonHelper.EscapeJson(items[i]));
                sb.Append('"');
            }
            sb.Append(']');
            return sb.ToString();
        }

        private static string DefaultGeminiDir()
            => Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".gemini");
    }
}
