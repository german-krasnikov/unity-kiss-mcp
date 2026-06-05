// InlineChipTracker: pure data model for inline chips embedded in TextField text.
// U+FFFC (OBJECT REPLACEMENT CHARACTER '￼') is used as a placeholder in the text.
// The i-th marker in the text corresponds to the i-th ChipData in the list.
// H12: _expectedNbsp runs parallel to _chips (Wave 3 uses it; stored here for data integrity).
using System;
using System.Collections.Generic;
using System.Linq;

namespace UnityMCP.Editor.Chat
{
    /// <summary>Identity + metadata for a single inline chip. KindKey is the string identity (H6).</summary>
    public readonly struct ChipData
    {
        public readonly string KindKey;
        public readonly string Path;
        public readonly string DisplayName;
        public readonly int    InstanceID;

        public ChipData(string kindKey, string path, string displayName, int instanceID)
        {
            KindKey     = kindKey     ?? ChipKindKeys.Asset;
            Path        = path        ?? "";
            DisplayName = displayName ?? "";
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

        private readonly List<ChipData> _chips        = new List<ChipData>();
        private readonly List<int>      _expectedNbsp = new List<int>(); // H12: parallel to _chips

        internal int Count => _chips.Count;

        internal IEnumerable<string> Paths => _chips.Select(c => c.Path);

        // H12: expected NBSP counts accessor
        internal IReadOnlyList<int> ExpectedNbspCounts => _expectedNbsp;

        /// <summary>Add chip with optional expected NBSP count (H12).</summary>
        internal void Add(ChipData chip, int nbspCount = 0)
        {
            _chips.Add(chip);
            _expectedNbsp.Add(nbspCount);
        }

        internal void RemoveAt(int index)
        {
            _chips.RemoveAt(index);
            _expectedNbsp.RemoveAt(index);
        }

        internal void Clear()
        {
            _chips.Clear();
            _expectedNbsp.Clear();
        }

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

            int oldEditStart = p;
            int oldEditEnd   = oldText.Length - s;

            var removedIndices = new List<int>();
            int markerPos = -1;
            for (int ci = 0; ci < _chips.Count; ci++)
            {
                markerPos = oldText.IndexOf(Marker, markerPos + 1);
                if (markerPos < 0) break;
                if (markerPos >= oldEditStart && markerPos < oldEditEnd)
                    removedIndices.Add(ci);
            }

            // Remove in reverse so indices stay valid
            for (int i = removedIndices.Count - 1; i >= 0; i--)
            {
                _chips.RemoveAt(removedIndices[i]);
                _expectedNbsp.RemoveAt(removedIndices[i]);
            }

            int newMarkerCount = 0;
            foreach (char c in newText)
                if (c == Marker) newMarkerCount++;

            while (_chips.Count > newMarkerCount)
            {
                _chips.RemoveAt(_chips.Count - 1);
                _expectedNbsp.RemoveAt(_expectedNbsp.Count - 1);
            }

            return removedIndices;
        }

        // ── Pure static helpers ───────────────────────────────────────────────

        internal static int CommonPrefix(string a, string b)
        {
            int max = Math.Min(a.Length, b.Length);
            int i = 0;
            while (i < max && a[i] == b[i]) i++;
            return i;
        }

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
