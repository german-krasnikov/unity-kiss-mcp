// TDD — Bug 3: scene objects in LLM responses rendered as pills, not underlined links.
// Pure headless: no Unity runtime dependency.
// Verifies BareNameNormalizer works with scene-object-derived ChipData (instanceID=0).
using System.Collections.Generic;
using NUnit.Framework;
using UnityMCP.Editor.Chat;
using static UnityMCP.Editor.Chat.Tests.TestStringHelpers;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class SceneObjectNormalizationTests
    {
        [SetUp]    public void SetUp()    => ChipKindRegistry.ResetToBuiltIns();
        [TearDown] public void TearDown() { ChipKindRegistry.ResetToBuiltIns(); ChipPillFactory.ColorResolver = null; }

        private static ChipData SceneChip(string name, string path)
            => new ChipData(ChipKindKeys.Hierarchy, path, name, 0);

        // SN1: bare name matching scene object → converted to [hierarchy:path] tag (instanceID=0 → no #id)
        [Test]
        public void SN1_BareSceneName_ConvertedToPillTag()
        {
            var chips  = new List<ChipData> { SceneChip("Grid", "/Grid") };
            var result = BareNameNormalizer.Normalize("The Grid is broken", chips);
            StringAssert.Contains("[hierarchy:/Grid]", result);
        }

        // SN2: name not in scene → left as plain text
        [Test]
        public void SN2_UnknownName_LeftAsText()
        {
            var chips  = new List<ChipData> { SceneChip("Grid", "/Grid") };
            var result = BareNameNormalizer.Normalize("The Floor is broken", chips);
            StringAssert.DoesNotContain("[hierarchy:", result);
            StringAssert.Contains("Floor", result);
        }

        // SN3: name already in [hierarchy:ref] tag → not double-converted
        [Test]
        public void SN3_AlreadyTagged_NotDoubleConverted()
        {
            var chips  = new List<ChipData> { SceneChip("Grid", "/Grid") };
            var input  = "[hierarchy:/Grid] needs fixing";
            var result = BareNameNormalizer.Normalize(input, chips);
            Assert.IsFalse(result.Contains("[["), $"Should not double-tag: {result}");
            var count = CountOccurrences(result, "[hierarchy:/Grid]");
            Assert.AreEqual(1, count, $"Expected exactly one tag: {result}");
        }

        // SN4: multiple scene objects in response → all converted (instanceID=0 → no #id suffix)
        [Test]
        public void SN4_MultipleSceneObjects_AllConverted()
        {
            var chips = new List<ChipData>
            {
                SceneChip("Grid",   "/Grid"),
                SceneChip("Player", "/Player"),
            };
            var result = BareNameNormalizer.Normalize("Grid and Player are selected", chips);
            StringAssert.Contains("[hierarchy:/Grid]",   result);
            StringAssert.Contains("[hierarchy:/Player]", result);
        }

        // SN5: single-char scene name → skipped by BareNameNormalizer
        [Test]
        public void SN5_SingleCharName_Skipped()
        {
            var chips  = new List<ChipData> { SceneChip("A", "/A") };
            var result = BareNameNormalizer.Normalize("A is here", chips);
            StringAssert.DoesNotContain("[hierarchy:/A]", result);
        }

        // SN6: scene object name in code block → not converted
        [Test]
        public void SN6_NameInCodeBlock_NotConverted()
        {
            var chips  = new List<ChipData> { SceneChip("Grid", "/Grid") };
            var text   = "```\nGrid.position = zero;\n```";
            var result = BareNameNormalizer.Normalize(text, chips);
            Assert.AreEqual(text, result);
        }

        // SN7: scene object + sent chip same name → sent chip instanceID preserved
        [Test]
        public void SN7_SentChipSameName_SentChipTakesPriority()
        {
            // First pass: normalize with sent chip (has real instanceID 42)
            var sentChips  = new List<ChipData> { new ChipData(ChipKindKeys.Hierarchy, "/Grid", "Grid", 42) };
            var afterSent  = BareNameNormalizer.Normalize("fix Grid now", sentChips);
            StringAssert.Contains("[hierarchy:/Grid #42]", afterSent);

            // Second pass: scene object normalization — already tagged, so not re-processed
            var sceneChips = new List<ChipData> { SceneChip("Grid", "/Grid") };
            var afterScene = BareNameNormalizer.Normalize(afterSent, sceneChips);
            // The [hierarchy:/Grid #42] is in a protected range → id preserved
            StringAssert.Contains("[hierarchy:/Grid #42]", afterScene);
            StringAssert.DoesNotContain("[hierarchy:/Grid]", afterScene);
        }

    }
}
