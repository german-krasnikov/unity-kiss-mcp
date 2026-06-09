// TDD — F29: drag/drop from external sources (Finder paths) and folder chips.
// Zero Unity deps for path tests — uses MCPChatWindow.ProcessExternalPath (static, testable).
using System.Collections.Generic;
using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class DragDropExternalTests
    {
        // ── ProcessExternalPath ───────────────────────────────────────────────

        [Test]
        public void ProcessExternalPath_NullPath_NoOp()
        {
            var calls = new List<(string path, string name)>();
            MCPChatWindow.ProcessExternalPath(null, (_, p, n) => calls.Add((p, n)));
            Assert.AreEqual(0, calls.Count);
        }

        [Test]
        public void ProcessExternalPath_EmptyPath_NoOp()
        {
            var calls = new List<(string path, string name)>();
            MCPChatWindow.ProcessExternalPath("", (_, p, n) => calls.Add((p, n)));
            Assert.AreEqual(0, calls.Count);
        }

        [Test]
        public void ProcessExternalPath_ValidFilePath_InsertsWithFilename()
        {
            var calls = new List<(string path, string name)>();
            MCPChatWindow.ProcessExternalPath("/Users/dev/MyScript.cs", (_, p, n) => calls.Add((p, n)));
            Assert.AreEqual(1, calls.Count);
            Assert.AreEqual("/Users/dev/MyScript.cs", calls[0].path);
            Assert.AreEqual("MyScript.cs",            calls[0].name);
        }

        [Test]
        public void ProcessExternalPath_DirectoryPath_InsertsWithDirName()
        {
            var calls = new List<(string path, string name)>();
            MCPChatWindow.ProcessExternalPath("/Users/dev/MyProject/", (_, p, n) => calls.Add((p, n)));
            Assert.AreEqual(1, calls.Count);
            Assert.AreEqual("/Users/dev/MyProject/", calls[0].path);
            // GetFileName on a trailing-slash path returns "" — code falls back to full path
            Assert.IsFalse(string.IsNullOrEmpty(calls[0].name));
        }

        [Test]
        public void ProcessExternalPath_PathWithNoSlash_UsePathAsName()
        {
            var calls = new List<(string path, string name)>();
            MCPChatWindow.ProcessExternalPath("justAFile", (_, p, n) => calls.Add((p, n)));
            Assert.AreEqual(1, calls.Count);
            Assert.AreEqual("justAFile", calls[0].name);
        }

        // ── ChipKindKeys.Folder constant ──────────────────────────────────────

        [Test]
        public void ChipKindKeys_HasFolderConstant()
        {
            Assert.AreEqual("folder", ChipKindKeys.Folder);
        }

        // External files pass null Object to insert delegate — should fall through to "asset" kind.
        [Test]
        public void ProcessExternalPath_NullObj_FallsToAssetKind()
        {
            var kind = ChipKindDetector.Detect(null, "/Users/test/file.txt");
            Assert.AreEqual(ChipKindKeys.Asset, kind);
        }

        // FolderChipProvider requires a non-null DefaultAsset — null obj must return false.
        [Test]
        public void FolderChipProvider_NullObj_ReturnsFalse()
        {
            ChipKindRegistry.ResetToBuiltIns();
            var provider = new FolderChipProvider();
            Assert.IsFalse(provider.CanHandle(null, "Assets/SomeFolder"));
        }
    }
}
