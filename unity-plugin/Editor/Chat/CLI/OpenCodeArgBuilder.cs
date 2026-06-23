// Build opencode CLI argv. Pure static — no process spawning, fully NUnit-testable.
// MCP config written to configDir/opencode-unity-mcp.json, injected via OPENCODE_CONFIG env var.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace UnityMCP.Editor.Chat
{
    public static class OpenCodeArgBuilder
    {
        /// <summary>Per-port config filename. Port=0 falls back to legacy bare name.</summary>
        internal static string ConfigFileName(int port) =>
            port > 0 ? $"opencode-unity-mcp-{port}.json" : "opencode-unity-mcp.json";

        /// <summary>
        /// Build opencode exec argv and env keys to strip.
        /// Writes opencode-unity-mcp.json for OPENCODE_CONFIG injection.
        /// </summary>
        public static (string[] args, string[] stripEnvKeys) Build(
            string prompt,
            string model           = null,
            bool   skipPermissions = true,
            string extraArgs       = null,
            string configDir       = null,  // injectable for tests
            int    port            = 0,
            string resumeId        = null)
        {
#if UNITY_EDITOR
            if (port == 0) port = MCPServer.ServerChatPort;
#endif
            if (port == 0) port = 9500;

            var dir = configDir ?? Path.GetTempPath();
            WriteConfig(dir, port);

            var args = new List<string> { "run", "--format", "json" };

            if (skipPermissions)
                args.Add("--dangerously-skip-permissions");

            if (!string.IsNullOrEmpty(model))
            {
                args.Add("--model");
                args.Add(model);
            }

            if (!string.IsNullOrEmpty(resumeId))
            {
                args.Add("-s");
                args.Add(resumeId);
            }

            if (!string.IsNullOrEmpty(extraArgs))
                foreach (var token in ArgTokenizer.Split(extraArgs))
                    args.Add(token);

            args.Add(prompt);

            return (args.ToArray(), new string[0]);
        }

        /// <summary>Writes opencode MCP config JSON and returns the absolute path.</summary>
        public static string WriteConfig(string configDir, int port = 9500)
        {
            if (!Directory.Exists(configDir))
                Directory.CreateDirectory(configDir);

            var path    = Path.Combine(configDir, ConfigFileName(port));
            var content = BuildConfigBlock(port);
            File.WriteAllText(path, content, new UTF8Encoding(false));
            return path;
        }

        /// <summary>Returns env dict to merge into spawn env.</summary>
        public static Dictionary<string, string> BuildEnv(string configDir, int port = 9500)
        {
            var configPath = Path.Combine(configDir ?? Path.GetTempPath(), ConfigFileName(port));
            return new Dictionary<string, string>
            {
                { "OPENCODE_CONFIG", configPath },
                { "UNITY_MCP_PORT",  port.ToString() },
            };
        }

        // ── Config JSON ───────────────────────────────────────────────────────

        internal static string BuildConfigBlock(int port = 9500)
        {
            var packageRoot = Path.GetFullPath("Packages/com.unity-mcp.editor");
            var serverDir   = ChatMcpConfigWriter.ResolveServerDir(packageRoot);
            string commandArray;

            if (serverDir != null)
            {
                var (cmd, cmdArgs) = ChatMcpConfigWriter.ResolvePythonCommand(serverDir, null);
                commandArray = JsonHelper.BuildJsonStringArray(
                    PrependCommand(cmd, cmdArgs));
            }
            else
            {
#if UNITY_EDITOR
                var isMac = UnityEngine.SystemInfo.operatingSystemFamily != UnityEngine.OperatingSystemFamily.Windows;
#else
                var isMac = true;
#endif
                var python = isMac ? "python3" : "python";
                commandArray = JsonHelper.BuildJsonStringArray(new[] { python, "-m", "unity_mcp.server" });
            }

            return
                "{\n" +
                "  \"mcp\": {\n" +
                "    \"unity-mcp\": {\n" +
                "      \"type\": \"local\",\n" +
                $"      \"command\": {commandArray},\n" +
                $"      \"environment\": {{ \"UNITY_MCP_PORT\": \"{port}\" }},\n" +
                "      \"enabled\": true\n" +
                "    }\n" +
                "  }\n" +
                "}\n";
        }

        private static string[] PrependCommand(string cmd, string[] args)
        {
            if (args == null || args.Length == 0) return new[] { cmd };
            var result = new string[args.Length + 1];
            result[0] = cmd;
            Array.Copy(args, 0, result, 1, args.Length);
            return result;
        }

    }
}
