// TDD — BareNameNormalizer tests (F14a).
// Pure headless: no Unity runtime dependency.
// Verifies bare name → [kind:ref] bracket tag replacement.
using System.Collections.Generic;
using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class BareNameNormalizerTests
    {
        [SetUp]    public void SetUp()    => ChipKindRegistry.ResetToBuiltIns();
        [TearDown] public void TearDown() => ChipKindRegistry.ResetToBuiltIns();

        private static ChipData H(string path, string name, int id = 0)
            => new ChipData(ChipKindKeys.Hierarchy, path, name, id);

        private static ChipData S(string path, string name)
            => new ChipData(ChipKindKeys.Script, path, name, 0);

        // 1. NoChips_Unchanged
        [Test]
        public void NoChips_Unchanged()
        {
            Assert.AreEqual("hello world", BareNameNormalizer.Normalize("hello world", null));
            Assert.AreEqual("hello world", BareNameNormalizer.Normalize("hello world", new List<ChipData>()));
        }

        // 2. BareName_ReplacedWithBracketTag
        [Test]
        public void BareName_ReplacedWithBracketTag()
        {
            var chips = new List<ChipData> { H("/Player", "Player", 1) };
            var result = BareNameNormalizer.Normalize("fix Player health", chips);
            StringAssert.Contains("[hierarchy:/Player #1]", result);
        }

        // 3. BareNameTwice_BothReplaced
        [Test]
        public void BareNameTwice_BothReplaced()
        {
            var chips = new List<ChipData> { H("/Player", "Player", 1) };
            var result = BareNameNormalizer.Normalize("Player hit Player", chips);
            int count = CountOccurrences(result, "[hierarchy:/Player #1]");
            Assert.AreEqual(2, count, $"Expected 2 replacements, got: {result}");
        }

        // 4. ExistingBracketTag_NotDoubled
        [Test]
        public void ExistingBracketTag_NotDoubled()
        {
            var chips = new List<ChipData> { H("/Player", "Player", 1) };
            var input  = "[hierarchy:/Player #1] is ready";
            var result = BareNameNormalizer.Normalize(input, chips);
            // Should not produce [[hierarchy:...]] or double-tag
            Assert.IsFalse(result.Contains("[["), $"Should not double-tag: {result}");
            Assert.AreEqual(1, CountOccurrences(result, "[hierarchy:/Player #1]"),
                $"Bracket tag should appear exactly once: {result}");
        }

        // 5. InsideCodeSpan_NotReplaced
        [Test]
        public void InsideCodeSpan_NotReplaced()
        {
            var chips = new List<ChipData> { H("/Player", "Player", 1) };
            var result = BareNameNormalizer.Normalize("use `Player` component", chips);
            StringAssert.Contains("`Player`", result);
            StringAssert.DoesNotContain("[hierarchy:/Player #1]", result);
        }

        // 6. CaseInsensitive_Matched
        [Test]
        public void CaseInsensitive_Matched()
        {
            var chips = new List<ChipData> { H("/Player", "Player", 1) };
            var result = BareNameNormalizer.Normalize("fix player now", chips);
            StringAssert.Contains("[hierarchy:/Player #1]", result);
        }

        // 7. WordBoundary_Respected
        [Test]
        public void WordBoundary_Respected()
        {
            var chips = new List<ChipData> { H("/Player", "Player", 1) };
            var result = BareNameNormalizer.Normalize("PlayerController is broken", chips);
            StringAssert.DoesNotContain("[hierarchy:/Player #1]", result);
            StringAssert.Contains("PlayerController", result);
        }

        // 8. LongestFirst_MultiWord
        [Test]
        public void LongestFirst_MultiWord()
        {
            var chips = new List<ChipData>
            {
                H("/Main",        "Main",        1),
                H("/Main Camera", "Main Camera", 2),
            };
            var result = BareNameNormalizer.Normalize("use Main Camera now", chips);
            StringAssert.Contains("[hierarchy:/Main Camera #2]", result);
            StringAssert.DoesNotContain("[hierarchy:/Main #1]", result);
        }

        // 9. SingleChar_Skipped
        [Test]
        public void SingleChar_Skipped()
        {
            var chips = new List<ChipData> { H("/A", "A", 1) };
            var result = BareNameNormalizer.Normalize("A is here", chips);
            // Single-char display names must be skipped
            StringAssert.DoesNotContain("[hierarchy:/A #1]", result);
        }

        // 10. NullText_ReturnsEmpty
        [Test]
        public void NullText_ReturnsEmpty()
        {
            var chips = new List<ChipData> { H("/Player", "Player", 1) };
            Assert.AreEqual("", BareNameNormalizer.Normalize(null, chips));
        }

        // 11. NonHierarchyChip_ScriptKind
        [Test]
        public void NonHierarchyChip_ScriptKind()
        {
            var chips = new List<ChipData> { S("Assets/Foo.cs", "Foo") };
            var result = BareNameNormalizer.Normalize("edit Foo script", chips);
            StringAssert.Contains("[script:Assets/Foo.cs]", result);
        }

        // 12. AdjacentToBracketTag_StillMatched
        [Test]
        public void AdjacentToBracketTag_StillMatched()
        {
            var chips = new List<ChipData> { H("/Player", "Player", 1) };
            var result = BareNameNormalizer.Normalize("Player near [hierarchy:/Enemy #2]", chips);
            StringAssert.Contains("[hierarchy:/Player #1]", result);
            StringAssert.Contains("[hierarchy:/Enemy #2]", result);
        }

        // 13. NameAtStartOfText
        [Test]
        public void NameAtStartOfText()
        {
            var chips = new List<ChipData> { H("/Player", "Player", 1) };
            var result = BareNameNormalizer.Normalize("Player is cool", chips);
            StringAssert.Contains("[hierarchy:/Player #1]", result);
        }

        // 14. NameAtEndOfText
        [Test]
        public void NameAtEndOfText()
        {
            var chips = new List<ChipData> { H("/Player", "Player", 1) };
            var result = BareNameNormalizer.Normalize("fix Player", chips);
            StringAssert.Contains("[hierarchy:/Player #1]", result);
        }

        // 15. NameWithUnderscore_WordBoundary
        [Test]
        public void NameWithUnderscore_WordBoundary()
        {
            var chips = new List<ChipData> { H("/Grid", "Grid", 1) };
            var result = BareNameNormalizer.Normalize("Grid_Floor is active", chips);
            // Underscore is a word char → should NOT match
            StringAssert.DoesNotContain("[hierarchy:/Grid #1]", result);
        }

        // ── helper ────────────────────────────────────────────────────────────

        private static int CountOccurrences(string text, string pattern)
        {
            int count = 0, idx = 0;
            while ((idx = text.IndexOf(pattern, idx, System.StringComparison.Ordinal)) >= 0)
            { count++; idx += pattern.Length; }
            return count;
        }
    }
}
