// TDD tests for InlineImageThumbnail (pure logic) and SplitLineWithThumbs.
// All headless: missing-file paths → Label fallback (no Texture2D/GPU calls).
using NUnit.Framework;
using System.IO;
using System.Linq;
using UnityEngine.UIElements;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class InlineImageThumbnailTests
    {
        // ── IsImagePath ───────────────────────────────────────────────────────

        [Test]
        public void IsImagePath_AbsolutePng_True()
            => Assert.IsTrue(InlineImageThumbnail.IsImagePath("/path/to/shot.png"));

        [Test]
        public void IsImagePath_AbsoluteJpg_True()
            => Assert.IsTrue(InlineImageThumbnail.IsImagePath("/path/to/photo.jpg"));

        [Test]
        public void IsImagePath_RelativePng_True()
            => Assert.IsTrue(InlineImageThumbnail.IsImagePath("Screenshots/img.png"));

        [Test]
        public void IsImagePath_BacktickWrapped_True()
            => Assert.IsTrue(InlineImageThumbnail.IsImagePath("`/path/shot.png`"));

        [Test]
        public void IsImagePath_CSharpFile_False()
            => Assert.IsFalse(InlineImageThumbnail.IsImagePath("/path/Script.cs"));

        [Test]
        public void IsImagePath_PlainWord_False()
            => Assert.IsFalse(InlineImageThumbnail.IsImagePath("hello"));

        [Test]
        public void IsImagePath_Null_False()
            => Assert.IsFalse(InlineImageThumbnail.IsImagePath(null));

        [Test]
        public void IsImagePath_Empty_False()
            => Assert.IsFalse(InlineImageThumbnail.IsImagePath(""));

        // BUG 2: missing extensions — these FAIL until IsImageFile is extended
        [Test]
        public void IsImagePath_Webp_ReturnsTrue()
            => Assert.IsTrue(InlineImageThumbnail.IsImagePath("/path/img.webp"));

        [Test]
        public void IsImagePath_Tiff_ReturnsTrue()
            => Assert.IsTrue(InlineImageThumbnail.IsImagePath("/path/img.tiff"));

        [Test]
        public void IsImagePath_Tif_ReturnsTrue()
            => Assert.IsTrue(InlineImageThumbnail.IsImagePath("/path/img.tif"));

        // ── Build (missing file → placeholder Label) ──────────────────────────

        [Test]
        public void Build_MissingFile_ReturnsMissingLabel()
        {
            var result = InlineImageThumbnail.Build("/nonexistent/path/shot.png");
            Assert.IsNotNull(result);
            Assert.IsInstanceOf<Label>(result);
            Assert.IsTrue(((Label)result).ClassListContains("md-image-thumb--missing"));
        }

        [Test]
        public void Build_NonImageExtension_ReturnsMissingLabel()
        {
            var result = InlineImageThumbnail.Build("/path/file.txt");
            Assert.IsNotNull(result);
            Assert.IsInstanceOf<Label>(result);
        }

        // ── SplitLineWithThumbs ───────────────────────────────────────────────

        [Test]
        public void SplitLine_NoImages_SingleLabel()
        {
            var elements = MixedParagraphRenderer.SplitLineWithThumbs("hello world foo").ToList();
            Assert.AreEqual(1, elements.Count);
            Assert.IsInstanceOf<Label>(elements[0]);
        }

        [Test]
        public void SplitLine_Empty_Empty()
        {
            var elements = MixedParagraphRenderer.SplitLineWithThumbs("").ToList();
            Assert.AreEqual(0, elements.Count);
        }

        [Test]
        public void SplitLine_PathAtEnd_ThumbnailLast()
        {
            var elements = MixedParagraphRenderer.SplitLineWithThumbs("See this: /some/shot.png").ToList();
            Assert.AreEqual(2, elements.Count);
            Assert.IsInstanceOf<Label>(elements[0]);
            Assert.IsInstanceOf<Label>(elements[1]); // missing file → placeholder Label
        }

        [Test]
        public void SplitLine_PathAtStart_ThumbnailFirst()
        {
            var elements = MixedParagraphRenderer.SplitLineWithThumbs("/some/shot.png and text").ToList();
            Assert.AreEqual(2, elements.Count);
        }

        [Test]
        public void SplitLine_TwoPathsInOneLine_TwoThumbnails()
        {
            var elements = MixedParagraphRenderer.SplitLineWithThumbs("/a/b.png text /c/d.jpg").ToList();
            // 2 thumbnails + 1 label (middle text)
            Assert.AreEqual(3, elements.Count);
        }

        [Test]
        public void SplitLine_MissingFile_PlaceholderNotNull()
        {
            var elements = MixedParagraphRenderer.SplitLineWithThumbs("/nonexistent/shot.png").ToList();
            Assert.AreEqual(1, elements.Count);
            Assert.IsNotNull(elements[0]);
        }
    }
}
