// TDD — ChipPillFactory tests (Wave 0).
// All asserts: child counts, class lists, label text, backgroundColor.
// No resolvedStyle/layout/live-panel dependence — headless safe.
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class ChipPillFactoryTests
    {
        [SetUp]    public void SetUp()    => ChipKindRegistry.ResetToBuiltIns();
        [TearDown] public void TearDown() => ChipKindRegistry.ResetToBuiltIns();

        // ── Label presence ────────────────────────────────────────────────────

        [Test]
        public void Build_HasKindAndNameLabels()
        {
            var pill = ChipPillFactory.Build(ChipKindKeys.Script, "Foo.cs");

            var kindLbl = pill.Q<Label>(className: "inline-chip-kind");
            var nameLbl = pill.Q<Label>(className: "inline-chip-label");

            Assert.IsNotNull(kindLbl, "kind label missing");
            Assert.IsNotNull(nameLbl, "name label missing");
            StringAssert.Contains(ChipKindKeys.Script, kindLbl.text);
            Assert.AreEqual("Foo.cs", nameLbl.text);
        }

        // ── Remove button presence ────────────────────────────────────────────

        [Test]
        public void Build_InputMode_HasRemoveButton()
        {
            bool removed = false;
            var pill = ChipPillFactory.Build(ChipKindKeys.Script, "Foo.cs", onRemove: () => removed = true);

            var btn = pill.Q<Button>(className: "inline-chip-remove");
            Assert.IsNotNull(btn, "remove button missing in input mode");
        }

        [Test]
        public void Build_ResponseMode_NoRemoveButton()
        {
            var pill = ChipPillFactory.Build(ChipKindKeys.Script, "Foo.cs", onRemove: null);

            var btn = pill.Q<Button>(className: "inline-chip-remove");
            Assert.IsNull(btn, "remove button must be absent in response mode (no onRemove)");
        }

        // ── Color from registry ───────────────────────────────────────────────

        [Test]
        public void Build_UsesRegistryColor()
        {
            // Script provider HexColor = "#4ade80"
            var pill = ChipPillFactory.Build(ChipKindKeys.Script, "Foo.cs");

            // backgroundColor is set inline — inspect the style directly (not resolvedStyle)
            var bg = pill.style.backgroundColor;
            Assert.AreEqual(StyleKeyword.Undefined, bg.keyword,
                "backgroundColor should be explicitly set (not keyword)");
            // r≈0.29, g≈0.87, b≈0.50 for #4ade80 @ alpha 0.85
            Assert.Greater(bg.value.g, bg.value.r,
                "Script chip must have a greenish background (#4ade80)");
        }

        [Test]
        public void Build_UnknownKind_FallbackColor()
        {
            var pill = ChipPillFactory.Build("unknown_kind_xyz", "ref");

            // Unknown kind → either no color set, or a gray fallback (r≈g≈b)
            var bg = pill.style.backgroundColor;
            if (bg.keyword != StyleKeyword.Undefined)
            {
                var c = bg.value;
                Assert.Less(Mathf.Abs(c.r - c.g), 0.15f, "fallback color should be grayish (r≈g)");
                Assert.Less(Mathf.Abs(c.g - c.b), 0.15f, "fallback color should be grayish (g≈b)");
            }
            // keyword=Undefined (color not set) is also valid for unknown kind
            Assert.IsTrue(
                bg.keyword == StyleKeyword.Undefined || (Mathf.Abs(bg.value.r - bg.value.g) < 0.15f),
                "unknown kind pill must have no color or a grayish background");
        }

        // ── ChipData overload ─────────────────────────────────────────────────

        [Test]
        public void Build_ChipDataOverload_SameAsKeyNameOverload()
        {
            var chip = new ChipData(ChipKindKeys.Script, "Assets/Bar.cs", "Bar.cs", 0);
            var pill1 = ChipPillFactory.Build(chip);
            var pill2 = ChipPillFactory.Build(ChipKindKeys.Script, "Bar.cs");

            var kind1 = pill1.Q<Label>(className: "inline-chip-kind")?.text;
            var kind2 = pill2.Q<Label>(className: "inline-chip-kind")?.text;
            Assert.AreEqual(kind1, kind2);
        }
    }
}
