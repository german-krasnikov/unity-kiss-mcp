// BuildFromRaw defensive @mention stripping tests.
using System.Collections.Generic;
using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class BuildFromRawDefensiveTests
    {
        [SetUp]    public void SetUp()    => ChipKindRegistry.ResetToBuiltIns();
        [TearDown] public void TearDown()
        {
            ChipKindRegistry.ResetToBuiltIns();
            ChipPillFactory.ColorResolver = null;
        }

        private static ChipData H(string path, string name, int id = 0)
            => new ChipData(ChipKindKeys.Hierarchy, path, name, id);

        private static PositionedChip PC(ChipData chip, int offset)
            => new PositionedChip(chip, offset);

        // ── Positive: exact offsets ───────────────────────────────────────────

        [Test]
        public void R_Def1_ExactOffset_SingleChip_StripsCleanly()
        {
            var chip = H("/Coll_3", "Collectible_3", 1);
            // raw: "@Collectible_3 test" — chip at offset 0
            var msg = ChipTextInterleaver.BuildFromRaw("@Collectible_3 test",
                new List<PositionedChip> { PC(chip, 0) });
            var display = ChipTextInterleaver.ToDisplayText(msg);
            Assert.AreEqual("@Collectible_3 test", display);
            foreach (var seg in msg.Segments)
                if (!seg.IsChip) StringAssert.DoesNotContain("@Collectible_3", seg.Text);
        }

        [Test]
        public void R_Def2_TwoExactOffsets_BothStripped_TextPreserved()
        {
            var c1 = H("/Coll_3", "Collectible_3", 1);
            var c2 = H("/Coll_2", "Collectible_2", 2);
            // raw: "@Collectible_3 test @Collectible_2" — c2 @ at index 20
            var msg = ChipTextInterleaver.BuildFromRaw("@Collectible_3 test @Collectible_2",
                new List<PositionedChip> { PC(c1, 0), PC(c2, 20) });
            Assert.AreEqual(2, msg.Chips.Count);
            foreach (var seg in msg.Segments)
                if (!seg.IsChip) StringAssert.DoesNotContain("@", seg.Text);
        }

        [Test]
        public void R_Def3_ChipStartAndEnd_CleanSegments()
        {
            var c1 = H("/A", "Alpha", 1);
            var c2 = H("/B", "Beta", 2);
            // "@Alpha middle @Beta" — c1 at 0, c2 at 14
            var raw = "@Alpha middle @Beta";
            var msg = ChipTextInterleaver.BuildFromRaw(raw,
                new List<PositionedChip> { PC(c1, 0), PC(c2, 14) });
            var chips = new List<MessageSegment>();
            var texts = new List<string>();
            foreach (var s in msg.Segments)
                if (s.IsChip) chips.Add(s); else if (!string.IsNullOrEmpty(s.Text)) texts.Add(s.Text);
            Assert.AreEqual(2, chips.Count);
            Assert.AreEqual(1, texts.Count);
            StringAssert.Contains("middle", texts[0]);
        }

        [Test]
        public void R_Def4_ThreeChipsInterleaved_AllStripped()
        {
            var c1 = H("/A", "A1", 1); var c2 = H("/B", "B2", 2); var c3 = H("/C", "C3", 3);
            // "@A1 x @B2 y @C3"
            var raw = "@A1 x @B2 y @C3";
            var msg = ChipTextInterleaver.BuildFromRaw(raw,
                new List<PositionedChip> { PC(c1, 0), PC(c2, 6), PC(c3, 12) });
            Assert.AreEqual(3, msg.Chips.Count);
            foreach (var seg in msg.Segments)
                if (!seg.IsChip) StringAssert.DoesNotContain("@", seg.Text);
        }

        // ── Negative/edge: misaligned offsets ────────────────────────────────

        [Test]
        public void R_Def5_OffsetPlusOne_StillStrips()
        {
            var chip = H("/Coll_3", "Collectible_3", 1);
            // raw: "@Collectible_3 test", chip stored at offset 1 (off-by-one: points to 'C')
            var msg = ChipTextInterleaver.BuildFromRaw("@Collectible_3 test",
                new List<PositionedChip> { PC(chip, 1) });
            Assert.AreEqual(1, msg.Chips.Count);
            foreach (var seg in msg.Segments)
                if (!seg.IsChip) StringAssert.DoesNotContain("@Collectible_3", seg.Text);
        }

        [Test]
        public void R_Def6_OffsetMinusOne_StillStrips()
        {
            var chip = H("/Coll_3", "Collectible_3", 1);
            // raw: "x@Collectible_3 test", chip at offset 0 (before @)
            var msg = ChipTextInterleaver.BuildFromRaw("x@Collectible_3 test",
                new List<PositionedChip> { PC(chip, 0) });
            Assert.AreEqual(1, msg.Chips.Count);
            foreach (var seg in msg.Segments)
                if (!seg.IsChip) StringAssert.DoesNotContain("@Collectible_3", seg.Text);
        }

        [Test]
        public void R_Def7_MentionManuallyEdited_ChipPresent_TextPreserved()
        {
            var chip = H("/Coll_3", "Collectible_3", 1);
            // User edited "@Collectible_3" to "@Coll" — mention not found
            var msg = ChipTextInterleaver.BuildFromRaw("@Coll test",
                new List<PositionedChip> { PC(chip, 0) });
            // Chip still present in Chips list
            Assert.AreEqual(1, msg.Chips.Count);
        }

        [Test]
        public void R_Def8_DuplicateDisplayNames_BothStripped()
        {
            var c1 = new ChipData(ChipKindKeys.Hierarchy, "/PathA", "Camera", 1);
            var c2 = new ChipData(ChipKindKeys.Hierarchy, "/PathB", "Camera", 2);
            // "@Camera text @Camera"
            var raw = "@Camera text @Camera";
            var msg = ChipTextInterleaver.BuildFromRaw(raw,
                new List<PositionedChip> { PC(c1, 0), PC(c2, 13) });
            Assert.AreEqual(2, msg.Chips.Count);
            foreach (var seg in msg.Segments)
                if (!seg.IsChip) StringAssert.DoesNotContain("@Camera", seg.Text);
        }

        [Test]
        public void R_Def9_SpacesInDisplayName_Stripped()
        {
            var chip = H("/Main Camera", "Main Camera", 1);
            var raw = "@Main Camera fix";
            var msg = ChipTextInterleaver.BuildFromRaw(raw,
                new List<PositionedChip> { PC(chip, 0) });
            Assert.AreEqual(1, msg.Chips.Count);
            foreach (var seg in msg.Segments)
                if (!seg.IsChip) StringAssert.DoesNotContain("@Main Camera", seg.Text);
        }

        [Test]
        public void R_Def10_OnlyMention_StripsToEmpty()
        {
            var chip = H("/Player", "Player", 1);
            var msg = ChipTextInterleaver.BuildFromRaw("@Player",
                new List<PositionedChip> { PC(chip, 0) });
            Assert.AreEqual(1, msg.Chips.Count);
            var display = ChipTextInterleaver.ToDisplayText(msg);
            Assert.AreEqual("@Player", display);
        }

        [Test]
        public void R_Def11_NoSegmentContainsAtChipName()
        {
            var c1 = H("/Coll_3", "Collectible_3", 1);
            var c2 = H("/Coll_2", "Collectible_2", 2);
            var raw = "@Collectible_3 test @Collectible_2";
            var msg = ChipTextInterleaver.BuildFromRaw(raw,
                new List<PositionedChip> { PC(c1, 0), PC(c2, 20) });
            foreach (var seg in msg.Segments)
            {
                if (seg.IsChip) continue;
                Assert.IsFalse(seg.Text.Contains("@Collectible_3"),
                    $"Segment contains @Collectible_3: '{seg.Text}'");
                Assert.IsFalse(seg.Text.Contains("@Collectible_2"),
                    $"Segment contains @Collectible_2: '{seg.Text}'");
            }
        }

        // ── Regression: exact user scenario ──────────────────────────────────

        [Test]
        public void R_Def12_UserScenario_TwoChips_NoStrayAtMentions()
        {
            var field = new InlineChipField();
            field.AddChip(new ChipData(ChipKindKeys.Hierarchy, "/Collectible_3", "Collectible_3", 1));
            // Simulate typing "test " after the chip
            ChipTestHelpers.Type(field, " test ");
            field.AddChip(new ChipData(ChipKindKeys.Hierarchy, "/Collectible_2", "Collectible_2", 2));

            var rawText    = (field.Text ?? "").Trim();
            var positioned = new System.Collections.Generic.List<PositionedChip>(
                field.Model.PositionedChips);
            var msg = ChipTextInterleaver.BuildFromRaw(rawText, positioned);

            foreach (var seg in msg.Segments)
            {
                if (seg.IsChip) continue;
                Assert.IsFalse(seg.Text.Contains("@"),
                    $"Text segment contains @: '{seg.Text}'");
            }
            Assert.AreEqual(2, msg.Chips.Count, "Both chips must be present");
        }
    }
}
