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
        public ClaudeBackendConfig    Claude       = new ClaudeBackendConfig();
        public CodexBackendConfig     Codex        = new CodexBackendConfig();
        public AntigravityBackendConfig Antigravity  = new AntigravityBackendConfig();
        public KimiBackendConfig      Kimi         = new KimiBackendConfig();
        public OpenCodeBackendConfig  OpenCode     = new OpenCodeBackendConfig();
        public ChipConfig             Chips        = new ChipConfig();
        public ModelPresetsConfig     ModelPresets = new ModelPresetsConfig();
        public int                    InactivityTimeoutSec = 180;

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
                store.Antigravity   = store.Antigravity   ?? new AntigravityBackendConfig();
                store.Kimi          = store.Kimi          ?? new KimiBackendConfig();
                store.OpenCode      = store.OpenCode      ?? new OpenCodeBackendConfig();
                store.Chips         = store.Chips         ?? new ChipConfig();
                store.ModelPresets  = store.ModelPresets  ?? new ModelPresetsConfig();
                MigrateKimiModel(store);
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

        private static void MigrateKimiModel(BackendConfigStore store)
        {
            if (store.Kimi == null) return;
            switch (store.Kimi.Model)
            {
                case "kimi-k2.7-code":
                case "kimi-k2.7-code-highspeed":
                    store.Kimi.Model = "kimi-for-coding"; break;
                case "kimi-k2.6":
                    store.Kimi.Model = "k2p6"; break;
                case "kimi-k2.5":
                    store.Kimi.Model = "k2p5"; break;
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
