// Build kimi CLI argv. Pure static — no process spawning, fully NUnit-testable.
// MCP config written to ~/.kimi-code/mcp.json (read automatically by kimi).
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace UnityMCP.Editor.Chat
{
    public static class KimiArgBuilder
    {
        /// <summary>
        /// Build kimi exec argv and env keys to strip.
        /// Writes ~/.kimi-code/mcp.json with MCP config before spawn.
        /// </summary>
        public static (string[] args, string[] stripEnvKeys) Build(
            string prompt,
            string model        = null,
            string approvalMode = null,
            string extraArgs    = null,
            string mcpConfigDir = null,  // injectable for tests
            int    port         = 0)
        {
            var dir = mcpConfigDir ?? DefaultKimiDir();
#if UNITY_EDITOR
            if (port == 0) port = MCPServer.ServerChatPort;
#endif
            if (port == 0) port = 9500; // fallback for test context

            WriteModelsConfig(dir);
            WriteMcpConfig(dir, port);

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

            // --yolo/--plan incompatible with -p mode

            if (!string.IsNullOrEmpty(extraArgs))
                foreach (var token in ArgTokenizer.Split(extraArgs))
                    args.Add(token);

            return (args.ToArray(), new string[0]); // Kimi uses OAuth — nothing to strip
        }

        // ── Models config ─────────────────────────────────────────────────────

        private static readonly (string alias, string apiModel)[] KnownModels = new[]
        {
            ("kimi-for-coding", "kimi-for-coding"),
            ("k2p6",            "k2p6"),
            ("k2p5",            "k2p5"),
        };

        internal static void WriteModelsConfig(string dir)
        {
            if (string.IsNullOrEmpty(dir)) return;
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var path    = Path.Combine(dir, "config.toml");
            var content = File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8) : "";
            var sb      = new StringBuilder();

            foreach (var (alias, apiModel) in KnownModels)
            {
                // Accept both short and full-path variants
                if (content.Contains($"[models.\"{alias}\"]") ||
                    content.Contains($"[models.\"kimi-code/{alias}\"]"))
                    continue;

                sb.AppendLine();
                sb.AppendLine($"[models.\"{alias}\"]");
                sb.AppendLine($"provider = \"managed:kimi-code\"");
                sb.AppendLine($"model = \"{apiModel}\"");
                sb.AppendLine($"max_context_size = 262144");
                sb.AppendLine($"capabilities = [ \"thinking\", \"always_thinking\", \"image_in\", \"video_in\", \"tool_use\" ]");
            }

            if (sb.Length == 0) return; // nothing new to write

            if (content.Length > 0 && !content.EndsWith("\n"))
                content += "\n";

            File.WriteAllText(path, content + sb.ToString(), new UTF8Encoding(false));
        }

        // ── MCP config ────────────────────────────────────────────────────────

        internal static void WriteMcpConfig(string dir, int port = 9500)
        {
            if (string.IsNullOrEmpty(dir)) return;
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var path = Path.Combine(dir, "mcp.json");

            if (!File.Exists(path))
            {
                File.WriteAllText(path, BuildMcpBlock(port), new UTF8Encoding(false));
                return;
            }

            var existing = File.ReadAllText(path, Encoding.UTF8);

            // Port-skip-if-unchanged: avoid unnecessary FS writes.
            if (existing.Contains($"\"UNITY_MCP_PORT\": \"{port}\"")
                || existing.Contains($"\"UNITY_MCP_PORT\":\"{port}\""))
                return;

            // Merge: update only the unity-mcp entry, preserve all other servers.
            string result;
            if (existing.Contains("\"unity-mcp\""))
            {
                result = ReplaceUnityMcpEntry(existing, port);
            }
            else if (existing.Contains("\"mcpServers\""))
            {
                // Inject unity-mcp into existing mcpServers block.
                var insertTarget = "\"mcpServers\"";
                var idx = existing.IndexOf(insertTarget, StringComparison.Ordinal);
                var braceIdx = existing.IndexOf('{', idx + insertTarget.Length);
                if (braceIdx >= 0)
                {
                    var unityEntry = BuildUnityMcpEntry(port);
                    var afterBrace = existing.Substring(braceIdx + 1).TrimStart();
                    var sep = afterBrace.StartsWith("}") ? "" : ",";
                    result = existing.Substring(0, braceIdx + 1)
                           + "\n    " + unityEntry + sep
                           + existing.Substring(braceIdx + 1);
                }
                else
                {
                    result = BuildMcpBlock(port);
                }
            }
            else
            {
                // No mcpServers at all: merge at root level.
                var lastBrace = existing.LastIndexOf('}');
                if (lastBrace >= 0)
                {
                    var comma = existing.Substring(0, lastBrace).TrimEnd().EndsWith("{") ? "" : ",";
                    result = existing.Substring(0, lastBrace)
                           + comma
                           + "\n  \"mcpServers\": {\n    " + BuildUnityMcpEntry(port) + "\n  }\n}";
                }
                else
                {
                    result = BuildMcpBlock(port);
                }
            }

            File.WriteAllText(path, result, new UTF8Encoding(false));
        }

        /// <summary>
        /// Replace only the "unity-mcp" entry value inside mcpServers, preserving all other entries.
        /// </summary>
        private static string ReplaceUnityMcpEntry(string existing, int port)
        {
            return JsonMergeHelper.ReplaceEntry(existing, "unity-mcp", BuildUnityMcpEntryValue(port)) ?? existing;
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
            return "\"unity-mcp\": " + BuildUnityMcpEntryValue(port);
        }

        private static string BuildUnityMcpEntryValue(int port)
        {
            var packageRoot = Path.GetFullPath("Packages/com.unity-mcp.editor");
            var serverDir   = ChatMcpConfigWriter.ResolveServerDir(packageRoot);
            string command, argsJson;

            if (serverDir != null)
            {
                var (cmd, cmdArgs) = ChatMcpConfigWriter.ResolvePythonCommand(serverDir, null);
                command  = JsonHelper.EscapeJson(cmd);
                argsJson = JsonHelper.BuildJsonStringArray(cmdArgs);
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
                "{\n" +
                $"      \"command\": \"{command}\",\n" +
                $"      \"args\": {argsJson},\n" +
                $"      \"env\": {{ \"UNITY_MCP_PORT\": \"{port}\" }}\n" +
                "    }";
        }

        private static string DefaultKimiDir()
            => Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".kimi-code");
    }
}
