// Pure NUnit tests for ChatLinkify. No UnityEngine/UnityEditor deps.
using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class ChatLinkifyTests
    {
        // Resolver that matches any name starting with "GO_"
        private static string ResolveObj(string name) =>
            name.StartsWith("GO_") ? "/" + name : null;

        // Resolver that matches any name ending with ".cs"
        private static string ResolveScript(string name) =>
            name.EndsWith(".cs") ? "Assets/Scripts/" + name : null;

        private const string CodeOpen  = "<color=#9aa5ce>";
        private const string CodeClose = "</color>";

        private string Span(string inner) => CodeOpen + inner + CodeClose;

        // ── null / empty ─────────────────────────────────────────────────────

        [Test]
        public void NullInput_ReturnsNull()
        {
            var result = ChatLinkify.Apply(null, ResolveObj, ResolveScript);
            Assert.IsNull(result);
        }

        [Test]
        public void EmptyInput_ReturnsEmpty()
        {
            var result = ChatLinkify.Apply("", ResolveObj, ResolveScript);
            Assert.AreEqual("", result);
        }

        // ── no code spans ────────────────────────────────────────────────────

        [Test]
        public void NoCodeSpans_Unchanged()
        {
            var input = "Hello <b>world</b> foo";
            Assert.AreEqual(input, ChatLinkify.Apply(input, ResolveObj, ResolveScript));
        }

        // ── object resolved ──────────────────────────────────────────────────

        [Test]
        public void CodeSpan_ObjectResolved_WrapsLink()
        {
            var input  = "See " + Span("GO_Player") + " here";
            var result = ChatLinkify.Apply(input, ResolveObj, ResolveScript);

            Assert.IsTrue(result.Contains("<link=\"obj:/GO_Player\">"), $"Got: {result}");
            Assert.IsTrue(result.Contains("</link>"),                  $"Got: {result}");
            Assert.IsTrue(result.Contains("<u>"),                      $"Got: {result}");
            Assert.IsTrue(result.Contains(Span("GO_Player")),          $"Got: {result}");
        }

        // ── script resolved ──────────────────────────────────────────────────

        [Test]
        public void CodeSpan_ScriptResolved_WrapsLink()
        {
            var input  = "Edit " + Span("Player.cs") + " now";
            var result = ChatLinkify.Apply(input, ResolveObj, ResolveScript);

            Assert.IsTrue(result.Contains("<link=\"script:Assets/Scripts/Player.cs\">"), $"Got: {result}");
            Assert.IsTrue(result.Contains("</link>"), $"Got: {result}");
        }

        // ── unresolved ───────────────────────────────────────────────────────

        [Test]
        public void CodeSpan_Unresolved_Unchanged()
        {
            var input  = "Use " + Span("SomeUnknown") + " here";
            var result = ChatLinkify.Apply(input, ResolveObj, ResolveScript);

            Assert.AreEqual(input, result);
        }

        // ── object wins over script ──────────────────────────────────────────

        [Test]
        public void CodeSpan_ObjectPriority_OverScript()
        {
            // A name that matches BOTH resolvers — object wins.
            string BothResolveObj(string n)    => "/" + n;  // always resolves
            string BothResolveScript(string n) => "Assets/" + n; // also resolves

            var input  = Span("Foo");
            var result = ChatLinkify.Apply(input, BothResolveObj, BothResolveScript);

            Assert.IsTrue(result.Contains("<link=\"obj:/Foo\">"), $"Got: {result}");
            Assert.IsFalse(result.Contains("script:"),            $"Got: {result}");
        }

        // ── multiple spans ───────────────────────────────────────────────────

        [Test]
        public void MultipleCodeSpans_EachResolvedIndependently()
        {
            var input  = Span("GO_A") + " and " + Span("Unknown") + " and " + Span("B.cs");
            var result = ChatLinkify.Apply(input, ResolveObj, ResolveScript);

            Assert.IsTrue(result.Contains("<link=\"obj:/GO_A\">"),              $"Got: {result}");
            Assert.IsTrue(result.Contains(Span("Unknown")),                    $"Got: {result}");
            Assert.IsTrue(result.Contains("<link=\"script:Assets/Scripts/B.cs\">"), $"Got: {result}");
        }

        // ── noparse stripping for lookup ─────────────────────────────────────

        [Test]
        public void CodeSpan_WithNoparse_StripsForLookup()
        {
            // After MarkdownInline.Escape, "GO_<A>" becomes "GO_<noparse><</noparse>A>"
            // Resolver should see "GO_<A>".
            // We craft the already-escaped rich text:
            var escapedInner = "GO_<noparse><</noparse>A>";
            var input        = Span(escapedInner);

            string ObjResolver(string n) => n == "GO_<A>" ? "/GO_<A>" : null;
            var result = ChatLinkify.Apply(input, ObjResolver, _ => null);

            Assert.IsTrue(result.Contains("<link=\"obj:/GO_<A>\">"), $"Got: {result}");
        }

        // ── existing link tag not double-wrapped ──────────────────────────────

        [Test]
        public void ExistingLinkTag_NotDoubleWrapped()
        {
            var input  = "<link=\"obj:/Already\">" + Span("AlreadyLinked") + "</link>";
            var result = ChatLinkify.Apply(input, _ => "/AlreadyLinked", _ => null);

            // Should NOT gain another <link> around the already-linked span
            var linkCount = System.Text.RegularExpressions.Regex.Matches(result, "<link=").Count;
            Assert.AreEqual(1, linkCount, $"Got: {result}");
        }

        // ── StripNoparse helper ───────────────────────────────────────────────

        [Test]
        public void StripNoparse_Basic()
        {
            var input    = "foo<noparse><</noparse>bar";
            var expected = "foo<bar";
            Assert.AreEqual(expected, ChatLinkify.StripNoparse(input));
        }

        [Test]
        public void StripNoparse_Empty()
        {
            Assert.AreEqual("", ChatLinkify.StripNoparse(""));
        }

        [Test]
        public void StripNoparse_NoNoparse_Unchanged()
        {
            Assert.AreEqual("hello", ChatLinkify.StripNoparse("hello"));
        }
    }
}
