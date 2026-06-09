// TDD — AtMentionNormalizer tests (Group C).
// Pure headless: no Unity runtime dependency.
// Verifies Bug 2 fix: @Name in LLM response → [kind:ref] bracket tags.
using System.Collections.Generic;
using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class AtMentionNormalizerTests
    {
        [SetUp]    public void SetUp()    => ChipKindRegistry.ResetToBuiltIns();
        [TearDown] public void TearDown() => ChipKindRegistry.ResetToBuiltIns();

        private static ChipData H(string path, string name, int id = 0)
            => new ChipData(ChipKindKeys.Hierarchy, path, name, id);

        // C1: text with no @mentions → returned unchanged
        [Test]
        public void C1_NoAtMentions_Unchanged()
        {
            var chips = new List<ChipData> { H("/Player", "Player", 1) };
            var result = AtMentionNormalizer.Normalize("fix the health component", chips);
            Assert.AreEqual("fix the health component", result);
        }

        // C2: "@Player" with matching chip → replaced with [hierarchy:/Player #1]
        [Test]
        public void C2_AtMentionWithMatch_ReplacedWithBracketTag()
        {
            var chips = new List<ChipData> { H("/Player", "Player", 1) };
            var result = AtMentionNormalizer.Normalize("check @Player health", chips);
            StringAssert.Contains("[hierarchy:/Player #1]", result);
            StringAssert.DoesNotContain("@Player", result);
        }

        // C3: "@Player" with no matching chip → returned unchanged (no strip)
        [Test]
        public void C3_AtMentionNoMatch_Unchanged()
        {
            var chips = new List<ChipData> { H("/Enemy", "Enemy", 2) };
            var result = AtMentionNormalizer.Normalize("fix @Player health", chips);
            StringAssert.Contains("@Player", result);
        }

        // C4: text with both @mention and [kind:ref] → @mention normalized, tag unchanged
        [Test]
        public void C4_MixedAtAndTag_AtNormalized_TagUnchanged()
        {
            var chips = new List<ChipData> { H("/Player", "Player", 1) };
            var input  = "see @Player and [hierarchy:/Enemy #2]";
            var result = AtMentionNormalizer.Normalize(input, chips);
            StringAssert.Contains("[hierarchy:/Player #1]", result);
            StringAssert.Contains("[hierarchy:/Enemy #2]",  result);
        }

        // C5: two @mentions for same chip name → both replaced
        [Test]
        public void C5_TwoAtMentionsSameChip_BothReplaced()
        {
            var chips = new List<ChipData> { H("/Player", "Player", 1) };
            var result = AtMentionNormalizer.Normalize("@Player and @Player again", chips);
            Assert.AreEqual(0, CountOccurrences(result, "@Player"));
            Assert.AreEqual(2, CountOccurrences(result, "[hierarchy:/Player #1]"));
        }

        // C6: @mention with multi-word name → matched correctly
        [Test]
        public void C6_MultiWordName_Matched()
        {
            var chips = new List<ChipData> { H("/Main Camera", "Main Camera", -123) };
            var result = AtMentionNormalizer.Normalize("check @Main Camera view", chips);
            StringAssert.Contains("[hierarchy:/Main Camera #-123]", result);
        }

        // C7: case-insensitive match
        [Test]
        public void C7_CaseInsensitive_Matched()
        {
            var chips = new List<ChipData> { H("/Player", "Player", 1) };
            var result = AtMentionNormalizer.Normalize("fix @player now", chips);
            StringAssert.Contains("[hierarchy:/Player #1]", result);
        }

        // C8: null sentChips → returns text unchanged
        [Test]
        public void C8_NullSentChips_Unchanged()
        {
            var result = AtMentionNormalizer.Normalize("fix @Player health", null);
            Assert.AreEqual("fix @Player health", result);
        }

        // Extra: empty sentChips → text unchanged
        [Test]
        public void Extra_EmptySentChips_Unchanged()
        {
            var result = AtMentionNormalizer.Normalize("fix @Player", new List<ChipData>());
            Assert.AreEqual("fix @Player", result);
        }

        // Extra: null text → null returned
        [Test]
        public void Extra_NullText_NullReturned()
        {
            var chips = new List<ChipData> { H("/Player", "Player", 1) };
            Assert.IsNull(AtMentionNormalizer.Normalize(null, chips));
        }

        // Extra: longest-first prevents partial match
        // @Main matches "Main" only if "Main Camera" chip is absent
        [Test]
        public void Extra_LongestFirst_MainCameraNotMatchedByMain()
        {
            // If we have "Main Camera" chip, "@Main Camera" should match, "@Main" should not
            var chips = new List<ChipData> { H("/Main Camera", "Main Camera", -1) };
            var result = AtMentionNormalizer.Normalize("see @Main Camera ok", chips);
            StringAssert.Contains("[hierarchy:/Main Camera #-1]", result);
        }

        // C_Disambig: both "Main" and "Main Camera" present — longest-first prevents "@Main Camera" matching "Main"
        [Test]
        public void BothMainAndMainCamera_LongestFirstDisambiguates()
        {
            var chips = new List<ChipData>
            {
                H("/Main",        "Main",        1),
                H("/Main Camera", "Main Camera", 2)
            };
            var r = AtMentionNormalizer.Normalize("see @Main Camera and @Main here", chips);
            StringAssert.Contains("[hierarchy:/Main Camera #2]", r);
            StringAssert.Contains("[hierarchy:/Main #1]", r);
            StringAssert.DoesNotContain("@Main", r);
        }

        // ── FIX 1: full-path echo tests ──────────────────────────────────────

        // P1: LLM echoes full path — @/GridPlayer → bracket
        [Test]
        public void P1_FullPathEcho_HierarchyChip_ReplacedWithBracket()
        {
            var chips = new List<ChipData> { H("/GridPlayer", "GridPlayer", 5) };
            var result = AtMentionNormalizer.Normalize("Check @/GridPlayer now", chips);
            StringAssert.Contains("[hierarchy:/GridPlayer #5]", result);
            StringAssert.DoesNotContain("@/GridPlayer", result);
        }

        // P2: full path with spaces consumed fully; DisplayName chip also present (longest-first)
        [Test]
        public void P2_FullPathWithSpaces_LongestFirstOverDisplayName()
        {
            var chips = new List<ChipData>
            {
                H("/UI Canvas/Main Camera", "Main Camera", 7),
            };
            var result = AtMentionNormalizer.Normalize("see @/UI Canvas/Main Camera ok", chips);
            StringAssert.Contains("[hierarchy:/UI Canvas/Main Camera #7]", result);
            StringAssert.DoesNotContain("@/UI Canvas/Main Camera", result);
        }

        // P3: plain DisplayName echo still normalizes (regression guard)
        [Test]
        public void P3_DisplayNameEcho_StillNormalized()
        {
            var chips = new List<ChipData> { H("/GridPlayer", "GridPlayer", 5) };
            var result = AtMentionNormalizer.Normalize("fix @GridPlayer health", chips);
            StringAssert.Contains("[hierarchy:/GridPlayer #5]", result);
            StringAssert.DoesNotContain("@GridPlayer", result);
        }

        // P4: script chip (asset path, no leading '/') — @Assets/Foo.cs → bracket
        [Test]
        public void P4_ScriptPathEcho_ReplacedWithBracket()
        {
            var chip   = new ChipData(ChipKindKeys.Script, "Assets/Foo.cs", "Foo", 0);
            var chips  = new List<ChipData> { chip };
            var result = AtMentionNormalizer.Normalize("open @Assets/Foo.cs please", chips);
            // Script chips have no instanceID so FormatChipRef emits [script:Assets/Foo.cs]
            StringAssert.Contains("[script:Assets/Foo.cs]", result);
            StringAssert.DoesNotContain("@Assets/Foo.cs", result);
        }

        // P5: both @/FullPath and @DisplayName in same response — both replaced
        [Test]
        public void P5_BothFullPathAndDisplayName_BothReplaced()
        {
            var chips = new List<ChipData> { H("/GridPlayer", "GridPlayer", 5) };
            var result = AtMentionNormalizer.Normalize(
                "check @/GridPlayer and also @GridPlayer again", chips);
            Assert.AreEqual(0, CountOccurrences(result, "@/GridPlayer"));
            Assert.AreEqual(0, CountOccurrences(result, "@GridPlayer"));
            Assert.AreEqual(2, CountOccurrences(result, "[hierarchy:/GridPlayer #5]"));
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
