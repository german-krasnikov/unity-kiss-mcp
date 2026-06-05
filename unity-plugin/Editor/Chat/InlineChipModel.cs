// InlineChipModel: pure C# data model for chips in the composed InlineChipField.
// No Unity rendering dependency — fully headless-testable.
// v5: chips are now PositionedChip (sorted by TextOffset) for Bug 3 fix.
using System.Collections.Generic;
using System.Linq;

namespace UnityMCP.Editor.Chat
{
    /// <summary>
    /// Pure data model for inline chips. Chips are stored with text positions (sorted ascending).
    /// No marker characters — text is always clean.
    /// </summary>
    internal sealed class InlineChipModel
    {
        private readonly List<PositionedChip> _chips = new List<PositionedChip>();

        internal int Count => _chips.Count;

        // Flat chip view for callers that don't need positions.
        internal IReadOnlyList<ChipData> Chips
            => _chips.Select(p => p.Chip).ToList();

        // Positional view for ChipTextInterleaver.
        internal IReadOnlyList<PositionedChip> PositionedChips => _chips;

        // Convenience: appends at sentinel offset int.MaxValue (sorts to end).
        internal void Add(ChipData chip) => InsertAt(int.MaxValue, chip);

        /// <summary>Insert chip at the given text offset; list stays sorted ascending.</summary>
        internal void InsertAt(int textOffset, ChipData chip)
        {
            var pc = new PositionedChip(chip, textOffset);
            // Insert in sorted position (simple linear scan — list is tiny, typically <5).
            int idx = _chips.Count;
            for (int i = 0; i < _chips.Count; i++)
            {
                if (_chips[i].TextOffset > textOffset) { idx = i; break; }
            }
            _chips.Insert(idx, pc);
        }

        /// <summary>Remove chip at index. Out-of-range is a no-op.</summary>
        internal void RemoveAt(int index)
        {
            if (index < 0 || index >= _chips.Count) return;
            _chips.RemoveAt(index);
        }

        /// <summary>
        /// Shift offsets of all chips at or after changeAt by delta.
        /// Called from TextField valueChanged callback to keep positions valid.
        /// </summary>
        internal void AdjustOffsetsAfterTextChange(int changeAt, int delta)
        {
            if (delta == 0) return;
            for (int i = 0; i < _chips.Count; i++)
            {
                if (_chips[i].TextOffset >= changeAt)
                {
                    // Skip sentinel offsets (int.MaxValue from Add() convenience) — adding delta overflows.
                    if (_chips[i].TextOffset == int.MaxValue) continue;
                    _chips[i] = new PositionedChip(_chips[i].Chip,
                        System.Math.Max(0, _chips[i].TextOffset + delta));
                }
            }
        }

        internal void Clear() => _chips.Clear();

        /// <summary>Serialize chip payload for the LLM.</summary>
        internal string SerializePayload(ChipConfig cfg)
            => ChipContextResolver.ResolveAllTyped(new List<ChipData>(Chips), cfg);

        /// <summary>
        /// Serialize chip paths + kind keys for domain-reload survival.
        /// Preserves v4 PendingTurnState format (parallel arrays).
        /// Existing callers that do var (paths, kindKeys) = ... still work.
        /// </summary>
        internal (string[] Paths, string[] KindKeys) SerializeForReload()
        {
            var paths    = new string[_chips.Count];
            var kindKeys = new string[_chips.Count];
            for (int i = 0; i < _chips.Count; i++)
            {
                paths[i]    = _chips[i].Chip.Path    ?? "";
                kindKeys[i] = _chips[i].Chip.KindKey ?? "";
            }
            return (paths, kindKeys);
        }

        /// <summary>Returns the text offsets parallel to SerializeForReload arrays (v5).</summary>
        internal int[] GetTextOffsets()
        {
            var offsets = new int[_chips.Count];
            for (int i = 0; i < _chips.Count; i++)
                offsets[i] = _chips[i].TextOffset;
            return offsets;
        }

        /// <summary>Rebuild model from persisted arrays after domain reload.
        /// Empty kindKey = v3 back-compat. textOffsets null = v4 back-compat (all offsets = 0).</summary>
        internal void RestoreFromReload(string[] paths, string[] kindKeys, int[] textOffsets = null)
        {
            _chips.Clear();
            if (paths == null) return;
            for (int i = 0; i < paths.Length; i++)
            {
                var path    = paths[i]    ?? "";
                var kindKey = (kindKeys != null && i < kindKeys.Length) ? kindKeys[i] ?? "" : "";
                var offset  = (textOffsets != null && i < textOffsets.Length) ? textOffsets[i] : 0;
                _chips.Add(new PositionedChip(
                    new ChipData(kindKey, path, DeriveDisplayName(path), 0), offset));
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
