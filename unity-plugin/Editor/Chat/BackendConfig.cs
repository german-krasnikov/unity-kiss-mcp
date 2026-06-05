// Per-backend CLI config persisted to Library/MCP_ChatBackendConfig.json (gitignored).
// JsonUtility requires [Serializable] + public fields.
// H15: HierarchyDepth default changed from "summary" to "path" (BREAKING).
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
        // BREAKING: HierarchyDepth default changed from "summary" to "path" (H15).
        // Users can restore via F9 settings form.
        public string HierarchyDepth = "path"; // BREAKING: default changed from summary to path (H15). Users restore via F9 settings.
        public string ScriptDepth    = "path";
        public string SceneDepth     = "path";
        public string PrefabDepth    = "path";
        public string AssetDepth     = "path";

        /// <summary>
        /// Return the configured depth string for a given kind key.
        /// Unknown keys fall back to ChipKindRegistry.ForKey(kindKey)?.DefaultDepth ?? "path" (H5).
        /// </summary>
        internal string DepthFor(string kindKey)
        {
            switch (kindKey)
            {
                case ChipKindKeys.Hierarchy: return HierarchyDepth;
                case ChipKindKeys.Script:    return ScriptDepth;
                case ChipKindKeys.Scene:     return SceneDepth;
                case ChipKindKeys.Prefab:    return PrefabDepth;
                // Material, Texture, SO, Asset → AssetDepth (built-in fallback)
                case ChipKindKeys.Material:
                case ChipKindKeys.Texture:
                case ChipKindKeys.ScriptableObject:
                case ChipKindKeys.Asset:     return AssetDepth;
                // H5: unknown/custom keys fall to registry provider's DefaultDepth
                default: return ChipKindRegistry.ForKey(kindKey)?.DefaultDepth ?? "path";
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
            File.WriteAllText(path, JsonUtility.ToJson(this, prettyPrint: true));
        }
    }
}
