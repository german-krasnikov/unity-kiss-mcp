using System.IO;
using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class BackendRegistryTests
    {
        private string _tmpDir;

        [SetUp]
        public void SetUp()
        {
            _tmpDir = Path.Combine(Path.GetTempPath(), "BackendRegistryTests_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tmpDir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_tmpDir)) Directory.Delete(_tmpDir, true);
        }

        private string MakeAgentsDir(string subdir)
        {
            var dir = Path.Combine(_tmpDir, subdir);
            Directory.CreateDirectory(dir);
            return dir;
        }

        private void WriteAgent(string dir, string filename, string name) =>
            File.WriteAllText(Path.Combine(dir, filename),
                $"---\nname: {name}\ndescription: test\n---\nBody.");

        // ── First entry is always Claude (enabled) ────────────────────────────

        [Test]
        public void Discover_ClaudeIsFirst()
        {
            var result = BackendRegistry.Discover(new string[0]);
            Assert.AreEqual("Claude", result[0].DisplayName);
            Assert.IsNull(result[0].AgentName);
            Assert.IsTrue(result[0].Enabled);
        }

        // ── Last entry is always Codex (disabled) ─────────────────────────────

        [Test]
        public void Discover_CodexIsLast()
        {
            var result = BackendRegistry.Discover(new string[0]);
            var last = result[result.Count - 1];
            Assert.AreEqual("Codex (soon)", last.DisplayName);
            Assert.IsFalse(last.Enabled);
        }

        // ── Agent files are discovered ────────────────────────────────────────

        [Test]
        public void Discover_ProjectAgentFile_Yields_EnabledSpec()
        {
            var projDir = MakeAgentsDir("proj");
            WriteAgent(projDir, "code-reviewer.md", "code-reviewer");

            var result = BackendRegistry.Discover(new[] { projDir });

            Assert.AreEqual(3, result.Count); // Claude + code-reviewer + Codex
            Assert.AreEqual("code-reviewer", result[1].DisplayName);
            Assert.AreEqual("code-reviewer", result[1].AgentName);
            Assert.IsTrue(result[1].Enabled);
        }

        [Test]
        public void Discover_UserAgentFile_Yields_EnabledSpec()
        {
            var userDir = MakeAgentsDir("user");
            WriteAgent(userDir, "doc-keeper.md", "doc-keeper");

            var result = BackendRegistry.Discover(new[] { userDir });

            Assert.AreEqual(3, result.Count);
            Assert.AreEqual("doc-keeper", result[1].AgentName);
        }

        // ── Project name shadows same-named user file (dedup) ─────────────────

        [Test]
        public void Discover_ProjectShadowsUserDuplicate()
        {
            var projDir = MakeAgentsDir("proj");
            var userDir = MakeAgentsDir("user");
            WriteAgent(projDir, "reviewer.md", "reviewer");
            WriteAgent(userDir, "reviewer.md", "reviewer"); // same name

            var result = BackendRegistry.Discover(new[] { projDir, userDir }); // nearest-first

            // Only one "reviewer" entry
            var reviewers = result.FindAll(b => b.AgentName == "reviewer");
            Assert.AreEqual(1, reviewers.Count);
        }

        // ── Non-existent dirs don't throw ─────────────────────────────────────

        [Test]
        public void Discover_NonExistentDirs_ReturnsClaudePlusCodex()
        {
            var result = BackendRegistry.Discover(new[] { "/nonexistent/path1", "/nonexistent/path2" });

            Assert.AreEqual(2, result.Count);
            Assert.AreEqual("Claude", result[0].DisplayName);
            Assert.AreEqual("Codex (soon)", result[1].DisplayName);
        }

        // ── Agent named "Claude" is skipped (collision guard) ─────────────────

        [Test]
        public void Discover_AgentNamedClaude_IsSkipped()
        {
            var projDir = MakeAgentsDir("proj");
            WriteAgent(projDir, "claude.md", "Claude");

            var result = BackendRegistry.Discover(new[] { projDir });

            // Still only Claude + Codex (no duplicate)
            Assert.AreEqual(2, result.Count);
        }

        // ── Agent named "Codex (soon)" is skipped (collision guard) ──────────

        [Test]
        public void Discover_AgentNamedCodexSoon_IsSkipped()
        {
            var projDir = MakeAgentsDir("proj");
            WriteAgent(projDir, "codex.md", "Codex (soon)");

            var result = BackendRegistry.Discover(new[] { projDir });

            // Exactly one "Codex (soon)" entry, and it is the disabled placeholder
            var codexEntries = result.FindAll(b => b.DisplayName == "Codex (soon)");
            Assert.AreEqual(1, codexEntries.Count);
            Assert.IsFalse(codexEntries[0].Enabled);
        }
    }
}
