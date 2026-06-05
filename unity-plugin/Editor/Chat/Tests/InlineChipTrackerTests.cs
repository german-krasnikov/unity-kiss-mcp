// TDD — InlineChipTracker tests.
// H6: ChipData uses string KindKey (ChipKind enum removed).
// H12: new expectedNbsp parallel tracking tests.
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
            t.Add(new ChipData(ChipKindKeys.Hierarchy, "/World/Player", "Player", 1));
            t.Add(new ChipData(ChipKindKeys.Hierarchy, "/World/Enemy",  "Enemy",  2));

            var paths = t.Paths.ToList();
            Assert.AreEqual(2, paths.Count);
            Assert.AreEqual("/World/Player", paths[0]);
            Assert.AreEqual("/World/Enemy",  paths[1]);
        }

        // ── SyncToText ────────────────────────────────────────────────────────

        [Test]
        public void SyncToText_RemoveMiddleMarker_DropsMiddleChip()
        {
            var t = Make();
            t.Add(new ChipData(ChipKindKeys.Asset, "/A", "A", 1));
            t.Add(new ChipData(ChipKindKeys.Asset, "/B", "B", 2));
            t.Add(new ChipData(ChipKindKeys.Asset, "/C", "C", 3));

            var old  = $"a{M}b{M}c{M}d";
            var next = $"a{M}bc{M}d";

            var removed = t.SyncToText(old, next);

            Assert.AreEqual(1, removed.Count);
            Assert.AreEqual(1, removed[0]);
            Assert.AreEqual(2, t.Count);
            var paths = t.Paths.ToList();
            Assert.AreEqual("/A", paths[0]);
            Assert.AreEqual("/C", paths[1]);
        }

        [Test]
        public void SyncToText_BackspaceLastMarker_DropsLastChip()
        {
            var t = Make();
            t.Add(new ChipData(ChipKindKeys.Asset, "/X", "X", 10));
            t.Add(new ChipData(ChipKindKeys.Asset, "/Y", "Y", 11));

            var old  = $"hello{M}world{M}";
            var next = $"hello{M}world";

            var removed = t.SyncToText(old, next);

            Assert.AreEqual(1, removed.Count);
            Assert.AreEqual(1, removed[0]);
            Assert.AreEqual(1, t.Count);
            Assert.AreEqual("/X", t.Paths.First());
        }

        [Test]
        public void SyncToText_NoMarkerChange_KeepsAllChips()
        {
            var t = Make();
            t.Add(new ChipData(ChipKindKeys.Asset, "/P", "P", 5));
            t.Add(new ChipData(ChipKindKeys.Asset, "/Q", "Q", 6));

            var old  = $"foo{M}bar{M}";
            var next = $"foo{M}baz{M}";

            var removed = t.SyncToText(old, next);

            Assert.AreEqual(0, removed.Count);
            Assert.AreEqual(2, t.Count);
        }

        [Test]
        public void SyncToText_SelectionDeleteSpanningTwoMarkers_DropsBoth()
        {
            var t = Make();
            t.Add(new ChipData(ChipKindKeys.Asset, "/A", "A", 1));
            t.Add(new ChipData(ChipKindKeys.Asset, "/B", "B", 2));
            t.Add(new ChipData(ChipKindKeys.Asset, "/C", "C", 3));

            var old  = $"x{M}y{M}z{M}w";
            var next = $"xz{M}w";

            var removed = t.SyncToText(old, next);

            Assert.AreEqual(2, removed.Count);
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
            t.Add(new ChipData(ChipKindKeys.Asset, "/A", "A", 1));
            t.Add(new ChipData(ChipKindKeys.Asset, "/B", "B", 2));

            t.Clear();

            Assert.AreEqual(0, t.Count);
            Assert.IsFalse(t.Paths.Any());
        }

        // ── CommonPrefix / CommonSuffix ───────────────────────────────────────

        [Test]
        public void CommonPrefix_EmptyStrings_ReturnsZero()
            => Assert.AreEqual(0, InlineChipTracker.CommonPrefix("", ""));

        [Test]
        public void CommonPrefix_IdenticalStrings_ReturnsFullLength()
            => Assert.AreEqual(5, InlineChipTracker.CommonPrefix("hello", "hello"));

        [Test]
        public void CommonPrefix_OneEmpty_ReturnsZero()
        {
            Assert.AreEqual(0, InlineChipTracker.CommonPrefix("abc", ""));
            Assert.AreEqual(0, InlineChipTracker.CommonPrefix("", "xyz"));
        }

        [Test]
        public void CommonSuffix_EmptyStrings_ReturnsZero()
            => Assert.AreEqual(0, InlineChipTracker.CommonSuffix("", "", 0, 0));

        [Test]
        public void CommonSuffix_IdenticalStrings_ReturnsFullLength()
            => Assert.AreEqual(5, InlineChipTracker.CommonSuffix("hello", "hello", 0, 0));

        [Test]
        public void CommonSuffix_PrefixAlreadyConsumed_DoesNotOverlap()
        {
            int p = InlineChipTracker.CommonPrefix("abcd", "axcd");
            int s = InlineChipTracker.CommonSuffix("abcd", "axcd", p, p);
            Assert.AreEqual(1, p);
            Assert.AreEqual(2, s);
        }

        // ── KindKey field roundtrip (H6) ──────────────────────────────────────

        [Test]
        public void ChipData_CarriesKindKey()
        {
            var chip = new ChipData(ChipKindKeys.Script, "Assets/Foo.cs", "Foo", 42);
            Assert.AreEqual(ChipKindKeys.Script, chip.KindKey);
            Assert.AreEqual("Assets/Foo.cs",     chip.Path);
            Assert.AreEqual("Foo",               chip.DisplayName);
            Assert.AreEqual(42,                  chip.InstanceID);
        }

        [Test]
        public void Add_WithKindKey_KindKeyPreserved()
        {
            var t = Make();
            t.Add(new ChipData(ChipKindKeys.Material, "Assets/Mat.mat", "Lava", 99));
            Assert.AreEqual(ChipKindKeys.Material, t[0].KindKey);
            Assert.AreEqual("Assets/Mat.mat",      t[0].Path);
        }

        // ── H12: expectedNbsp parallel tracking ──────────────────────────────

        [Test]
        public void Add_WithNbspCount_ExpectedNbspCountsPreserved()
        {
            var t = Make();
            t.Add(new ChipData(ChipKindKeys.Hierarchy, "/A", "A", 1), 4);
            t.Add(new ChipData(ChipKindKeys.Script,    "/B", "B", 2), 7);

            Assert.AreEqual(2, t.ExpectedNbspCounts.Count);
            Assert.AreEqual(4, t.ExpectedNbspCounts[0]);
            Assert.AreEqual(7, t.ExpectedNbspCounts[1]);
        }

        [Test]
        public void Add_DefaultNbsp_IsZero()
        {
            var t = Make();
            t.Add(new ChipData(ChipKindKeys.Asset, "/C", "C", 3));
            Assert.AreEqual(1, t.ExpectedNbspCounts.Count);
            Assert.AreEqual(0, t.ExpectedNbspCounts[0]);
        }

        [Test]
        public void RemoveAt_SyncsNbspList()
        {
            var t = Make();
            t.Add(new ChipData(ChipKindKeys.Asset, "/A", "A", 1), 2);
            t.Add(new ChipData(ChipKindKeys.Asset, "/B", "B", 2), 5);
            t.Add(new ChipData(ChipKindKeys.Asset, "/C", "C", 3), 3);

            t.RemoveAt(1); // remove B

            Assert.AreEqual(2, t.ExpectedNbspCounts.Count);
            Assert.AreEqual(2, t.ExpectedNbspCounts[0]); // A
            Assert.AreEqual(3, t.ExpectedNbspCounts[1]); // C
        }

        [Test]
        public void Clear_ResetsNbspList()
        {
            var t = Make();
            t.Add(new ChipData(ChipKindKeys.Asset, "/A", "A", 1), 4);
            t.Clear();
            Assert.AreEqual(0, t.ExpectedNbspCounts.Count);
        }

        [Test]
        public void SyncToText_RemoveChip_SyncsNbspList()
        {
            var t = Make();
            t.Add(new ChipData(ChipKindKeys.Asset, "/A", "A", 1), 2);
            t.Add(new ChipData(ChipKindKeys.Asset, "/B", "B", 2), 5);

            var old  = $"x{M}y{M}";
            var next = $"xy{M}";

            t.SyncToText(old, next);

            // A was in edit region → removed. B survives.
            Assert.AreEqual(1, t.ExpectedNbspCounts.Count);
            Assert.AreEqual(5, t.ExpectedNbspCounts[0]);
        }
    }
}
