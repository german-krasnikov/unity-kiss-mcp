// Pure frontmatter parser — zero UnityEngine deps. NUnit-testable.
using System;

namespace UnityMCP.Editor.Chat
{
    internal static class AgentFrontmatterParser
    {
        /// <summary>
        /// Extract the `name:` value from the first YAML frontmatter block.
        /// Falls back to <paramref name="fileStem"/> when not found.
        /// </summary>
        internal static string ParseName(string fileText, string fileStem)
        {
            if (string.IsNullOrEmpty(fileText)) return fileStem;

            // Normalize line endings; trim leading blank lines.
            var text  = fileText.Replace("\r\n", "\n").TrimStart('\n', '\r', ' ');
            var lines = text.Split('\n');

            // Must start with ---
            if (lines.Length < 2 || lines[0].Trim() != "---") return fileStem;

            for (var i = 1; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (line == "---") break; // end of frontmatter — name: not found

                if (!line.StartsWith("name:", StringComparison.Ordinal)) continue;

                var value = line.Substring("name:".Length).Trim();
                // Strip surrounding quotes if present
                if (value.Length >= 2 && value[0] == '"' && value[value.Length - 1] == '"')
                    value = value.Substring(1, value.Length - 2).Trim();
                else if (value.Length >= 2 && value[0] == '\'' && value[value.Length - 1] == '\'')
                    value = value.Substring(1, value.Length - 2).Trim();

                return string.IsNullOrEmpty(value) ? fileStem : value;
            }

            return fileStem;
        }
    }
}
