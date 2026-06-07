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

        // ── Last entry is always Codex (Session) (enabled) ───────────────────

        [Test]
        public void Discover_CodexIsLast()
        {
            var result = BackendRegistry.Discover(new string[0]);
            // Codex (spawn-per-turn) is second-to-last; Codex (Session) is last
            var codex = result.Find(b => b.Kind == BackendKind.Codex);
            Assert.IsNotNull(codex.DisplayName);
            Assert.AreEqual("Codex", codex.DisplayName);
            Assert.IsTrue(codex.Enabled);
            Assert.AreEqual(BackendKind.CodexAppServer, result[result.Count - 1].Kind);
        }

        // ── Agent files are discovered ────────────────────────────────────────

        [Test]
        public void Discover_ProjectAgentFile_Yields_EnabledSpec()
        {
            var projDir = MakeAgentsDir("proj");
            WriteAgent(projDir, "code-reviewer.md", "code-reviewer");

            var result = BackendRegistry.Discover(new[] { projDir });

            Assert.AreEqual(4, result.Count); // Claude + code-reviewer + Codex + Codex (Session)
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

            Assert.AreEqual(4, result.Count);
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
        public void Discover_NonExistentDirs_ReturnsClaudePlusCodexEntries()
        {
            var result = BackendRegistry.Discover(new[] { "/nonexistent/path1", "/nonexistent/path2" });

            Assert.AreEqual(3, result.Count);
            Assert.AreEqual("Claude",            result[0].DisplayName);
            Assert.AreEqual("Codex",             result[1].DisplayName);
            Assert.AreEqual("Codex (Session)", result[2].DisplayName);
            Assert.IsTrue(result[1].Enabled);
            Assert.IsTrue(result[2].Enabled);
        }

        // ── Agent named "Claude" is skipped (collision guard) ─────────────────

        [Test]
        public void Discover_AgentNamedClaude_IsSkipped()
        {
            var projDir = MakeAgentsDir("proj");
            WriteAgent(projDir, "claude.md", "Claude");

            var result = BackendRegistry.Discover(new[] { projDir });

            // Claude + Codex + Codex (Session), no extra "Claude" entry
            Assert.AreEqual(3, result.Count);
        }

        // ── Agent named "Codex" is skipped (collision guard) ─────────────────

        [Test]
        public void Discover_AgentNamedCodex_IsSkipped()
        {
            var projDir = MakeAgentsDir("proj");
            WriteAgent(projDir, "codex.md", "Codex");

            var result = BackendRegistry.Discover(new[] { projDir });

            // Exactly one "Codex" entry, and it is the built-in enabled Codex
            var codexEntries = result.FindAll(b => b.DisplayName == "Codex");
            Assert.AreEqual(1, codexEntries.Count);
            Assert.IsTrue(codexEntries[0].Enabled);
        }
    }
}
