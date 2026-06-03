// Pure walk-up logic — System.IO only, zero UnityEngine deps. NUnit-testable.
using System;
using System.Collections.Generic;
using System.IO;

namespace UnityMCP.Editor.Chat
{
    internal static class AgentSearchPath
    {
        // Ordered nearest-first list of candidate ".claude/agents" dirs.
        // Does NOT check existence — BackendRegistry guards that.
        internal static List<string> Resolve(string projectRoot, string homeDir)
        {
            var dirs = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            void Add(string baseDir)
            {
                if (string.IsNullOrEmpty(baseDir)) return;
                var p = Path.Combine(baseDir, ".claude", "agents");
                if (seen.Add(p)) dirs.Add(p);
            }

            var d = projectRoot;
            while (!string.IsNullOrEmpty(d))
            {
                Add(d);
                var parent = Path.GetDirectoryName(d);
                if (parent == d) break; // reached filesystem root
                d = parent;
            }

            Add(homeDir);
            return dirs;
        }
    }
}
