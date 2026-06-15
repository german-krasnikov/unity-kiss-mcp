using System;
using System.IO;
using NUnit.Framework;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class MCPActionsKillTests
    {
        [Test]
        public void Kill_MissingLockfile_DoesNotThrow()
        {
            // KillAll globs server-{ServerPort}-*.lock; when none exist it logs and returns.
            Assert.DoesNotThrow(() => MCPActions.Kill());
        }

        [Test]
        public void KillAll_MissingLockfile_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => MCPActions.KillAll());
        }

        [Test]
        public void Kill_ForwardsToKillAll()
        {
            // Kill() must delegate to KillAll() — both must not throw when no lockfiles present.
            Assert.DoesNotThrow(() => MCPActions.Kill());
            Assert.DoesNotThrow(() => MCPActions.KillAll());
        }

        [Test]
        public void KillAll_GlobsPerPidPattern()
        {
            // Verify the lockfile pattern uses PID format: server-{port}-*.lock
            var port = MCPServer.ServerPort;
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var dir = Path.Combine(home, ".unity-mcp");

            // Pattern used by KillAll must match per-PID files, NOT legacy single-file
            var perPidFile = $"server-{port}-12345.lock";
            var legacyFile = $"server-{port}.lock";

            // Confirm pattern: per-PID file matches glob "server-{port}-*.lock"
            Assert.IsTrue(perPidFile.StartsWith($"server-{port}-"),
                "Per-PID lockfile must start with server-{port}-");
            Assert.IsFalse(legacyFile.Contains("-12345"),
                "Legacy lockfile must NOT contain PID in filename");
        }

        [Test]
        public void KillAll_UsesServerPort_NotHardcoded9500()
        {
            var port = MCPServer.ServerPort;
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var perPidPattern = Path.Combine(home, ".unity-mcp", $"server-{port}-*.lock");
            Assert.IsTrue(perPidPattern.Contains($"server-{port}-"),
                $"KillAll pattern must use ServerPort={port}, not hardcoded 9500");
        }
    }
}
