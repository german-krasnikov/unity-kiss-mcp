// TDD tests for P3: SceneNameLinker — auto-links known scene object names in AI responses.
// Pure headless tests — no UnityEditor/UnityEngine deps.
using System.Collections.Generic;
using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class SceneNameLinkerTests
    {
        [TearDown] public void TearDown() => MarkdownInline.Linker = null;

        private static SceneNameLinker Make(params (string name, string path)[] entries)
        {
            var d = new Dictionary<string, string>();
            foreach (var (n, p) in entries) d[n] = p;
            return new SceneNameLinker(d);
        }

        // ── ShouldAutoLink ────────────────────────────────────────────────────

        [Test]
        public void Test_ShouldAutoLink_ShortName_Rejected()
            => Assert.IsFalse(SceneNameLinker.ShouldAutoLink("Go"));

        [Test]
        public void Test_ShouldAutoLink_GenericName_Rejected()
            => Assert.IsFalse(SceneNameLinker.ShouldAutoLink("Canvas"));

        [Test]
        public void Test_ShouldAutoLink_NameWithDigit_Accepted()
            => Assert.IsTrue(SceneNameLinker.ShouldAutoLink("Player1"));

        [Test]
        public void Test_ShouldAutoLink_NameWithUnderscore_Accepted()
            => Assert.IsTrue(SceneNameLinker.ShouldAutoLink("Main_Camera"));

        [Test]
        public void Test_ShouldAutoLink_ConsecUppercase_Accepted()
            => Assert.IsTrue(SceneNameLinker.ShouldAutoLink("NPCSpawner"));

        [Test]
        public void Test_ShouldAutoLink_AllLowerNoSpecial_Rejected()
            => Assert.IsFalse(SceneNameLinker.ShouldAutoLink("player"));

        [Test]
        public void Test_ShouldAutoLink_SingleUpperOnly_Rejected()
            // "Player" is in skip list; but even without that, no digit/underscore/consecUpper
            => Assert.IsFalse(SceneNameLinker.ShouldAutoLink("Player"));

        // ── Linkify ───────────────────────────────────────────────────────────

        [Test]
        public void Test_Linkify_KnownName_WrappedInLink()
        {
            var linker = Make(("Player1", "/Player1"));
            var result = linker.Linkify("see Player1 here");
            StringAssert.Contains("<link=\"chip:hierarchy:/Player1\">", result);
            StringAssert.Contains("<u>Player1</u>", result);
        }

        [Test]
        public void Test_Linkify_UnknownName_PassedThrough()
        {
            var linker = Make(("Player1", "/Player1"));
            var result = linker.Linkify("RandomWord");
            Assert.AreEqual("RandomWord", result);
        }

        [Test]
        public void Test_Linkify_InsideExistingLink_NotLinked()
        {
            var linker = Make(("Player1", "/Player1"));
            var input  = "<link=\"obj:/Player1\">Player1</link>";
            var result = linker.Linkify(input);
            // Should have exactly 1 link (the original), not 2.
            Assert.AreEqual(1, CountOccurrences(result, "<link="),
                $"Existing link must not be re-linked. Got: {result}");
        }

        [Test]
        public void Test_Linkify_MultipleNames_AllLinked()
        {
            var linker = Make(("Player1", "/Player1"), ("NPC_02", "/NPC_02"));
            var result = linker.Linkify("Player1 and NPC_02");
            StringAssert.Contains("<link=\"chip:hierarchy:/Player1\">", result);
            StringAssert.Contains("<link=\"chip:hierarchy:/NPC_02\">", result);
        }

        [Test]
        public void Test_Linkify_EmptyText_ReturnsEmpty()
        {
            var linker = Make(("Player1", "/Player1"));
            Assert.AreEqual("", linker.Linkify(""));
        }

        [Test]
        public void Test_Linkify_NullText_ReturnsNull()
        {
            var linker = Make(("Player1", "/Player1"));
            Assert.IsNull(linker.Linkify(null));
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
