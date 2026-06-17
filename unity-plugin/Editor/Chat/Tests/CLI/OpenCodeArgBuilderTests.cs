// TDD tests for OpenCodeArgBuilder.
// Pure unit tests — injectable configDir seam, no real FS for argv tests.
using System.IO;
using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class OpenCodeArgBuilderTests
    {
        private string _tempDir;

        [SetUp]
        public void SetUp()
        {
            _tempDir = Path.Combine(Path.GetTempPath(),
                "OpenCodeArgBuilderTests_" + System.Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(_tempDir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }

        private (string[] args, string[] strip) Build(
            string prompt          = "list objects",
            string model           = null,
            bool   skipPermissions = true,
            string extraArgs       = null,
            string resumeId        = null)
        {
            return OpenCodeArgBuilder.Build(prompt, model, skipPermissions, extraArgs,
                _tempDir, port: 9500, resumeId: resumeId);
        }

        // ── Subcommand ─────────────────────────────────────────────────────────

        [Test]
        public void Build_FirstArgIsRun()
        {
            var (args, _) = Build();
            Assert.AreEqual("run", args[0]);
        }

        // ── Format flag ────────────────────────────────────────────────────────

        [Test]
        public void Build_ContainsFormatJson()
        {
            var (args, _) = Build();
            int idx = System.Array.IndexOf(args, "--format");
            Assert.Greater(idx, -1, "--format flag must be present");
            Assert.AreEqual("json", args[idx + 1]);
        }

        // ── Skip permissions ──────────────────────────────────────────────────

        [Test]
        public void Build_SkipPermissions_True_AddsFlag()
        {
            var (args, _) = Build(skipPermissions: true);
            Assert.Contains("--dangerously-skip-permissions", args);
        }

        [Test]
        public void Build_SkipPermissions_False_OmitsFlag()
        {
            var (args, _) = Build(skipPermissions: false);
            Assert.IsFalse(System.Array.IndexOf(args, "--dangerously-skip-permissions") >= 0);
        }

        // ── Model ─────────────────────────────────────────────────────────────

        [Test]
        public void Build_WithModel_AddsModelFlag()
        {
            var (args, _) = Build(model: "anthropic/claude-sonnet-4-20250514");
            int idx = System.Array.IndexOf(args, "--model");
            Assert.Greater(idx, -1, "--model must be present");
            Assert.AreEqual("anthropic/claude-sonnet-4-20250514", args[idx + 1]);
        }

        [Test]
        public void Build_NullModel_NoModelFlag()
        {
            var (args, _) = Build(model: null);
            Assert.IsFalse(System.Array.IndexOf(args, "--model") >= 0);
        }

        [Test]
        public void Build_EmptyModel_NoModelFlag()
        {
            var (args, _) = Build(model: "");
            Assert.IsFalse(System.Array.IndexOf(args, "--model") >= 0);
        }

        // ── Extra args ────────────────────────────────────────────────────────

        [Test]
        public void Build_WithExtraArgs_AppendsTokens()
        {
            var (args, _) = Build(extraArgs: "--debug --timeout 5");
            CollectionAssert.Contains(args, "--debug");
            CollectionAssert.Contains(args, "--timeout");
            CollectionAssert.Contains(args, "5");
        }

        [Test]
        public void Build_NullExtraArgs_NoExtra()
        {
            var (args, _) = Build(extraArgs: null);
            // just verify no crash and prompt is still last
            Assert.AreEqual("list objects", args[args.Length - 1]);
        }

        // ── Prompt ────────────────────────────────────────────────────────────

        [Test]
        public void Build_PromptIsLastArg()
        {
            var (args, _) = Build(prompt: "my prompt");
            Assert.AreEqual("my prompt", args[args.Length - 1]);
        }

        // ── Resume ────────────────────────────────────────────────────────────

        [Test]
        public void Build_WithResumeId_AddsSFlag()
        {
            var (args, _) = Build(resumeId: "oc-session-123");
            int idx = System.Array.IndexOf(args, "-s");
            Assert.Greater(idx, -1, "-s flag must be present");
            Assert.AreEqual("oc-session-123", args[idx + 1]);
        }

        // ── StripEnvKeys ──────────────────────────────────────────────────────

        [Test]
        public void Build_StripEnvKeys_IsEmpty()
        {
            var (_, strip) = Build();
            Assert.AreEqual(0, strip.Length, "opencode has no API key to strip");
        }

        // ── WriteConfig ───────────────────────────────────────────────────────

        [Test]
        public void WriteConfig_CreatesFile()
        {
            OpenCodeArgBuilder.WriteConfig(_tempDir, 9500);
            var path = Path.Combine(_tempDir, "opencode-unity-mcp.json");
            Assert.IsTrue(File.Exists(path));
        }

        [Test]
        public void WriteConfig_ContainsMcpKey()
        {
            OpenCodeArgBuilder.WriteConfig(_tempDir, 9500);
            var content = File.ReadAllText(Path.Combine(_tempDir, "opencode-unity-mcp.json"));
            StringAssert.Contains("\"mcp\"", content);
        }

        [Test]
        public void WriteConfig_ContainsTypeLocal()
        {
            OpenCodeArgBuilder.WriteConfig(_tempDir, 9500);
            var content = File.ReadAllText(Path.Combine(_tempDir, "opencode-unity-mcp.json"));
            StringAssert.Contains("\"type\"", content);
            StringAssert.Contains("\"local\"", content);
        }

        [Test]
        public void WriteConfig_ContainsUnityMcpKey()
        {
            OpenCodeArgBuilder.WriteConfig(_tempDir, 9500);
            var content = File.ReadAllText(Path.Combine(_tempDir, "opencode-unity-mcp.json"));
            StringAssert.Contains("unity-mcp", content);
        }

        [Test]
        public void WriteConfig_ContainsCommandArray()
        {
            OpenCodeArgBuilder.WriteConfig(_tempDir, 9500);
            var content = File.ReadAllText(Path.Combine(_tempDir, "opencode-unity-mcp.json"));
            // "command" must be an array (JSON array bracket)
            StringAssert.Contains("\"command\"", content);
            // Should contain python3 or python as array element
            StringAssert.Contains("python", content);
        }

        [Test]
        public void WriteConfig_ContainsEnvironmentKey_NotEnvKey()
        {
            OpenCodeArgBuilder.WriteConfig(_tempDir, 9500);
            var content = File.ReadAllText(Path.Combine(_tempDir, "opencode-unity-mcp.json"));
            StringAssert.Contains("\"environment\"", content);
        }

        [Test]
        public void WriteConfig_ContainsPort()
        {
            OpenCodeArgBuilder.WriteConfig(_tempDir, 9876);
            var content = File.ReadAllText(Path.Combine(_tempDir, "opencode-unity-mcp.json"));
            StringAssert.Contains("9876", content);
        }

        [Test]
        public void WriteConfig_Overwrites_ExistingFile()
        {
            var path = Path.Combine(_tempDir, "opencode-unity-mcp.json");
            File.WriteAllText(path, "old content");
            OpenCodeArgBuilder.WriteConfig(_tempDir, 9500);
            var content = File.ReadAllText(path);
            StringAssert.Contains("\"mcp\"", content);
            StringAssert.DoesNotContain("old content", content);
        }
    }
}
