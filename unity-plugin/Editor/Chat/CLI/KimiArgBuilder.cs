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

            // Port-skip-if-unchanged optimization: avoid unnecessary FS writes.
            if (File.Exists(path))
            {
                var existing = File.ReadAllText(path, Encoding.UTF8);
                if (existing.Contains($"\"UNITY_MCP_PORT\": \"{port}\"")
                    || existing.Contains($"\"UNITY_MCP_PORT\":\"{port}\""))
                    return; // already correct
            }

            File.WriteAllText(path, BuildMcpBlock(port), new UTF8Encoding(false));
        }

        internal static string BuildMcpBlock(int port = 9500)
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
                "  \"mcpServers\": {\n" +
                "    \"unity-mcp\": {\n" +
                $"      \"command\": \"{command}\",\n" +
                $"      \"args\": {argsJson},\n" +
                $"      \"env\": {{ \"UNITY_MCP_PORT\": \"{port}\" }}\n" +
                "    }\n" +
                "  }\n" +
                "}\n";
        }

        private static string DefaultKimiDir()
            => Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".kimi-code");
    }
}
