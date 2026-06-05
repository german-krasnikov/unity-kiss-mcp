// Persistence layer for per-backend CLI config (Load/Save to Library/MCP_ChatBackendConfig.json).
// Split from BackendConfig.cs to keep files under 200 lines.
using System;
using System.IO;
using UnityEngine;

namespace UnityMCP.Editor.Chat
{
    [Serializable]
    internal sealed class BackendConfigStore
    {
        public ClaudeBackendConfig Claude = new ClaudeBackendConfig();
        public CodexBackendConfig  Codex  = new CodexBackendConfig();
        public ChipConfig          Chips  = new ChipConfig();

        private static string DefaultPath =>
            Path.Combine(Application.dataPath, "..", "Library", "MCP_ChatBackendConfig.json");

        internal static BackendConfigStore Load(string path = null)
        {
            path = path ?? DefaultPath;
            if (!File.Exists(path))
                return new BackendConfigStore();
            try
            {
                var json  = File.ReadAllText(path);
                var store = JsonUtility.FromJson<BackendConfigStore>(json);
                store.Claude = store.Claude ?? new ClaudeBackendConfig();
                store.Codex  = store.Codex  ?? new CodexBackendConfig();
                store.Chips  = store.Chips  ?? new ChipConfig();
                return store;
            }
            catch
            {
                return new BackendConfigStore();
            }
        }

        internal void Save(string path = null)
        {
            path = path ?? DefaultPath;
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            Chips?.FlushToArrays(); // P4: persist override cache to arrays before serializing
            File.WriteAllText(path, JsonUtility.ToJson(this, prettyPrint: true));
        }
    }
}
