// TDD tests for InlinePreviewBuilder — pure headless, no file I/O.
// TextureLoader seam prevents disk access in tests.
using NUnit.Framework;
using System;
using UnityEngine;
using UnityEngine.UIElements;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class InlinePreviewBuilderTests
    {
        const string AnyPath = "Assets/tex.png";

        [SetUp]
        public void SetUp()
        {
            // Inject stub loader: always returns a 1x1 white texture
            InlinePreviewBuilder.TextureLoader = _ => Texture2D.whiteTexture;
        }

        [TearDown]
        public void TearDown()
        {
            InlinePreviewBuilder.TextureLoader = null;
        }

        // ── null / empty path ─────────────────────────────────────────────────

        [Test]
        public void Build_NullPath_ReturnsNull()
            => Assert.IsNull(InlinePreviewBuilder.Build("texture", null));

        [Test]
        public void Build_EmptyPath_ReturnsNull()
            => Assert.IsNull(InlinePreviewBuilder.Build("texture", ""));

        // ── texture / image ───────────────────────────────────────────────────

        [Test]
        public void Build_TextureKey_ReturnsElementWithImageChild()
        {
            var ve = InlinePreviewBuilder.Build(ChipKindKeys.Texture, AnyPath);
            Assert.IsNotNull(ve);
            Assert.IsNotNull(ve.Q<Image>(), "texture preview must contain an Image element");
        }

        [Test]
        public void Build_ImageKey_ReturnsElementWithImageChild()
        {
            var ve = InlinePreviewBuilder.Build(ChipKindKeys.Image, AnyPath);
            Assert.IsNotNull(ve);
            Assert.IsNotNull(ve.Q<Image>(), "image preview must contain an Image element");
        }

        // ── model / prefab ────────────────────────────────────────────────────

        [Test]
        public void Build_ModelKey_ReturnsNonNull()
        {
            var ve = InlinePreviewBuilder.Build(ChipKindKeys.Model, AnyPath);
            Assert.IsNotNull(ve);
        }

        [Test]
        public void Build_PrefabKey_ReturnsNonNull()
        {
            var ve = InlinePreviewBuilder.Build(ChipKindKeys.Prefab, AnyPath);
            Assert.IsNotNull(ve);
        }

        // ── audio ─────────────────────────────────────────────────────────────

        [Test]
        public void Build_AudioKey_ContainsLabel()
        {
            var ve = InlinePreviewBuilder.Build(ChipKindKeys.Audio, AnyPath);
            Assert.IsNotNull(ve);
            Assert.IsNotNull(ve.Q<Label>(), "audio preview must contain a Label");
        }

        // ── unknown key ───────────────────────────────────────────────────────

        [Test]
        public void Build_UnknownKey_ReturnsNull()
            => Assert.IsNull(InlinePreviewBuilder.Build(ChipKindKeys.Script, AnyPath));

        // ── TextureLoader seam ────────────────────────────────────────────────

        [Test]
        public void Build_TextureKey_InvokesTextureLoaderSeam()
        {
            int callCount = 0;
            InlinePreviewBuilder.TextureLoader = path => { callCount++; return Texture2D.whiteTexture; };

            InlinePreviewBuilder.Build(ChipKindKeys.Texture, AnyPath);

            Assert.AreEqual(1, callCount, "TextureLoader seam must be called once for texture key");
        }
    }
}
