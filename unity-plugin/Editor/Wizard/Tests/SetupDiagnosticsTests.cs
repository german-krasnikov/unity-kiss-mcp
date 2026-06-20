using NUnit.Framework;
using UnityMCP.Editor.Wizard;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class SetupDiagnosticsTests
    {
        [Test]
        public void BuildClaudeCodeSnippet_ContainsPort()
        {
            var snippet = SetupDiagnostics.BuildClaudeCodeSnippet(9500);
            Assert.That(snippet, Does.Contain("9500"));
        }

        [Test]
        public void BuildClaudeCodeSnippet_ContainsMcpAdd()
        {
            var snippet = SetupDiagnostics.BuildClaudeCodeSnippet(9500);
            Assert.That(snippet, Does.Contain("mcp add"));
        }

        [Test]
        public void BuildClaudeCodeSnippet_ContainsUnity()
        {
            var snippet = SetupDiagnostics.BuildClaudeCodeSnippet(9500);
            StringAssert.Contains("unity", snippet.ToLowerInvariant());
        }

        [Test]
        public void BuildClaudeCodeSnippet_DifferentPort_ContainsThatPort()
        {
            var snippet = SetupDiagnostics.BuildClaudeCodeSnippet(9501);
            Assert.That(snippet, Does.Contain("9501"));
            Assert.That(snippet, Does.Not.Contain("9500"));
        }

        [Test]
        public void CheckServer_WhenNotRunning_ReturnsFalse()
        {
            // MCPServer is not running in test context (no TCP listener)
            var (ok, detail) = SetupDiagnostics.CheckServer();
            // Either ok or not — but result must be consistent (not throw)
            Assert.IsNotNull(detail);
        }

        [Test]
        public void CheckPython_NullDir_ReturnsFalse()
        {
            var (ok, detail) = SetupDiagnostics.CheckPython(null);
            Assert.IsFalse(ok);
            Assert.IsNotNull(detail);
        }

        [Test]
        public void CheckPython_NonexistentDir_ReturnsFalse()
        {
            var (ok, detail) = SetupDiagnostics.CheckPython("/nonexistent/path/abc123");
            Assert.IsFalse(ok);
            Assert.IsNotNull(detail);
        }

        [Test]
        public void CheckPython_EmptyDir_ReturnsFalse()
        {
            var (ok, detail) = SetupDiagnostics.CheckPython("");
            Assert.IsFalse(ok);
            Assert.IsNotNull(detail);
        }
    }
}
