// TDD: InstallSourceDetector — pure logic paths testable without PackageInfo.
using NUnit.Framework;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class InstallSourceDetectorTests
    {
        // ── IsLocalRepoRoot ───────────────────────────────────────────────────

        [Test]
        public void IsLocalRepoRoot_WithInstallPy_ReturnsTrue()
        {
            // Arrange: temp dir with install.py
            var dir = System.IO.Path.GetTempPath()
                + "mcp_test_" + System.Guid.NewGuid().ToString("N");
            System.IO.Directory.CreateDirectory(dir);
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "install.py"), "");

            try
            {
                // Act + Assert
                Assert.IsTrue(InstallSourceDetector.IsLocalRepoRoot(dir));
            }
            finally
            {
                System.IO.Directory.Delete(dir, true);
            }
        }

        [Test]
        public void IsLocalRepoRoot_WithoutInstallPy_ReturnsFalse()
        {
            var dir = System.IO.Path.GetTempPath()
                + "mcp_test_" + System.Guid.NewGuid().ToString("N");
            System.IO.Directory.CreateDirectory(dir);

            try
            {
                Assert.IsFalse(InstallSourceDetector.IsLocalRepoRoot(dir));
            }
            finally
            {
                System.IO.Directory.Delete(dir, true);
            }
        }

        [Test]
        public void IsLocalRepoRoot_NullPath_ReturnsFalse()
        {
            Assert.IsFalse(InstallSourceDetector.IsLocalRepoRoot(null));
        }

        [Test]
        public void IsLocalRepoRoot_EmptyPath_ReturnsFalse()
        {
            Assert.IsFalse(InstallSourceDetector.IsLocalRepoRoot(""));
        }

#if UNITY_INCLUDE_TESTS
        // ── Injection tests ───────────────────────────────────────────────────

        [Test]
        public void Detect_WithInjectedLocalSource_ReturnsLocal()
        {
            InstallSourceDetector.SetSourceForTest(InstallSourceDetector.Source.Local);
            Assert.AreEqual(InstallSourceDetector.Source.Local, InstallSourceDetector.Detect());
            InstallSourceDetector.ClearTestOverride();
        }

        [Test]
        public void Detect_WithInjectedGitSource_ReturnsGit()
        {
            InstallSourceDetector.SetSourceForTest(InstallSourceDetector.Source.Git);
            Assert.AreEqual(InstallSourceDetector.Source.Git, InstallSourceDetector.Detect());
            InstallSourceDetector.ClearTestOverride();
        }

        [Test]
        public void LocalRepoRoot_WithInjectedPath_ReturnsInjected()
        {
            InstallSourceDetector.SetLocalRepoRootForTest("/fake/repo");
            Assert.AreEqual("/fake/repo", InstallSourceDetector.LocalRepoRoot());
            InstallSourceDetector.ClearTestOverride();
        }
#endif
    }
}
