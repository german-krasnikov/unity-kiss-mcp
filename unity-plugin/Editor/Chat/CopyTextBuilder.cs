// Pure string helpers for building clipboard text. Zero UnityEngine deps.
using System.Collections.Generic;

namespace UnityMCP.Editor.Chat
{
    internal static class CopyTextBuilder
    {
        internal static string ForUser(string userText) => userText ?? "";

        internal static string ForAssistant(string rawMarkdown) => rawMarkdown ?? "";

        internal static string ForToolChip(ToolCallRecord rec)
        {
            if (string.IsNullOrEmpty(rec.Name)) return "";
            var s = rec.Name;
            if (!string.IsNullOrEmpty(rec.ArgsJson)) s += " " + rec.ArgsJson;
            if (rec.HasResult) s += "\n→ " + rec.ResultText;   // → arrow
            return s;
        }

        internal static string ForToolGroup(IReadOnlyList<string> childTexts)
        {
            if (childTexts == null || childTexts.Count == 0) return "";
            return string.Join("\n", childTexts);
        }
    }
}
