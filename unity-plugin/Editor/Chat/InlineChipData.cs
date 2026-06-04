// InlineChipTracker: pure data model for inline chips embedded in TextField text.
// U+FFFC (OBJECT REPLACEMENT CHARACTER '￼') is used as a placeholder in the text.
// The i-th marker in the text corresponds to the i-th ChipData in the list.
using System;
using System.Collections.Generic;
using System.Linq;

namespace UnityMCP.Editor.Chat
{
    internal readonly struct ChipData
    {
        internal readonly string Path;
        internal readonly string DisplayName;
        internal readonly int    InstanceID;

        internal ChipData(string path, string displayName, int instanceID)
        {
            Path        = path;
            DisplayName = displayName;
            InstanceID  = instanceID;
        }
    }

    /// <summary>
    /// Tracks inline chips associated with U+FFFC marker chars in a TextField value.
    /// All state-mutation methods are pure with respect to VisualElements.
    /// </summary>
    internal sealed class InlineChipTracker
    {
        internal const char Marker = '￼'; // U+FFFC OBJECT REPLACEMENT CHARACTER

        private readonly List<ChipData> _chips = new List<ChipData>();

        internal int Count => _chips.Count;

        internal IEnumerable<string> Paths => _chips.Select(c => c.Path);

        internal void Add(ChipData chip) => _chips.Add(chip);

        internal void RemoveAt(int index) => _chips.RemoveAt(index);

        internal void Clear() => _chips.Clear();

        internal ChipData this[int i] => _chips[i];

        /// <summary>
        /// Compare oldText → newText using the prefix/suffix algorithm to find which
        /// marker(s) were removed, then rebuild the index mapping from surviving markers.
        /// Returns the (original) indices of chips that were removed.
        /// Updates internal list in-place.
        /// </summary>
        internal List<int> SyncToText(string oldText, string newText)
        {
            oldText = oldText ?? "";
            newText = newText ?? "";

            int p = CommonPrefix(oldText, newText);
            int s = CommonSuffix(oldText, newText, p, p);

            // Edited region in old text: [p, oldText.Length - s)
            int oldEditStart = p;
            int oldEditEnd   = oldText.Length - s;

            // Find which chip indices had their marker inside the edited region
            var removedIndices = new List<int>();
            int markerPos = -1;
            for (int ci = 0; ci < _chips.Count; ci++)
            {
                // Find the (ci+1)-th occurrence of Marker in oldText
                markerPos = oldText.IndexOf(Marker, markerPos + 1);
                if (markerPos < 0) break; // fewer markers than chips — shouldn't happen
                if (markerPos >= oldEditStart && markerPos < oldEditEnd)
                    removedIndices.Add(ci);
            }

            // Remove in reverse order so indices stay valid
            for (int i = removedIndices.Count - 1; i >= 0; i--)
                _chips.RemoveAt(removedIndices[i]);

            // Reindex: count surviving markers in newText and reconcile
            // (handles edge case where a paste added/removed text around markers)
            int newMarkerCount = 0;
            foreach (char c in newText)
                if (c == Marker) newMarkerCount++;

            // If mismatch (e.g. paste deleted extra chips), trim from end
            while (_chips.Count > newMarkerCount)
                _chips.RemoveAt(_chips.Count - 1);

            return removedIndices;
        }

        // ── Pure static helpers ───────────────────────────────────────────────

        /// <summary>Length of the longest common prefix of a and b.</summary>
        internal static int CommonPrefix(string a, string b)
        {
            int max = Math.Min(a.Length, b.Length);
            int i = 0;
            while (i < max && a[i] == b[i]) i++;
            return i;
        }

        /// <summary>
        /// Length of the longest common suffix of a and b, given that the first
        /// <paramref name="prefixA"/> / <paramref name="prefixB"/> chars are already
        /// accounted for (so we don't overlap with the prefix).
        /// </summary>
        internal static int CommonSuffix(string a, string b, int prefixA, int prefixB)
        {
            int ia = a.Length - 1;
            int ib = b.Length - 1;
            int count = 0;
            while (ia >= prefixA && ib >= prefixB && a[ia] == b[ib])
            {
                ia--; ib--; count++;
            }
            return count;
        }
    }
}
