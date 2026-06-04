// Per-backend CLI config persisted to Library/MCP_ChatBackendConfig.json (gitignored).
// JsonUtility requires [Serializable] + public fields.
using System;
using System.IO;
using UnityEngine;

namespace UnityMCP.Editor.Chat
{
    [Serializable]
    internal sealed class ClaudeBackendConfig
    {
        public string PermissionMode = "plan";   // "plan" | "acceptEdits"
        public string Model          = "";        // empty = default
        public string ExtraArgs      = "";        // whitespace-split appended to argv
    }

    [Serializable]
    internal sealed class CodexBackendConfig
    {
        public string Model             = "";
        public string PermissionMode    = "danger-full-access";
        public int    StartupTimeoutSec = 30;
        public string ExtraArgs         = "";
    }

    [Serializable]
    internal sealed class BackendConfigStore
    {
        public ClaudeBackendConfig Claude = new ClaudeBackendConfig();
        public CodexBackendConfig  Codex  = new CodexBackendConfig();

        private static string DefaultPath =>
            Path.Combine(Application.dataPath, "..", "Library", "MCP_ChatBackendConfig.json");

        /// <summary>
        /// Load from path. Returns default instance when file absent or corrupt.
        /// Optional path param for testability (avoids touching real Library/).
        /// </summary>
        internal static BackendConfigStore Load(string path = null)
        {
            path = path ?? DefaultPath;
            if (!File.Exists(path))
                return new BackendConfigStore();
            try
            {
                var json  = File.ReadAllText(path);
                var store = JsonUtility.FromJson<BackendConfigStore>(json);
                // Null guard: JsonUtility can leave nested objects null if fields missing.
                store.Claude = store.Claude ?? new ClaudeBackendConfig();
                store.Codex  = store.Codex  ?? new CodexBackendConfig();
                return store;
            }
            catch
            {
                return new BackendConfigStore();
            }
        }

        /// <summary>
        /// Persist to path. Optional path param for testability.
        /// </summary>
        internal void Save(string path = null)
        {
            path = path ?? DefaultPath;
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(path, JsonUtility.ToJson(this, prettyPrint: true));
        }
    }
}
