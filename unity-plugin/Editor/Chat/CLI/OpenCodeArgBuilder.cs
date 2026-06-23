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
        /// <param name="userConfigPath">
        /// Path to global ~/.opencode/config.json; null = default location.
        /// External (non-unity) MCP entries are merged in. Unity entries are stripped.
        /// Injectable for tests.
        /// </param>
        public static string WriteConfig(string configDir, int port = 9500,
            string userConfigPath = null)
        {
            if (!Directory.Exists(configDir))
                Directory.CreateDirectory(configDir);

            var globalPath = userConfigPath ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".opencode", "config.json");

            var path    = Path.Combine(configDir, ConfigFileName(port));
            var content = BuildConfigBlock(port);
            content = MergeGlobalOpenCodeConfig(content, globalPath);
            File.WriteAllText(path, content, new UTF8Encoding(false));
            return path;
        }

        /// <summary>Returns env dict to merge into spawn env.
        /// Only OPENCODE_CONFIG is needed — UNITY_MCP_PORT is already in the JSON config
        /// and injecting it here would violate the CliBackendBase rule (no port in process env).
        /// </summary>
        public static Dictionary<string, string> BuildEnv(string configDir, int port = 9500)
        {
            var configPath = Path.Combine(configDir ?? Path.GetTempPath(), ConfigFileName(port));
            return new Dictionary<string, string>
            {
                { "OPENCODE_CONFIG", configPath },
            };
        }

        // ── Unity-key filter ─────────────────────────────────────────────────

        private static readonly string[] _unityKeys = { "unity", "unity-mcp", "unity-kiss-mcp" };

        private static bool IsNonUnityKey(string key) =>
            !Array.Exists(_unityKeys, k => key.Equals(k, StringComparison.OrdinalIgnoreCase));

        // ── Merge logic ───────────────────────────────────────────────────────

        /// <summary>
        /// Merges external (non-unity) MCP server entries from the user's global opencode config
        /// into the base config's "mcp" block. Gracefully returns baseConfig on
        /// missing/empty/corrupt/non-object-value input. Non-object entries (arrays, strings)
        /// in the user "mcp" block are skipped — MCP servers are always objects by spec.
        /// </summary>
        internal static string MergeGlobalOpenCodeConfig(string baseConfig, string userConfigPath)
        {
            if (!File.Exists(userConfigPath)) return baseConfig;

            string userJson;
            try { userJson = File.ReadAllText(userConfigPath, Encoding.UTF8); }
            catch { return baseConfig; }

            if (string.IsNullOrWhiteSpace(userJson)) return baseConfig;

            // Locate "mcp" block in user config — matches FIRST "mcp" key, assumes top-level placement.
            var mcpKey = "\"mcp\"";
            var mcpIdx = userJson.IndexOf(mcpKey, StringComparison.Ordinal);
            if (mcpIdx < 0) return baseConfig;

            var mcpBraceStart = userJson.IndexOf('{', mcpIdx + mcpKey.Length);
            if (mcpBraceStart < 0) return baseConfig;

            // Walk brace-depth to extract content between "mcp": { ... }
            int depth = 1, pos = mcpBraceStart + 1;
            while (pos < userJson.Length && depth > 0)
            {
                if (userJson[pos] == '{') depth++;
                else if (userJson[pos] == '}') depth--;
                pos++;
            }
            var mcpBlockContent = userJson.Substring(mcpBraceStart + 1, pos - mcpBraceStart - 2);

            // Extract object-valued entries whose keys are not unity-related
            var extra = JsonMergeHelper.ExtractObjectEntries(mcpBlockContent, IsNonUnityKey);

            // Inject into base config's "mcp" block using the shared primitive.
            // FindBlockClose matches FIRST "mcp" key in baseConfig — top-level placement assumed.
            return JsonMergeHelper.InjectBeforeBlockClose(baseConfig, "mcp", extra);
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
                commandArray = JsonHelper.BuildJsonStringArray(PrependCommand(cmd, cmdArgs));
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
