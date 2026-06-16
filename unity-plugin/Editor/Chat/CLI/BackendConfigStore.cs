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
        public ClaudeBackendConfig  Claude       = new ClaudeBackendConfig();
        public CodexBackendConfig   Codex        = new CodexBackendConfig();
        public GeminiBackendConfig  Gemini       = new GeminiBackendConfig();
        public ChipConfig           Chips        = new ChipConfig();
        public ModelPresetsConfig   ModelPresets = new ModelPresetsConfig();

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
                store.Claude        = store.Claude        ?? new ClaudeBackendConfig();
                store.Codex         = store.Codex         ?? new CodexBackendConfig();
                store.Gemini        = store.Gemini        ?? new GeminiBackendConfig();
                store.Chips         = store.Chips         ?? new ChipConfig();
                store.ModelPresets  = store.ModelPresets  ?? new ModelPresetsConfig();
                return store;
            }
            catch
            {
                return new BackendConfigStore();
            }
        }

        internal (string label, string modelId)[] GetPresetsForKind(BackendKind kind)
        {
            var entries = ModelPresets?.For(kind);
            if (entries == null || entries.Length == 0)
                return ModelPresetDefaults.For(kind);

            var result = new (string, string)[entries.Length + 2];
            result[0] = ("Default", "");
            for (int i = 0; i < entries.Length; i++)
                result[i + 1] = (entries[i].label, entries[i].modelId);
            result[result.Length - 1] = ("Custom...", ModelPresetDefaults.CustomSentinel);
            return result;
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
