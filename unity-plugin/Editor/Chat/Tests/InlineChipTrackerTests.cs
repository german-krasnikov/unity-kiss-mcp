// TDD — RED first. Tests drive InlineChipTracker (pure, no VisualElements).
// U+FFFC = '￼' (OBJECT REPLACEMENT CHARACTER)
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class InlineChipTrackerTests
    {
        private const char M = '￼'; // OBJECT REPLACEMENT CHARACTER

        private InlineChipTracker Make() => new InlineChipTracker();

        // ── Add / Paths ───────────────────────────────────────────────────────

        [Test]
        public void Add_ThenPaths_ReturnsPathsInOrder()
        {
            var t = Make();
            t.Add(new ChipData("/World/Player", "Player", 1));
            t.Add(new ChipData("/World/Enemy",  "Enemy",  2));

            var paths = t.Paths.ToList();
            Assert.AreEqual(2, paths.Count);
            Assert.AreEqual("/World/Player", paths[0]);
            Assert.AreEqual("/World/Enemy",  paths[1]);
        }

        // ── SyncToText: remove middle marker ─────────────────────────────────

        [Test]
        public void SyncToText_RemoveMiddleMarker_DropsMiddleChip()
        {
            var t = Make();
            t.Add(new ChipData("/A", "A", 1));
            t.Add(new ChipData("/B", "B", 2));
            t.Add(new ChipData("/C", "C", 3));

            // old: "a￼b￼c￼d"  (3 markers, matching 3 chips)
            var old = $"a{M}b{M}c{M}d";
            // new: "a￼bc￼d"  (middle marker deleted)
            var next = $"a{M}bc{M}d";

            var removed = t.SyncToText(old, next);

            Assert.AreEqual(1, removed.Count);
            Assert.AreEqual(1, removed[0]); // index 1 = chip B
            Assert.AreEqual(2, t.Count);
            var paths = t.Paths.ToList();
            Assert.AreEqual("/A", paths[0]);
            Assert.AreEqual("/C", paths[1]);
        }

        // ── SyncToText: backspace last marker ────────────────────────────────

        [Test]
        public void SyncToText_BackspaceLastMarker_DropsLastChip()
        {
            var t = Make();
            t.Add(new ChipData("/X", "X", 10));
            t.Add(new ChipData("/Y", "Y", 11));

            var old  = $"hello{M}world{M}";
            var next = $"hello{M}world";   // trailing marker deleted

            var removed = t.SyncToText(old, next);

            Assert.AreEqual(1, removed.Count);
            Assert.AreEqual(1, removed[0]);
            Assert.AreEqual(1, t.Count);
            Assert.AreEqual("/X", t.Paths.First());
        }

        // ── SyncToText: plain text change, no marker change ──────────────────

        [Test]
        public void SyncToText_NoMarkerChange_KeepsAllChips()
        {
            var t = Make();
            t.Add(new ChipData("/P", "P", 5));
            t.Add(new ChipData("/Q", "Q", 6));

            // edit plain text between the two markers
            var old  = $"foo{M}bar{M}";
            var next = $"foo{M}baz{M}"; // only 'r'→'z'

            var removed = t.SyncToText(old, next);

            Assert.AreEqual(0, removed.Count);
            Assert.AreEqual(2, t.Count);
        }

        // ── SyncToText: selection-delete spanning two markers ────────────────

        [Test]
        public void SyncToText_SelectionDeleteSpanningTwoMarkers_DropsBoth()
        {
            var t = Make();
            t.Add(new ChipData("/A", "A", 1));
            t.Add(new ChipData("/B", "B", 2));
            t.Add(new ChipData("/C", "C", 3));

            // "x￼y￼z￼w"  → select and delete "￼y￼" → "xz￼w"
            var old  = $"x{M}y{M}z{M}w";
            var next = $"xz{M}w";

            var removed = t.SyncToText(old, next);

            Assert.AreEqual(2, removed.Count);
            // chips A and B (indices 0 and 1) were inside the edited region
            Assert.Contains(0, removed);
            Assert.Contains(1, removed);
            Assert.AreEqual(1, t.Count);
            Assert.AreEqual("/C", t.Paths.First());
        }

        // ── Clear ─────────────────────────────────────────────────────────────

        [Test]
        public void Clear_EmptiesTrackerAndPaths()
        {
            var t = Make();
            t.Add(new ChipData("/A", "A", 1));
            t.Add(new ChipData("/B", "B", 2));

            t.Clear();

            Assert.AreEqual(0, t.Count);
            Assert.IsFalse(t.Paths.Any());
        }

        // ── CommonPrefix / CommonSuffix edge cases ────────────────────────────

        [Test]
        public void CommonPrefix_EmptyStrings_ReturnsZero()
        {
            Assert.AreEqual(0, InlineChipTracker.CommonPrefix("", ""));
        }

        [Test]
        public void CommonPrefix_IdenticalStrings_ReturnsFullLength()
        {
            Assert.AreEqual(5, InlineChipTracker.CommonPrefix("hello", "hello"));
        }

        [Test]
        public void CommonPrefix_OneEmpty_ReturnsZero()
        {
            Assert.AreEqual(0, InlineChipTracker.CommonPrefix("abc", ""));
            Assert.AreEqual(0, InlineChipTracker.CommonPrefix("", "xyz"));
        }

        [Test]
        public void CommonSuffix_EmptyStrings_ReturnsZero()
        {
            Assert.AreEqual(0, InlineChipTracker.CommonSuffix("", "", 0, 0));
        }

        [Test]
        public void CommonSuffix_IdenticalStrings_ReturnsFullLength()
        {
            // prefix already consumed 0 chars, both strings identical
            Assert.AreEqual(5, InlineChipTracker.CommonSuffix("hello", "hello", 0, 0));
        }

        [Test]
        public void CommonSuffix_PrefixAlreadyConsumed_DoesNotOverlap()
        {
            // "abcd" vs "axcd" → prefix=1 ("a"), then suffix from rest
            // old rest = "bcd", new rest = "xcd" → suffix = "cd" = 2
            int p = InlineChipTracker.CommonPrefix("abcd", "axcd"); // = 1
            int s = InlineChipTracker.CommonSuffix("abcd", "axcd", p, p);
            Assert.AreEqual(1, p);
            Assert.AreEqual(2, s);
        }
    }
}
