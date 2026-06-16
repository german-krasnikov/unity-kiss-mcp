// Model preset data types and defaults — extracted from BackendConfig.cs.
using System;
using System.Collections.Generic;

namespace UnityMCP.Editor.Chat
{
    [Serializable]
    internal sealed class ModelPresetEntry
    {
        public string label   = "";
        public string modelId = "";
    }

    [Serializable]
    internal sealed class ModelPresetsConfig
    {
        public ModelPresetEntry[] Claude = new ModelPresetEntry[0];
        public ModelPresetEntry[] Codex  = new ModelPresetEntry[0];
        public ModelPresetEntry[] Gemini = new ModelPresetEntry[0];

        internal ModelPresetEntry[] For(BackendKind kind)
        {
            switch (kind)
            {
                case BackendKind.Claude: return Claude;
                case BackendKind.Codex:  return Codex;
                case BackendKind.Gemini: return Gemini;
                default: return new ModelPresetEntry[0];
            }
        }
    }

    internal static class ModelPresetDefaults
    {
        internal const string CustomSentinel = "__custom__";

        internal static readonly Dictionary<BackendKind, (string label, string modelId)[]> All
            = new Dictionary<BackendKind, (string, string)[]>
        {
            [BackendKind.Claude] = new[]
            {
                ("Default",    ""),
                ("Fable 5",    "claude-fable-5"),
                ("Opus 4.8",   "claude-opus-4-8"),
                ("Opus 4.7",   "claude-opus-4-7"),
                ("Opus 4.6",   "claude-opus-4-6"),
                ("Sonnet 4.6", "claude-sonnet-4-6"),
                ("Haiku 4.5",  "claude-haiku-4-5"),
                ("Custom...",  CustomSentinel),
            },
            [BackendKind.Codex] = new[]
            {
                ("Default",      ""),
                ("GPT-5.5",      "gpt-5.5"),
                ("GPT-5.4",      "gpt-5.4"),
                ("GPT-5.4 Mini", "gpt-5.4-mini"),
                ("o3-pro",       "o3-pro"),
                ("o3",           "o3"),
                ("o4-mini",      "o4-mini"),
                ("GPT-4.1",      "gpt-4.1"),
                ("GPT-4.1 Mini", "gpt-4.1-mini"),
                ("GPT-4o",       "gpt-4o"),
                ("Custom...",    CustomSentinel),
            },
            [BackendKind.Gemini] = new[]
            {
                ("Default",          ""),
                ("3.5 Flash",        "gemini-3.5-flash"),
                ("3.1 Pro Preview",  "gemini-3.1-pro-preview"),
                ("3 Pro Preview",    "gemini-3-pro-preview"),
                ("3 Flash Preview",  "gemini-3-flash-preview"),
                ("2.5 Pro",          "gemini-2.5-pro"),
                ("2.5 Flash",        "gemini-2.5-flash"),
                ("2.5 Flash Lite",   "gemini-2.5-flash-lite"),
                ("Custom...",        CustomSentinel),
            },
        };

        internal static (string label, string modelId)[] For(BackendKind kind)
            => All.TryGetValue(kind, out var p) ? p : new[] { ("Default", ""), ("Custom...", CustomSentinel) };
    }
}
