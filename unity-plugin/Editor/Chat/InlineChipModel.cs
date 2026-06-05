// InlineChipModel: pure C# data model for chips in the composed InlineChipField.
// No Unity rendering dependency — fully headless-testable.
// Replaces InlineChipTracker (which tied chips to U+FFFC markers in TextField text).
using System.Collections.Generic;

namespace UnityMCP.Editor.Chat
{
    /// <summary>
    /// Pure data model for inline chips. Chips are positional (chips-at-front layout).
    /// No marker characters, no NBSP — text is always clean.
    /// </summary>
    internal sealed class InlineChipModel
    {
        private readonly List<ChipData> _chips = new List<ChipData>();

        internal int Count => _chips.Count;

        internal IReadOnlyList<ChipData> Chips => _chips;

        internal void Add(ChipData chip) => _chips.Add(chip);

        /// <summary>Remove chip at index. Out-of-range is a no-op.</summary>
        internal void RemoveAt(int index)
        {
            if (index < 0 || index >= _chips.Count) return;
            _chips.RemoveAt(index);
        }

        internal void Clear() => _chips.Clear();

        /// <summary>
        /// Serialize all chips to the AI-facing payload string.
        /// Delegates entirely to ChipContextResolver — single production path.
        /// </summary>
        internal string SerializePayload(ChipConfig cfg)
            => ChipContextResolver.ResolveAllTyped(new List<ChipData>(_chips), cfg);

        /// <summary>
        /// Serialize chip paths + kind keys for domain-reload survival.
        /// Preserves v4 PendingTurnState format (parallel arrays).
        /// </summary>
        internal (string[] Paths, string[] KindKeys) SerializeForReload()
        {
            var paths    = new string[_chips.Count];
            var kindKeys = new string[_chips.Count];
            for (int i = 0; i < _chips.Count; i++)
            {
                paths[i]    = _chips[i].Path    ?? "";
                kindKeys[i] = _chips[i].KindKey ?? "";
            }
            return (paths, kindKeys);
        }

        /// <summary>
        /// Rebuild model from persisted arrays after domain reload.
        /// Empty kindKey = v3 back-compat: kept as-is (chip remains usable).
        /// </summary>
        internal void RestoreFromReload(string[] paths, string[] kindKeys)
        {
            _chips.Clear();
            if (paths == null) return;
            for (int i = 0; i < paths.Length; i++)
            {
                var path    = paths[i]    ?? "";
                var kindKey = (kindKeys != null && i < kindKeys.Length) ? kindKeys[i] ?? "" : "";
                _chips.Add(new ChipData(
                    kindKey,
                    path, DeriveDisplayName(path), 0));
            }
        }

        // ── private ───────────────────────────────────────────────────────────

        private static string DeriveDisplayName(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;
            var slash = path.LastIndexOf('/');
            return slash >= 0 ? path.Substring(slash + 1) : path;
        }
    }
}
