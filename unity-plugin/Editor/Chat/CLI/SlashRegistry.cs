// Slash-command template registry — static data + pure matching/resolution logic.
using System;
using System.Collections.Generic;

namespace UnityMCP.Editor.Chat
{
    internal static class SlashRegistry
    {
        internal static readonly SlashTemplate[] Builtins =
        {
            new SlashTemplate("fix-compile",    "Fix all compile errors.",                          ContextGather.CompileErrors),
            new SlashTemplate("add-component",  "Add a component to the selected object.",          ContextGather.Selection),
            new SlashTemplate("playtest",       "Run a playtest to verify the scene works.",        ContextGather.SceneState),
            new SlashTemplate("inspect",        "Inspect the selected object in detail.",           ContextGather.Selection),
            new SlashTemplate("screenshot",     "Take a screenshot and describe what you see.",     ContextGather.None),
        };

        /// <summary>Returns templates whose Name starts with <paramref name="prefix"/>
        /// (case-insensitive). null/empty/"/" → all.</summary>
        internal static List<SlashTemplate> Match(string prefix)
        {
            if (prefix == null || prefix == "" || prefix == "/")
                return new List<SlashTemplate>(Builtins);

            var key = prefix.TrimStart('/').ToLowerInvariant();
            if (key == "")
                return new List<SlashTemplate>(Builtins);

            var results = new List<SlashTemplate>();
            foreach (var t in Builtins)
                if (t.Name.ToLowerInvariant().StartsWith(key))
                    results.Add(t);
            return results;
        }

        /// <summary>Builds the text to inject into the composer for a given template.
        /// <paramref name="gatherOverride"/> replaces production gather calls in tests.</summary>
        internal static string Resolve(SlashTemplate t,
            Func<ContextGather, string> gatherOverride = null)
        {
            if (t.Gather == ContextGather.None)
                return t.Prefill;

            string ctx = null;
            try
            {
                ctx = gatherOverride != null
                    ? gatherOverride(t.Gather)
                    : GatherContext(t.Gather);
            }
            catch { ctx = "(context unavailable)"; }

            if (string.IsNullOrEmpty(ctx)) return t.Prefill;
            return t.Prefill + "\n" + ctx;
        }

        // Production gather — calls real Unity APIs.
        private static string GatherContext(ContextGather flags)
        {
#if !UNITY_INCLUDE_TESTS
            var parts = new System.Text.StringBuilder();
            if ((flags & ContextGather.CompileErrors) != 0)
            {
                var err = CompileErrorCapture.GetErrors();
                if (!string.IsNullOrEmpty(err)) Append(parts, err);
            }
            if ((flags & ContextGather.Selection) != 0)
            {
                var sel = SelectionSummary.Summarize(UnityEditor.Selection.activeGameObject);
                if (!string.IsNullOrEmpty(sel)) Append(parts, sel);
            }
            if ((flags & ContextGather.SceneState) != 0)
            {
                var snap = EditorStateSnapshot.Capture();
                if (!string.IsNullOrEmpty(snap)) Append(parts, snap);
            }
            if ((flags & ContextGather.Console) != 0)
            {
                var logs = ConsoleCapture.GetLogs(5, "error");
                if (!string.IsNullOrEmpty(logs)) Append(parts, logs);
            }
            return parts.Length > 0 ? parts.ToString() : null;
#else
            return null;
#endif
        }

#if !UNITY_INCLUDE_TESTS
        private static void Append(System.Text.StringBuilder sb, string text)
        {
            if (sb.Length > 0) sb.Append('\n');
            sb.Append(text);
        }
#endif
    }
}
