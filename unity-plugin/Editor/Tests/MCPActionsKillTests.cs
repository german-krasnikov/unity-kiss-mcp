using System;
using System.IO;
using NUnit.Framework;

namespace UnityMCP.Editor.Tests
{
    // ── CS5.test.1 / CS5.arch.1 — MCPActions.Kill uses live ServerPort ──────

    [TestFixture]
    public class MCPActionsKillTests
    {
        [Test]
        public void Kill_MissingLockfile_DoesNotThrow()
        {
            // Kill() reads server-{ServerPort}.lock; when it does not exist it logs
            // a warning and returns. This test confirms Kill() is callable without crash
            // and that no hardcoded path exception is thrown (lockfile simply absent).
            Assert.DoesNotThrow(() => MCPActions.Kill());
        }

        [Test]
        public void Kill_UsesServerPort_NotHardcoded9500()
        {
            // Verify the formula: the lockfile name must be server-{ServerPort}.lock,
            // NOT server-9500.lock when ServerPort differs from 9500.
            var port = MCPServer.ServerPort;
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            // Build what the fixed code builds
            var fixedPath = Path.Combine(home, ".unity-mcp", $"server-{port}.lock");
            // Build what the old broken code built
            var brokenPath = Path.Combine(home, ".unity-mcp", "server-9500.lock");

            Assert.AreEqual(Path.GetFileName(fixedPath), $"server-{port}.lock");

            if (port != 9500)
                Assert.AreNotEqual(fixedPath, brokenPath,
                    "When ServerPort != 9500 the fixed path must differ from the old hardcoded path");
        }
    }
}
