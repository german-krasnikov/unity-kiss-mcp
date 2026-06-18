// [UnityTest] screenshot tests — open a real MCPChatWindow, capture screenshots.
// Requires UnityEngine.TestRunner (Play Mode or Editor coroutines).
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.TestTools;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class ChatWindowScreenshotTests
    {
        private MCPChatWindow _window;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            if (Application.isBatchMode)
            {
                Assert.Ignore("Screenshot tests require a GUI; skipping in batch mode.");
                yield break;
            }

            _window = EditorWindow.GetWindow<MCPChatWindow>("MCP Chat Test");
            _window.minSize = new Vector2(400, 600);
            _window.position = new Rect(100, 100, 400, 600);
            _window.Show();
            _window.Focus();
            yield return null;
            yield return null;
            yield return null;
        }

        [TearDown]
        public void TearDown()
        {
            if (_window != null) _window.Close();
        }

        [UnityTest]
        public IEnumerator Screenshot_EmptyWindow()
        {
            yield return null;
            var path = CaptureWindow(_window, "empty_chat");
            Assert.IsTrue(File.Exists(path), $"Screenshot not saved at {path}");
            Assert.Greater(new FileInfo(path).Length, 1000, "PNG too small");
        }

        [UnityTest]
        public IEnumerator Screenshot_WithChipsAdded()
        {
            _window.InsertInlineChip(null, "/Player", "Player");
            _window.InsertInlineChip(null, "/Enemy",  "Enemy");
            yield return null;
            yield return null;
            var path = CaptureWindow(_window, "chips_added");
            Assert.IsTrue(File.Exists(path));
        }

        // ── utility ───────────────────────────────────────────────────────────

        private static string CaptureWindow(EditorWindow window, string prefix)
        {
            float scale = EditorGUIUtility.pixelsPerPoint;
            var pos = window.position;
            int w = (int)(pos.width  * scale);
            int h = (int)(pos.height * scale);

            var pixels = InternalEditorUtility.ReadScreenPixel(pos.position, w, h);
            var tex    = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels(pixels);
            tex.Apply();
            var png = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);

            var dir      = Path.Combine(Application.dataPath, "..", "ScreenShots");
            Directory.CreateDirectory(dir);
            var filename = $"{System.DateTime.Now:yyyy-MM-dd_HH-mm-ss}_{prefix}.png";
            var path     = Path.Combine(dir, filename);
            File.WriteAllBytes(path, png);
            TestContext.WriteLine($"Screenshot saved: {path}");
            return path;
        }
    }
}
