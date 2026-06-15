// Build gemini CLI argv. Pure static — no process spawning, fully NUnit-testable.
// MCP config is passed via .gemini/settings.json (no --mcp-config flag in Gemini CLI).
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace UnityMCP.Editor.Chat
{
    public static class GeminiArgBuilder
    {
        private static readonly string[] StripKeys = { "GEMINI_API_KEY" };

        /// <summary>
        /// Build gemini exec argv and env keys to strip.
        /// Writes .gemini/settings.json with MCP config before spawn.
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
            // Write MCP config to .gemini/settings.json
            var dir = settingsDir ?? DefaultGeminiDir();
#if UNITY_EDITOR
            if (port == 0) port = MCPServer.ServerChatPort;
#endif
            if (port == 0) port = 9500; // fallback for test context
            WriteMcpSettings(dir, port);

            var args = new List<string>
            {
                "-p", prompt,
                "--output-format", "stream-json",
            };

            if (!string.IsNullOrEmpty(model))
            {
                args.Add("--model");
                args.Add(model);
            }

            if (string.Equals(approvalMode, "yolo", StringComparison.OrdinalIgnoreCase))
                args.Add("--yolo");

            if (sandbox)
                args.Add("--sandbox");

            if (!string.IsNullOrEmpty(extraArgs))
                foreach (var token in ArgTokenizer.Split(extraArgs))
                    args.Add(token);

            return (args.ToArray(), StripKeys);
        }

        // ── MCP settings ──────────────────────────────────────────────────────

        /// <summary>
        /// Writes .gemini/settings.json with the MCP unity-mcp server config.
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

            // Already configured — always rewrite to guarantee fresh port.
            // JsonHelper can't reach nested "env" at depth 3, and string-surgery
            // is fragile. Full rewrite is safe: we preserve non-MCP settings below.
            if (existing.Contains("\"unity-mcp\""))
            {
                if (existing.Contains($"\"UNITY_MCP_PORT\": \"{port}\"")
                    || existing.Contains($"\"UNITY_MCP_PORT\":\"{port}\""))
                    return; // port already correct — skip IO
                // Rebuild entire file preserving non-mcpServers keys
                var fresh = RewriteWithFreshMcp(existing, port);
                File.WriteAllText(path, fresh, new UTF8Encoding(false));
                return;
            }

            // File exists but lacks unity-mcp: inject into existing mcpServers or create one.
            string merged;
            if (existing.Contains("\"mcpServers\""))
            {
                // Insert unity-mcp entry after the opening brace of mcpServers value.
                var insertTarget = "\"mcpServers\"";
                var idx = existing.IndexOf(insertTarget, StringComparison.Ordinal);
                var braceIdx = existing.IndexOf('{', idx + insertTarget.Length);
                if (braceIdx >= 0)
                {
                    var unityEntry = BuildUnityMcpEntry(port);
                    // Peek whether there are already entries (non-empty object).
                    var afterBrace = existing.Substring(braceIdx + 1).TrimStart();
                    var sep = afterBrace.StartsWith("}") ? "" : ",";
                    merged = existing.Substring(0, braceIdx + 1)
                           + "\n    " + unityEntry + sep
                           + existing.Substring(braceIdx + 1);
                }
                else
                {
                    // Malformed — overwrite with fresh block.
                    merged = mcpBlock;
                }
            }
            else
            {
                // No mcpServers at all in an existing file: merge at root level.
                // Strip closing brace, append mcpServers key.
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

        /// <summary>
        /// Rebuild settings.json keeping non-mcpServers content, replacing mcpServers entirely.
        /// </summary>
        private static string RewriteWithFreshMcp(string existing, int port)
        {
            var mcpIdx = existing.IndexOf("\"mcpServers\"", StringComparison.Ordinal);
            if (mcpIdx < 0) return BuildMcpBlock(port); // shouldn't happen

            // Find the opening '{' of mcpServers value
            var braceStart = existing.IndexOf('{', mcpIdx + 12);
            if (braceStart < 0) return BuildMcpBlock(port);

            // Find matching closing '}' by counting depth
            int depth = 1, braceEnd = braceStart + 1;
            while (braceEnd < existing.Length && depth > 0)
            {
                if (existing[braceEnd] == '{') depth++;
                else if (existing[braceEnd] == '}') depth--;
                braceEnd++;
            }

            // Replace mcpServers value with fresh entry
            var before = existing.Substring(0, braceStart + 1);
            var after = existing.Substring(braceEnd - 1); // includes closing '}'
            return before + "\n    " + BuildUnityMcpEntry(port) + "\n  " + after;
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
