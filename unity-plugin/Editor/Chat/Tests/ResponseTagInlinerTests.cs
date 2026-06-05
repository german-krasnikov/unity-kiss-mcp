// TDD tests for ResponseTagInliner — pure unit, no Unity objects.
// H6: ExtractTags returns (string KindKey, string Ref) — no ChipKind enum.
// New: register custom kind → Apply matches with custom color + chip:KEY: linkId.
using NUnit.Framework;
using System.Collections.Generic;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class ResponseTagInlinerTests
    {
        [SetUp]    public void SetUp()    => ChipKindRegistry.ResetToBuiltIns();
        [TearDown] public void TearDown() => ChipKindRegistry.ResetToBuiltIns();

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
        public void Apply_HierarchyTag_LinkIdFormat_ChipPrefix()
        {
            var result = ResponseTagInliner.Apply("[hierarchy:/World/Player #1]");
            StringAssert.Contains("chip:hierarchy:/World/Player #1", result);
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
            StringAssert.DoesNotContain("[hierarchy:/Player #1]", result);
            StringAssert.DoesNotContain("[script:Foo]", result);
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
            const string text = "[hierarchy:]";
            Assert.AreEqual(text, ResponseTagInliner.Apply(text));
        }

        [Test]
        public void Apply_PreservesNonTagBrackets()
        {
            const string text = "[some text] and [another]";
            Assert.AreEqual(text, ResponseTagInliner.Apply(text));
        }

        [Test]
        public void Apply_MarkdownLink_NotMatched()
        {
            const string text = "[Click here](https://example.com)";
            Assert.AreEqual(text, ResponseTagInliner.Apply(text));
        }

        [Test]
        public void Apply_NestedBrackets_OnlyInnerKindMatched()
        {
            var result = ResponseTagInliner.Apply("[[hierarchy:x]]");
            StringAssert.DoesNotContain("[hierarchy:x]", result);
            StringAssert.Contains("hierarchy", result);
        }

        [Test]
        public void Apply_BracketWithoutColon_NotMatched()
        {
            const string text = "[word]";
            Assert.AreEqual(text, ResponseTagInliner.Apply(text));
        }

        // ── ExtractTags — string KindKey (H6) ────────────────────────────────

        [Test]
        public void ExtractTags_ReturnsParsedKindAndRef()
        {
            var tags = ResponseTagInliner.ExtractTags("[hierarchy:/Player #1] [script:Foo]");
            Assert.AreEqual(2, tags.Count);
            Assert.AreEqual(ChipKindKeys.Hierarchy, tags[0].KindKey);
            Assert.AreEqual("/Player #1",           tags[0].Ref);
            Assert.AreEqual(ChipKindKeys.Script,    tags[1].KindKey);
            Assert.AreEqual("Foo",                  tags[1].Ref);
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
            Assert.AreEqual(ChipKindKeys.Hierarchy,       tags[0].KindKey);
            Assert.AreEqual(ChipKindKeys.Scene,            tags[1].KindKey);
            Assert.AreEqual(ChipKindKeys.Script,           tags[2].KindKey);
            Assert.AreEqual(ChipKindKeys.Prefab,           tags[3].KindKey);
            Assert.AreEqual(ChipKindKeys.Material,         tags[4].KindKey);
            Assert.AreEqual(ChipKindKeys.Texture,          tags[5].KindKey);
            Assert.AreEqual(ChipKindKeys.ScriptableObject, tags[6].KindKey);
            Assert.AreEqual(ChipKindKeys.Asset,            tags[7].KindKey);
        }

        // ── Custom kind via registry ──────────────────────────────────────────

        [Test]
        public void Apply_CustomKind_MatchesAfterRegister()
        {
            // Register fake provider with key "test_kind"
            var fake = new TestKindProvider();
            ChipKindRegistry.Register(fake);
            var result = ResponseTagInliner.Apply("[test_kind:some/path]");
            StringAssert.Contains("test_kind", result);
            StringAssert.DoesNotContain("[test_kind:some/path]", result);
            StringAssert.Contains("chip:test_kind:some/path", result);
            StringAssert.Contains("#abcdef", result);
        }

        // Helper for above test
        private sealed class TestKindProvider : IChipKindProvider
        {
            public string Key          => "test_kind";
            public int    Priority     => 10;
            public string IconName     => "d_DefaultAsset Icon";
            public string HexColor     => "#abcdef";
            public string DefaultDepth => "path";
            public bool   CanHandle(UnityEngine.Object obj, string assetPath) => false;
            public ChipData Create(UnityEngine.Object obj, string assetPath) => default;
            public string FormatPayload(ChipData chip, ChipPayloadContext ctx) => $"[{Key}:{chip.Path}]";
            public void   Navigate(string reference) { }
        }
    }
}
