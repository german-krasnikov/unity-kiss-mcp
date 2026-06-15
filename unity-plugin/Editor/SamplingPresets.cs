using System.Collections.Generic;

namespace UnityMCP.Editor
{
    internal static class SamplingPresets
    {
        internal static readonly string[] KnownBackends = { "claude", "codex", "gemini" };

        internal static readonly Dictionary<string, string[]> ModelsByBackend = new()
        {
            ["claude"] = new[] { "haiku", "sonnet", "opus" },
            ["codex"]  = new[] { "codex-mini-latest", "o4-mini", "o3" },
            ["gemini"] = new[] { "gemini-2.5-flash", "gemini-2.5-pro" },
        };

        // Legacy All dict kept for backward compat (preset-apply logic moved to SettingsPageFactory)
        internal static readonly Dictionary<string, Dictionary<string, string>> All = new()
        {
            ["Claude"] = new()
            {
                ["visual_verify"]       = "haiku",
                ["screenshot_describe"] = "haiku",
                ["visual_diff"]         = "sonnet",
                ["summarize"]           = "haiku",
                ["do_intent"]           = "haiku",
                ["distiller"]           = "haiku",
            },
            ["Codex"] = new()
            {
                ["visual_verify"]       = "codex-mini-latest",
                ["screenshot_describe"] = "codex-mini-latest",
                ["visual_diff"]         = "codex-mini-latest",
                ["summarize"]           = "codex-mini-latest",
                ["do_intent"]           = "codex-mini-latest",
                ["distiller"]           = "codex-mini-latest",
            },
        };
    }
}
