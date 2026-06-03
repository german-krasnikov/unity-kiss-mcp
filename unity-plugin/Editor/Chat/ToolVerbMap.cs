// Maps raw tool names to human-friendly verbs shown in the chat UI.
// Pure logic, NUnit-testable.
using System.Collections.Generic;

namespace UnityMCP.Editor.Chat
{
    public static class ToolVerbMap
    {
        private static readonly Dictionary<string, string> _map =
            new Dictionary<string, string>
            {
                { "mcp__unity-mcp__get_hierarchy",        "Reading scene" },
                { "mcp__unity-mcp__set_property",         "Editing" },
                { "mcp__unity-mcp__create_object",        "Creating" },
                { "mcp__unity-mcp__screenshot",           "Capturing screenshot" },
                { "mcp__unity-mcp__batch",                "Running batch" },
                { "mcp__unity-mcp__manage_component",     "Editing components" },
                { "mcp__unity-mcp__run_playtest",         "Playtesting" },
                { "mcp__unity-mcp__validate_references",  "Checking refs" },
                { "mcp__unity-mcp__inspect",              "Reading" },
                { "mcp__unity-mcp__search_scene",         "Searching" },
                { "mcp__unity-mcp__find_references",      "Searching" },
                { "mcp__unity-mcp__wire_event",           "Wiring" },
                { "mcp__unity-mcp__delete_object",        "Deleting" },
                { "mcp__unity-mcp__get_component",        "Reading component" },
                { "mcp__unity-mcp__set_parent",           "Re-parenting" },
                { "mcp__unity-mcp__move_to",              "Moving" },
                { "mcp__unity-mcp__get_console",          "Reading console" },
                { "mcp__unity-mcp__get_compile_errors",   "Checking errors" },
                { "mcp__unity-mcp__run_tests",            "Running tests" },
                { "mcp__unity-mcp__scene",                "Managing scene" },
            };

        /// <summary>
        /// Returns a short human verb for a raw tool name.
        /// Falls back to stripping the mcp__ prefix and replacing underscores with spaces.
        /// </summary>
        public static string Humanize(string rawToolName)
        {
            if (string.IsNullOrEmpty(rawToolName)) return "Working";
            if (_map.TryGetValue(rawToolName, out var verb)) return verb;

            // Fallback: strip leading mcp__<server>__ prefix
            var name = rawToolName;
            var secondDunder = name.IndexOf("__", 4); // skip first "mcp__"
            if (name.StartsWith("mcp__") && secondDunder > 0)
                name = name.Substring(secondDunder + 2);

            return name.Replace('_', ' ');
        }
    }
}
