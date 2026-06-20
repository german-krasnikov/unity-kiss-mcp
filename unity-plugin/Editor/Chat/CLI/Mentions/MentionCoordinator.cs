// Phase 3: Orchestrates all IMentionSource providers.
// Merges, deduplicates by path (keeps higher score), sorts desc, caps results.
// Pure synchronous logic — no UI dependency, fully NUnit-testable.
using System.Collections.Generic;

namespace UnityMCP.Editor.Chat
{
    internal sealed class MentionCoordinator
    {
        private readonly List<IMentionSource> _sources;
        private readonly List<MentionCandidate> _temp = new List<MentionCandidate>();
        private readonly Dictionary<string, MentionCandidate> _dedupMap = new Dictionary<string, MentionCandidate>();
        private int _requestId;

        internal MentionCoordinator(params IMentionSource[] sources)
        {
            _sources = new List<IMentionSource>(sources);
        }

        /// <summary>
        /// Search all sources, merge + dedup by path (higher score wins), sort desc, cap at maxResults.
        /// Returns request ID for staleness check via IsCurrent().
        /// </summary>
        internal int Search(string query, int maxResults, List<MentionCandidate> results)
        {
            int id = ++_requestId;

            if (string.IsNullOrEmpty(query))
                return id;

            _dedupMap.Clear();
            _temp.Clear();

            foreach (var source in _sources)
            {
                source.RefreshIfDirty();
                source.Search(query, maxResults * 2, _temp);
            }

            // Dedup by path — keep higher score
            foreach (var candidate in _temp)
            {
                string path = candidate.Chip.Path;
                if (!_dedupMap.TryGetValue(path, out var existing) || candidate.Score > existing.Score)
                    _dedupMap[path] = candidate;
            }

            // Collect, sort desc, cap
            _temp.Clear();
            foreach (var kv in _dedupMap.Values)
                _temp.Add(kv);

            _temp.Sort((a, b) => b.Score.CompareTo(a.Score));

            int count = System.Math.Min(_temp.Count, maxResults);
            for (int i = 0; i < count; i++)
                results.Add(_temp[i]);

            return id;
        }

        /// <summary>True if the given request ID is still the latest (no newer search started).</summary>
        internal bool IsCurrent(int requestId) => requestId == _requestId;
    }
}
