// TDD tests for ResponseTagInliner — pure unit, no Unity objects.
// Focuses heavily on false-positive safety.
using NUnit.Framework;
using System.Collections.Generic;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class ResponseTagInlinerTests
    {
        // ── Basic: no tags ────────────────────────────────────────────────────

        [Test]
        public void Apply_NoTags_ReturnsUnchanged()
        {
            const string text = "Hello world, no special brackets here.";
            Assert.AreEqual(text, ResponseTagInliner.Apply(text));
        }

        [Test]
        public void Apply_Null_ReturnsNull()
        {
            Assert.IsNull(ResponseTagInliner.Apply(null));
        }

        [Test]
        public void Apply_Empty_ReturnsEmpty()
        {
            Assert.AreEqual("", ResponseTagInliner.Apply(""));
        }

        // ── Known kinds recognized ─────────────────────────────────────────────

        [Test]
        public void Apply_HierarchyTag_ReplacedWithPill()
        {
            var result = ResponseTagInliner.Apply("[hierarchy:/World/Player #12345]");
            StringAssert.Contains("hierarchy", result);
            StringAssert.Contains("/World/Player #12345", result);
            StringAssert.DoesNotContain("[hierarchy:/World/Player #12345]", result);
        }

        [Test]
        public void Apply_ScriptTag_ReplacedWithPill()
        {
            var result = ResponseTagInliner.Apply("[script:PlayerController]");
            StringAssert.Contains("script", result);
            StringAssert.Contains("PlayerController", result);
            StringAssert.DoesNotContain("[script:PlayerController]", result);
        }

        [Test]
        public void Apply_SceneTag_ReplacedWithPill()
        {
            var result = ResponseTagInliner.Apply("[scene:Assets/Scenes/Main.unity]");
            StringAssert.Contains("scene", result);
            StringAssert.Contains("Assets/Scenes/Main.unity", result);
        }

        [Test]
        public void Apply_MultipleTagsSameLine_AllReplaced()
        {
            var input  = "Check [hierarchy:/Player #1] and [script:Foo]";
            var result = ResponseTagInliner.Apply(input);
            // Both original bracket forms gone
            StringAssert.DoesNotContain("[hierarchy:/Player #1]", result);
            StringAssert.DoesNotContain("[script:Foo]", result);
            // Both refs present
            StringAssert.Contains("/Player #1", result);
            StringAssert.Contains("Foo", result);
        }

        // ── False-positive safety ─────────────────────────────────────────────

        [Test]
        public void Apply_UnknownKind_NotMatched()
        {
            const string text = "[foobar:something]";
            Assert.AreEqual(text, ResponseTagInliner.Apply(text));
        }

        [Test]
        public void Apply_EmptyRef_NotMatched()
        {
            // [hierarchy:] has empty ref — should NOT match (regex requires [^\]]+)
            const string text = "[hierarchy:]";
            Assert.AreEqual(text, ResponseTagInliner.Apply(text));
        }

        [Test]
        public void Apply_PreservesNonTagBrackets()
        {
            // Bare word in brackets without colon+kind → unchanged
            const string text = "[some text] and [another]";
            Assert.AreEqual(text, ResponseTagInliner.Apply(text));
        }

        [Test]
        public void Apply_MarkdownLink_NotMatched()
        {
            // Markdown [text](url) must NOT be eaten — different syntax
            const string text = "[Click here](https://example.com)";
            Assert.AreEqual(text, ResponseTagInliner.Apply(text));
        }

        [Test]
        public void Apply_NestedBrackets_OnlyInnerKindMatched()
        {
            // [[hierarchy:x]] — outer brackets are NOT part of the pattern; inner [hierarchy:x] is matched
            var result = ResponseTagInliner.Apply("[[hierarchy:x]]");
            // The inner tag [hierarchy:x] should be replaced; the outer ] left as-is
            StringAssert.DoesNotContain("[hierarchy:x]", result);
            StringAssert.Contains("hierarchy", result);
        }

        [Test]
        public void Apply_BracketWithoutColon_NotMatched()
        {
            // [word] with no colon → not a kind:ref pattern
            const string text = "[word]";
            Assert.AreEqual(text, ResponseTagInliner.Apply(text));
        }

        // ── ExtractTags ───────────────────────────────────────────────────────

        [Test]
        public void ExtractTags_ReturnsParsedKindAndRef()
        {
            var tags = ResponseTagInliner.ExtractTags("[hierarchy:/Player #1] [script:Foo]");
            Assert.AreEqual(2, tags.Count);
            Assert.AreEqual(ChipKind.Hierarchy, tags[0].Kind);
            Assert.AreEqual("/Player #1",       tags[0].Ref);
            Assert.AreEqual(ChipKind.Script,    tags[1].Kind);
            Assert.AreEqual("Foo",              tags[1].Ref);
        }

        [Test]
        public void ExtractTags_Empty_ReturnsEmptyList()
        {
            var tags = ResponseTagInliner.ExtractTags("plain text");
            Assert.AreEqual(0, tags.Count);
        }

        [Test]
        public void ExtractTags_AllKinds_ParsedCorrectly()
        {
            var input = "[hierarchy:h] [scene:s] [script:sc] [prefab:p] [material:m] [texture:t] [so:so] [asset:a]";
            var tags = ResponseTagInliner.ExtractTags(input);
            Assert.AreEqual(8, tags.Count);
            Assert.AreEqual(ChipKind.Hierarchy,       tags[0].Kind);
            Assert.AreEqual(ChipKind.Scene,            tags[1].Kind);
            Assert.AreEqual(ChipKind.Script,           tags[2].Kind);
            Assert.AreEqual(ChipKind.Prefab,           tags[3].Kind);
            Assert.AreEqual(ChipKind.Material,         tags[4].Kind);
            Assert.AreEqual(ChipKind.Texture,          tags[5].Kind);
            Assert.AreEqual(ChipKind.ScriptableObject, tags[6].Kind);
            Assert.AreEqual(ChipKind.Asset,            tags[7].Kind);
        }
    }
}
