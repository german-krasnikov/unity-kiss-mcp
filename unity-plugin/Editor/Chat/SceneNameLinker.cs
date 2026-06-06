// Auto-links known scene object names in rich text with <link> tags.
// Pure logic with injectable name dictionary — NUnit-testable.
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace UnityMCP.Editor.Chat
{
    internal sealed class SceneNameLinker
    {
        private Dictionary<string, string> _names = new Dictionary<string, string>();
        private Regex _regex;

        private static readonly HashSet<string> SkipList = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
        {
            "Canvas", "Camera", "Light", "Image", "Text", "Button", "Panel",
            "Slider", "Toggle", "Grid", "Manager", "Controller", "System",
            "Default", "Global", "World", "Event", "Debug", "Player"
        };

        internal SceneNameLinker() { }

        // Constructor for tests — inject names directly
        internal SceneNameLinker(Dictionary<string, string> names)
        {
            _names = names ?? new Dictionary<string, string>();
            RebuildRegex();
        }

        internal void Refresh(IReadOnlyDictionary<string, string> objects)
        {
            _names.Clear();
            if (objects == null) { _regex = null; return; }
            foreach (var kv in objects)
            {
                if (ShouldAutoLink(kv.Key))
                    _names[kv.Key] = kv.Value;
            }
            RebuildRegex();
        }

        internal static bool ShouldAutoLink(string name)
        {
            if (string.IsNullOrEmpty(name) || name.Length < 3) return false;
            if (SkipList.Contains(name)) return false;
            bool hasDigit = false, hasUnderscore = false, hasConsecUpper = false;
            for (int i = 0; i < name.Length; i++)
            {
                if (char.IsDigit(name[i])) hasDigit = true;
                if (name[i] == '_') hasUnderscore = true;
                if (i > 0 && char.IsUpper(name[i]) && char.IsUpper(name[i - 1])) hasConsecUpper = true;
            }
            return hasDigit || hasUnderscore || hasConsecUpper;
        }

        internal string Linkify(string text)
        {
            if (string.IsNullOrEmpty(text) || _regex == null || _names.Count == 0) return text;
            return _regex.Replace(text, m =>
            {
                var before    = text.Substring(0, m.Index);
                int linkOpen  = CountOccurrences(before, "<link=");
                int linkClose = CountOccurrences(before, "</link>");
                if (linkOpen > linkClose) return m.Value;

                var name = m.Value;
                if (!_names.TryGetValue(name, out var path)) return name;
                return $"<link=\"chip:hierarchy:{path}\"><u>{name}</u></link>";
            });
        }

        private void RebuildRegex()
        {
            if (_names.Count == 0) { _regex = null; return; }
            var escaped = _names.Keys.OrderByDescending(k => k.Length).Select(Regex.Escape);
            _regex = new Regex(@"\b(" + string.Join("|", escaped) + @")\b", RegexOptions.Compiled);
        }

        private static int CountOccurrences(string text, string pattern)
        {
            int count = 0, idx = 0;
            while ((idx = text.IndexOf(pattern, idx, System.StringComparison.Ordinal)) >= 0)
            { count++; idx += pattern.Length; }
            return count;
        }
    }
}
