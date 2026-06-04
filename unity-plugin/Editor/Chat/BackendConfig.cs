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

    /// <summary>Per-kind detail depth for chip context sent to AI.</summary>
    [Serializable]
    internal sealed class ChipConfig
    {
        // "path" = bracket format only. "summary" = SelectionSummary.
        // "full" = ComponentSerializer dump (hierarchy only). "none" = omit.
        public string HierarchyDepth = "summary";
        public string ScriptDepth    = "path";
        public string SceneDepth     = "path";
        public string PrefabDepth    = "path";
        public string AssetDepth     = "path";

        /// <summary>Return the configured depth string for a given kind.</summary>
        internal string DepthFor(ChipKind kind)
        {
            switch (kind)
            {
                case ChipKind.Hierarchy: return HierarchyDepth;
                case ChipKind.Script:    return ScriptDepth;
                case ChipKind.Scene:     return SceneDepth;
                case ChipKind.Prefab:    return PrefabDepth;
                default:                 return AssetDepth; // Material/Texture/SO/Asset
            }
        }
    }

    [Serializable]
    internal sealed class BackendConfigStore
    {
        public ClaudeBackendConfig Claude = new ClaudeBackendConfig();
        public CodexBackendConfig  Codex  = new CodexBackendConfig();
        public ChipConfig          Chips  = new ChipConfig();

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
                store.Chips  = store.Chips  ?? new ChipConfig();
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
