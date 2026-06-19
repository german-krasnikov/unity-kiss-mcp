// TDD tests for AgyArgBuilder (Antigravity CLI).
// Key differences vs Gemini: no --output-format, --dangerously-skip-permissions instead of --yolo.
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class AgyArgBuilderTests
    {
        private string _tempDir;

        [SetUp]
        public void SetUp()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "AgyArgBuilderTests_" + System.Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(_tempDir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }

        private (string[] args, string[] strip) Build(
            string prompt       = "list objects",
            string model        = null,
            string approvalMode = null,
            bool   sandbox      = false,
            string extraArgs    = null)
        {
            return AgyArgBuilder.Build(prompt, model, approvalMode, sandbox, extraArgs, _tempDir);
        }

        // ── Core flags ────────────────────────────────────────────────────────

        [Test]
        public void Build_ContainsPromptFlag()
        {
            var (args, _) = Build("hello");
            int idx = System.Array.IndexOf(args, "-p");
            Assert.Greater(idx, -1, "-p flag must be present");
            Assert.AreEqual("hello", args[idx + 1]);
        }

        [Test]
        public void Build_NoOutputFormatFlag()
        {
            // agy does NOT support --output-format — must be absent
            var (args, _) = Build();
            Assert.IsFalse(System.Array.IndexOf(args, "--output-format") >= 0,
                "--output-format must NOT be present for agy");
        }

        [Test]
        public void Build_NullExtraArgs_OnlyTwoArgs()
        {
            // Minimal: just -p <prompt>
            var (args, _) = Build(extraArgs: null);
            Assert.AreEqual(2, args.Length, "Minimal agy args: -p <prompt>");
        }

        [Test]
        public void Build_StripEnvKeys_ContainsGeminiApiKey()
        {
            var (_, strip) = Build();
            Assert.Contains("GEMINI_API_KEY", strip);
        }

        // ── Model ─────────────────────────────────────────────────────────────

        [Test]
        public void Build_WithModel_AddsModelFlag()
        {
            var (args, _) = Build(model: "some-model");
            int idx = System.Array.IndexOf(args, "--model");
            Assert.Greater(idx, -1, "--model must be present");
            Assert.AreEqual("some-model", args[idx + 1]);
        }

        [Test]
        public void Build_NullModel_NoModelFlag()
        {
            var (args, _) = Build(model: null);
            Assert.IsFalse(System.Array.IndexOf(args, "--model") >= 0);
        }

        // ── Approval mode ─────────────────────────────────────────────────────

        [Test]
        public void Build_YoloMode_AddsDangerouslySkipPermissions()
        {
            var (args, _) = Build(approvalMode: "yolo");
            Assert.Contains("--dangerously-skip-permissions", args);
        }

        [Test]
        public void Build_YoloMode_NoLegacyYoloFlag()
        {
            // agy uses --dangerously-skip-permissions, not --yolo
            var (args, _) = Build(approvalMode: "yolo");
            Assert.IsFalse(System.Array.IndexOf(args, "--yolo") >= 0,
                "--yolo must NOT appear for agy (use --dangerously-skip-permissions)");
        }

        [Test]
        public void Build_YoloMode_CaseInsensitive()
        {
            var (args, _) = Build(approvalMode: "YOLO");
            Assert.Contains("--dangerously-skip-permissions", args);
        }

        [Test]
        public void Build_DefaultMode_NoDangerousFlag()
        {
            var (args, _) = Build(approvalMode: "default");
            Assert.IsFalse(System.Array.IndexOf(args, "--dangerously-skip-permissions") >= 0);
        }

        // ── Sandbox ───────────────────────────────────────────────────────────

        [Test]
        public void Build_SandboxTrue_AddsSandboxFlag()
        {
            var (args, _) = Build(sandbox: true);
            Assert.Contains("--sandbox", args);
        }

        [Test]
        public void Build_SandboxFalse_NoSandboxFlag()
        {
            var (args, _) = Build(sandbox: false);
            Assert.IsFalse(System.Array.IndexOf(args, "--sandbox") >= 0);
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

        // ── MCP settings file (same path as Gemini: ~/.gemini/settings.json) ──

        [Test]
        public void Build_WritesSettingsJson()
        {
            Build();
            var path = Path.Combine(_tempDir, "settings.json");
            Assert.IsTrue(File.Exists(path), "settings.json must be written");
        }

        [Test]
        public void Build_SettingsJson_ContainsUnityMcp()
        {
            Build();
            var content = File.ReadAllText(Path.Combine(_tempDir, "settings.json"));
            StringAssert.Contains("unity-mcp", content);
        }

        [Test]
        public void Build_SettingsJson_ContainsTrustTrue()
        {
            Build();
            var content = File.ReadAllText(Path.Combine(_tempDir, "settings.json"));
            StringAssert.Contains("\"trust\"", content);
            StringAssert.Contains("true", content);
        }

        [Test]
        public void Build_SettingsJson_SamePort_NotRewritten()
        {
            var path = Path.Combine(_tempDir, "settings.json");
            var existing = "{\"mcpServers\":{\"unity-mcp\":{\"command\":\"python3\",\"args\":[],\"env\":{\"UNITY_MCP_PORT\":\"9500\"},\"trust\":true}}}";
            File.WriteAllText(path, existing);
            var mtime = File.GetLastWriteTimeUtc(path);
            System.Threading.Thread.Sleep(50);
            AgyArgBuilder.Build("test", settingsDir: _tempDir, port: 9500);
            Assert.AreEqual(mtime, File.GetLastWriteTimeUtc(path),
                "Settings file must not be rewritten when port matches");
        }

        [Test]
        public void Build_SettingsJson_StalePort_GetsUpdated()
        {
            var path = Path.Combine(_tempDir, "settings.json");
            var stale = "{\"mcpServers\":{\"unity-mcp\":{\"command\":\"python3\",\"args\":[],\"env\":{\"UNITY_MCP_PORT\":\"9501\"},\"trust\":true}}}";
            File.WriteAllText(path, stale);
            AgyArgBuilder.Build("test", settingsDir: _tempDir, port: 9900);
            var content = File.ReadAllText(path);
            StringAssert.Contains("\"UNITY_MCP_PORT\": \"9900\"", content);
            StringAssert.DoesNotContain("9501", content);
            Assert.AreEqual(content.Count(c => c == '{'), content.Count(c => c == '}'),
                "Brace count must be balanced");
        }

        // ── BuildMcpBlock ─────────────────────────────────────────────────────

        [Test]
        public void BuildMcpBlock_ContainsMcpServers()
        {
            var block = AgyArgBuilder.BuildMcpBlock();
            StringAssert.Contains("mcpServers", block);
        }

        [Test]
        public void BuildMcpBlock_ContainsUnityMcpKey()
        {
            var block = AgyArgBuilder.BuildMcpBlock();
            StringAssert.Contains("unity-mcp", block);
        }
    }
}
