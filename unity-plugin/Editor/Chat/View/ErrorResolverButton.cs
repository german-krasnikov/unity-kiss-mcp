using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor.Chat
{
    /// <summary>
    /// Footer toolbar button — reads runtime errors from console, deduplicates them,
    /// and dispatches a structured fix prompt to the active chat session (F1).
    /// </summary>
    [InitializeOnLoad]
    internal sealed class ErrorResolverButton : IToolbarButtonProvider
    {
        public string Key         => "error_resolver";
        public int    Order       => 10;
        public string ButtonLabel => "Fix Errors";
        public string Tooltip     => "Group runtime errors and send a fix prompt to chat";
        public bool   MenuOnly    => true;

        static ErrorResolverButton()
            => ToolbarButtonRegistry.Register(new ErrorResolverButton());

        public void OnClick(EditorWindow window)
        {
            var chat = window as MCPChatWindow;
            if (chat == null) return;

            var raw = ConsoleCapture.GetLogs(-1, "error");
            if (string.IsNullOrWhiteSpace(raw))
            {
                EditorUtility.DisplayDialog("Fix Errors", "No errors in console.", "OK");
                return;
            }

            var grouped = GroupErrors(raw);
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Fix All — Best Practices"), false,
                () => DispatchPreset(chat, grouped, "best_practices"));
            menu.AddItem(new GUIContent("Fix All — Quick Fix"), false,
                () => DispatchPreset(chat, grouped, "quick_fix"));
            menu.AddItem(new GUIContent("Fix All — Custom"), false,
                () => DispatchPreset(chat, grouped, "custom"));
            menu.ShowAsContext();
        }

        static void DispatchPreset(MCPChatWindow chat, string grouped, string preset)
        {
            EditorPrefs.SetString("MCP.ErrorResolver.Preset", preset);
            chat.InjectMessage(BuildPrompt(grouped, preset));
        }

        /// <summary>
        /// Collapse duplicate error messages. Key = first non-stack-trace line, value = count.
        /// Returns formatted string with "(xN)" suffix for duplicates.
        /// </summary>
        internal static string GroupErrors(string rawLogs)
        {
            if (string.IsNullOrEmpty(rawLogs)) return "";
            var counts = new Dictionary<string, int>();
            foreach (var raw in rawLogs.Split('\n'))
            {
                var line = raw.Trim();
                if (string.IsNullOrEmpty(line)) continue;
                if (line.StartsWith("at ", StringComparison.Ordinal)) continue;
                if (!counts.ContainsKey(line)) counts[line] = 0;
                counts[line]++;
            }
            var sb = new StringBuilder();
            foreach (var kv in counts)
                sb.AppendLine(kv.Value > 1 ? $"{kv.Key} (x{kv.Value})" : kv.Key);
            return sb.ToString().TrimEnd();
        }

        /// <summary>Build final prompt from grouped errors and agent preset.</summary>
        internal static string BuildPrompt(string grouped, string preset)
        {
            string prefix = preset switch
            {
                "best_practices" => "Fix these Unity runtime errors following SOLID/Unity best practices:\n",
                "quick_fix"      => "Fix these Unity runtime errors with minimal code changes:\n",
                _                => EditorPrefs.GetString("MCP.ErrorResolver.CustomPrefix", ""),
            };
            return prefix + grouped;
        }
    }
}
