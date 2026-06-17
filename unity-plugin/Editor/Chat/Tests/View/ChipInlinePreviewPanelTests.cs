// TDD tests for ChipInlinePreviewPanel — toggle / lazy-build / fallback logic.
// Injects a custom InlinePreviewBuilder.TextureLoader seam to avoid file I/O.
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class ChipInlinePreviewPanelTests
    {
        const string AnyPath = "Assets/tex.png";

        [SetUp]
        public void SetUp()
        {
            InlinePreviewBuilder.TextureLoader = _ => Texture2D.whiteTexture;
        }

        [TearDown]
        public void TearDown()
        {
            InlinePreviewBuilder.TextureLoader = null;
        }

        // ── initial state ─────────────────────────────────────────────────────

        [Test]
        public void IsVisible_Initially_False()
        {
            var panel = new ChipInlinePreviewPanel(ChipKindKeys.Texture, AnyPath, null);
            Assert.IsFalse(panel.IsVisible);
        }

        // ── toggle show / hide ────────────────────────────────────────────────

        [Test]
        public void Toggle_FirstCall_ShowsPanel()
        {
            var panel = new ChipInlinePreviewPanel(ChipKindKeys.Texture, AnyPath, null);
            panel.Toggle();
            Assert.IsTrue(panel.IsVisible);
        }

        [Test]
        public void Toggle_SecondCall_HidesPanel()
        {
            var panel = new ChipInlinePreviewPanel(ChipKindKeys.Texture, AnyPath, null);
            panel.Toggle();
            panel.Toggle();
            Assert.IsFalse(panel.IsVisible);
        }

        [Test]
        public void Toggle_ThirdCall_ShowsAgain()
        {
            var panel = new ChipInlinePreviewPanel(ChipKindKeys.Texture, AnyPath, null);
            panel.Toggle();
            panel.Toggle();
            panel.Toggle();
            Assert.IsTrue(panel.IsVisible);
        }

        // ── lazy build: only once ─────────────────────────────────────────────

        [Test]
        public void Toggle_BuildsPreviewOnlyOnce()
        {
            int loaderCalls = 0;
            InlinePreviewBuilder.TextureLoader = _ => { loaderCalls++; return Texture2D.whiteTexture; };

            var panel = new ChipInlinePreviewPanel(ChipKindKeys.Texture, AnyPath, null);
            panel.Toggle(); // build + show
            panel.Toggle(); // hide (no rebuild)
            panel.Toggle(); // show (no rebuild)

            Assert.AreEqual(1, loaderCalls, "TextureLoader must be called only once — on first Toggle");
        }

        // ── fallback when preview unavailable ────────────────────────────────

        [Test]
        public void Toggle_WhenBuildReturnsNull_CallsFallback()
        {
            bool fallbackCalled = false;
            // "script" key → Build returns null
            var panel = new ChipInlinePreviewPanel(ChipKindKeys.Script, AnyPath,
                () => fallbackCalled = true);

            panel.Toggle();

            Assert.IsTrue(fallbackCalled, "fallback must be invoked when Build returns null");
        }

        [Test]
        public void Toggle_WhenBuildReturnsNull_PanelRemainsHidden()
        {
            var panel = new ChipInlinePreviewPanel(ChipKindKeys.Script, AnyPath, null);
            panel.Toggle();
            Assert.IsFalse(panel.IsVisible, "panel must stay hidden when Build returns null");
        }

        // ── CSS class ─────────────────────────────────────────────────────────

        [Test]
        public void Panel_HasChipInlinePreviewClass()
        {
            var panel = new ChipInlinePreviewPanel(ChipKindKeys.Texture, AnyPath, null);
            Assert.IsTrue(panel.ClassListContains("chip-inline-preview"));
        }
    }
}
