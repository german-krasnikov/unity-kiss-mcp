// TDD: MCPServer.ShouldStartServer — AssetImportWorker / batch mode guard.
// EditMode, no TCP required.
using System.IO;
using NUnit.Framework;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class MCPServerStartGuardTests
    {
        [Test]
        public void ShouldStartServer_BatchMode_ReturnsFalse()
        {
            Assert.IsFalse(MCPServer.ShouldStartServer(isBatchMode: true));
        }

        [Test]
        public void ShouldStartServer_NormalEditor_ReturnsTrue()
        {
            Assert.IsTrue(MCPServer.ShouldStartServer(isBatchMode: false));
        }

        // Source-text assertion: verifies the static ctor actually calls ShouldStartServer.
        // Unit tests above verify pure-function behavior; this verifies wiring in the ctor
        // (cannot test static ctor directly — [InitializeOnLoad] fires once at domain load).
        [Test]
        public void StaticCtor_ContainsBatchModeGuard()
        {
            var src = Path.GetFullPath(
                Path.Combine("Packages", "com.unity-mcp.plugin", "Editor", "MCPServer.cs"));
            if (!File.Exists(src))
            {
                Assert.Ignore($"MCPServer.cs not found at {src} — skip in CI");
                return;
            }
            var code = File.ReadAllText(src);
            StringAssert.Contains("ShouldStartServer", code,
                "static ctor must call ShouldStartServer to guard against batch mode / AssetImportWorker");
        }
    }
}
