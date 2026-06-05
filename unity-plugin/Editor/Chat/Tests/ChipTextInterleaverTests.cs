// TDD — ChipTextInterleaver tests (Group B).
// Pure headless: no Unity runtime dependency.
// Verifies Bug 1 fix: interleaved segment ordering.
using System.Collections.Generic;
using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class ChipTextInterleaverTests
    {
        [SetUp]    public void SetUp()    => ChipKindRegistry.ResetToBuiltIns();
        [TearDown] public void TearDown() => ChipKindRegistry.ResetToBuiltIns();

        private static ChipData H(string path, string name, int id = 0)
            => new ChipData(ChipKindKeys.Hierarchy, path, name, id);

        private static PositionedChip PC(ChipData chip, int offset)
            => new PositionedChip(chip, offset);

        // B1: empty text + no chips → UserMessage with one empty-text segment
        [Test]
        public void B1_EmptyTextNoChips_OneEmptyTextSegment()
        {
            var msg = ChipTextInterleaver.Build("", new List<PositionedChip>());
            Assert.AreEqual(1, msg.Segments.Count);
            Assert.IsFalse(msg.Segments[0].IsChip);
            Assert.AreEqual("", msg.Segments[0].Text);
        }

        // B2: text only, no chips → single text segment
        [Test]
        public void B2_TextOnlyNoChips_SingleTextSegment()
        {
            var msg = ChipTextInterleaver.Build("hello world", new List<PositionedChip>());
            Assert.AreEqual(1, msg.Segments.Count);
            Assert.IsFalse(msg.Segments[0].IsChip);
            Assert.AreEqual("hello world", msg.Segments[0].Text);
        }

        // B3: chip at offset 0, text "hello" → chip segment, then "hello" text segment
        [Test]
        public void B3_ChipAtOffset0_ChipThenText()
        {
            var chip = H("/A", "A", 1);
            var msg = ChipTextInterleaver.Build("hello", new List<PositionedChip> { PC(chip, 0) });
            Assert.AreEqual(2, msg.Segments.Count);
            Assert.IsTrue(msg.Segments[0].IsChip);
            Assert.AreEqual("/A", msg.Segments[0].Chip.Path);
            Assert.IsFalse(msg.Segments[1].IsChip);
            Assert.AreEqual("hello", msg.Segments[1].Text);
        }

        // B4: chip at offset 5, text "hello world" → "hello" text, chip, " world" text
        [Test]
        public void B4_ChipAtOffset5_TextChipText()
        {
            var chip = H("/A", "A", 1);
            var msg = ChipTextInterleaver.Build("hello world", new List<PositionedChip> { PC(chip, 5) });
            Assert.AreEqual(3, msg.Segments.Count);
            Assert.IsFalse(msg.Segments[0].IsChip);
            Assert.AreEqual("hello", msg.Segments[0].Text);
            Assert.IsTrue(msg.Segments[1].IsChip);
            Assert.IsFalse(msg.Segments[2].IsChip);
            Assert.AreEqual(" world", msg.Segments[2].Text);
        }

        // B5: two chips at different offsets — segments in correct order
        [Test]
        public void B5_TwoChipsAtDifferentOffsets_CorrectOrder()
        {
            var chipA = H("/A", "A", 1);
            var chipB = H("/B", "B", 2);
            var positioned = new List<PositionedChip> { PC(chipA, 3), PC(chipB, 8) };
            var msg = ChipTextInterleaver.Build("abcdefghij", positioned);
            // Expect: "abc" text, chip A, "efghi" text... wait: offsets 3 and 8 in "abcdefghij"
            // pos0→3: "abc", chip A, pos3→8: "defgh", chip B, pos8→10: "ij"
            Assert.AreEqual(5, msg.Segments.Count);
            Assert.AreEqual("abc",   msg.Segments[0].Text);
            Assert.AreEqual("/A",    msg.Segments[1].Chip.Path);
            Assert.AreEqual("defgh", msg.Segments[2].Text);
            Assert.AreEqual("/B",    msg.Segments[3].Chip.Path);
            Assert.AreEqual("ij",    msg.Segments[4].Text);
        }

        // B6: two chips same DisplayName different offsets — both appear in output
        [Test]
        public void B6_TwoChipsSameDisplayName_BothInSegments()
        {
            var chip1 = new ChipData(ChipKindKeys.Hierarchy, "/A", "Camera", 1);
            var chip2 = new ChipData(ChipKindKeys.Hierarchy, "/B", "Camera", 2);
            var positioned = new List<PositionedChip> { PC(chip1, 0), PC(chip2, 5) };
            var msg = ChipTextInterleaver.Build("hello world", positioned);
            var chipSegs = new List<MessageSegment>();
            foreach (var s in msg.Segments) if (s.IsChip) chipSegs.Add(s);
            Assert.AreEqual(2, chipSegs.Count);
            Assert.AreEqual("/A", chipSegs[0].Chip.Path);
            Assert.AreEqual("/B", chipSegs[1].Chip.Path);
        }

        // B7: ToLlmPayload — text segments concatenated, chip context appended
        [Test]
        public void B7_ToLlmPayload_TextPlusChipContext()
        {
            var chip = new ChipData(ChipKindKeys.Script, "Assets/Foo.cs", "Foo.cs", 0);
            var positioned = new List<PositionedChip> { PC(chip, 5) };
            var msg = ChipTextInterleaver.Build("fix it please", positioned);
            var payload = ChipTextInterleaver.ToLlmPayload(msg, new ChipConfig());
            StringAssert.Contains("fix it please", payload);
            StringAssert.Contains("[script:Assets/Foo.cs]", payload);
        }

        // B8: ToLlmPayload — empty chips → just plain text, no trailing newline
        [Test]
        public void B8_ToLlmPayload_NoChips_PlainTextOnly()
        {
            var msg = ChipTextInterleaver.Build("hello world", new List<PositionedChip>());
            var payload = ChipTextInterleaver.ToLlmPayload(msg, new ChipConfig());
            Assert.AreEqual("hello world", payload);
            Assert.IsFalse(payload.EndsWith("\n"));
        }

        // B9: ToLlmPayload — chip with depth=none → excluded from payload
        [Test]
        public void B9_ToLlmPayload_NoneDepthChip_ExcludedFromPayload()
        {
            var chip = new ChipData(ChipKindKeys.Script, "Assets/Foo.cs", "Foo.cs", 0);
            var positioned = new List<PositionedChip> { PC(chip, 0) };
            var msg = ChipTextInterleaver.Build("fix", positioned);
            var cfg = new ChipConfig { ScriptDepth = "none" };
            var payload = ChipTextInterleaver.ToLlmPayload(msg, cfg);
            Assert.AreEqual("fix", payload);
            Assert.IsFalse(payload.Contains("[script:"));
        }

        // Extra: null text treated as empty
        [Test]
        public void Extra_NullText_TreatedAsEmpty()
        {
            var msg = ChipTextInterleaver.Build(null, new List<PositionedChip>());
            Assert.AreEqual(1, msg.Segments.Count);
            Assert.IsFalse(msg.Segments[0].IsChip);
        }

        // Extra: chip list preserves only chips (no duplicates in Chips property)
        [Test]
        public void Extra_TwoChips_ChipsPropertyHasBoth()
        {
            var chipA = H("/A", "A", 1);
            var chipB = H("/B", "B", 2);
            var msg = ChipTextInterleaver.Build("hi", new List<PositionedChip>
                { PC(chipA, 0), PC(chipB, 1) });
            Assert.AreEqual(2, msg.Chips.Count);
        }

        // ── Group U: User scenario regression tests ───────────────────────────

        // U1: chip at 0, text "jhkjhkj", chip at 7 → [chip, text, chip] segments
        [Test]
        public void U1_ChipTextChip_InterleavedSegments()
        {
            var chipA = new ChipData(ChipKindKeys.Hierarchy, "/Main Camera", "Main Camera", -12345);
            var chipB = new ChipData(ChipKindKeys.Hierarchy, "/Light", "Light", -99);
            var positioned = new List<PositionedChip> { PC(chipA, 0), PC(chipB, 7) };
            var msg = ChipTextInterleaver.Build("jhkjhkj", positioned);

            Assert.AreEqual(3, msg.Segments.Count);
            Assert.IsTrue(msg.Segments[0].IsChip);
            Assert.AreEqual("/Main Camera", msg.Segments[0].Chip.Path);
            Assert.IsFalse(msg.Segments[1].IsChip);
            Assert.AreEqual("jhkjhkj", msg.Segments[1].Text);
            Assert.IsTrue(msg.Segments[2].IsChip);
            Assert.AreEqual("/Light", msg.Segments[2].Chip.Path);
            Assert.AreEqual(2, msg.Chips.Count);
        }

        // U2: same as U1 but verify LLM payload contains chip context
        [Test]
        public void U2_ChipTextChip_LlmPayloadContainsChipContext()
        {
            var chipA = new ChipData(ChipKindKeys.Hierarchy, "/Main Camera", "Main Camera", -12345);
            var chipB = new ChipData(ChipKindKeys.Hierarchy, "/Light", "Light", -99);
            var positioned = new List<PositionedChip> { PC(chipA, 0), PC(chipB, 7) };
            var msg = ChipTextInterleaver.Build("jhkjhkj", positioned);
            var payload = ChipTextInterleaver.ToLlmPayload(msg, new ChipConfig());

            StringAssert.Contains("jhkjhkj", payload);
            StringAssert.Contains("[hierarchy:", payload);
            Assert.Greater(payload.Length, "jhkjhkj".Length);
        }

        // U3: both chips at offset 7 (end of text) — text first, then both chips.
        // Regression: chips must NOT be lost even when offset equals text length.
        [Test]
        public void U3_BothChipsAtEnd_TextThenBothChips()
        {
            var chipA = new ChipData(ChipKindKeys.Hierarchy, "/Main Camera", "Main Camera", -12345);
            var chipB = new ChipData(ChipKindKeys.Hierarchy, "/Light", "Light", -99);
            var positioned = new List<PositionedChip> { PC(chipA, 7), PC(chipB, 7) };
            var msg = ChipTextInterleaver.Build("jhkjhkj", positioned);

            Assert.AreEqual(2, msg.Chips.Count);
            var chipSegs = new List<MessageSegment>();
            foreach (var s in msg.Segments) if (s.IsChip) chipSegs.Add(s);
            Assert.AreEqual(2, chipSegs.Count);
        }

        // U4: chips at end — LLM payload still includes chip context
        [Test]
        public void U4_BothChipsAtEnd_PayloadIncludesChipContext()
        {
            var chipA = new ChipData(ChipKindKeys.Hierarchy, "/Main Camera", "Main Camera", -12345);
            var positioned = new List<PositionedChip> { PC(chipA, 7) };
            var msg = ChipTextInterleaver.Build("jhkjhkj", positioned);
            var payload = ChipTextInterleaver.ToLlmPayload(msg, new ChipConfig());

            StringAssert.Contains("[hierarchy:", payload);
            StringAssert.Contains("jhkjhkj", payload);
        }

        // U5: display text excludes chip payloads — only raw text returned
        [Test]
        public void U5_DisplayText_ExcludesChipContext()
        {
            var chip = new ChipData(ChipKindKeys.Hierarchy, "/Main Camera", "Main Camera", -12345);
            var msg = ChipTextInterleaver.Build("hello", new List<PositionedChip> { PC(chip, 5) });
            var display = ChipTextInterleaver.ToDisplayText(msg);
            Assert.AreEqual("hello", display);
        }

        // D9 (moved from UserMessageBubbleTests): no @ in any text segment — Build internals
        [Test]
        public void D9_TextSegments_NoAtMentions()
        {
            var chip = H("/Player", "Player", 1);
            var msg = ChipTextInterleaver.Build("fix this", new List<PositionedChip> { PC(chip, 4) });
            foreach (var seg in msg.Segments)
                if (!seg.IsChip) StringAssert.DoesNotContain("@", seg.Text);
        }
    }
}
