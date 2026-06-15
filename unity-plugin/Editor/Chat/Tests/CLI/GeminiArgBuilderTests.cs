// TDD tests for GeminiArgBuilder.
// Pure unit tests — no real FS writes (settingsDir injectable seam), no Unity API.
using System.IO;
using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class GeminiArgBuilderTests
    {
        private string _tempDir;

        [SetUp]
        public void SetUp()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "GeminiArgBuilderTests_" + System.Guid.NewGuid().ToString("N").Substring(0, 8));
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
            return GeminiArgBuilder.Build(prompt, model, approvalMode, sandbox, extraArgs, _tempDir);
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
        public void Build_ContainsStreamJsonFormat()
        {
            var (args, _) = Build();
            Assert.Contains("--output-format", args);
            int idx = System.Array.IndexOf(args, "--output-format");
            Assert.AreEqual("stream-json", args[idx + 1]);
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
            var (args, _) = Build(model: "gemini-2.5-pro");
            int idx = System.Array.IndexOf(args, "--model");
            Assert.Greater(idx, -1, "--model must be present");
            Assert.AreEqual("gemini-2.5-pro", args[idx + 1]);
        }

        [Test]
        public void Build_NullModel_NoModelFlag()
        {
            var (args, _) = Build(model: null);
            Assert.IsFalse(System.Array.IndexOf(args, "--model") >= 0,
                "--model must not appear when model is null");
        }

        [Test]
        public void Build_EmptyModel_NoModelFlag()
        {
            var (args, _) = Build(model: "");
            Assert.IsFalse(System.Array.IndexOf(args, "--model") >= 0,
                "--model must not appear when model is empty");
        }

        // ── Approval mode ─────────────────────────────────────────────────────

        [Test]
        public void Build_YoloMode_AddsYoloFlag()
        {
            var (args, _) = Build(approvalMode: "yolo");
            Assert.Contains("--yolo", args);
        }

        [Test]
        public void Build_YoloMode_CaseInsensitive()
        {
            var (args, _) = Build(approvalMode: "YOLO");
            Assert.Contains("--yolo", args);
        }

        [Test]
        public void Build_DefaultMode_NoYoloFlag()
        {
            var (args, _) = Build(approvalMode: "default");
            Assert.IsFalse(System.Array.IndexOf(args, "--yolo") >= 0,
                "--yolo must not appear for default approval mode");
        }

        [Test]
        public void Build_NullApprovalMode_NoYoloFlag()
        {
            var (args, _) = Build(approvalMode: null);
            Assert.IsFalse(System.Array.IndexOf(args, "--yolo") >= 0);
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
            Assert.IsFalse(System.Array.IndexOf(args, "--sandbox") >= 0,
                "--sandbox must not appear when sandbox is false");
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
        public void Build_NullExtraArgs_NoExtraTokens()
        {
            var (args, _) = Build(extraArgs: null);
            // Just core flags: -p, prompt, --output-format, stream-json
            Assert.AreEqual(4, args.Length);
        }

        // ── MCP settings file ─────────────────────────────────────────────────

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
            // Build with same port (9500 = test default)
            GeminiArgBuilder.Build("test", settingsDir: _tempDir, port: 9500);
            Assert.AreEqual(mtime, File.GetLastWriteTimeUtc(path),
                "Settings file must not be rewritten when port matches");
        }

        [Test]
        public void Build_SettingsJson_StalePort_GetsUpdated()
        {
            var path = Path.Combine(_tempDir, "settings.json");
            var stale = "{\"mcpServers\":{\"unity-mcp\":{\"command\":\"python3\",\"args\":[],\"env\":{\"UNITY_MCP_PORT\":\"9501\"},\"trust\":true}}}";
            File.WriteAllText(path, stale);
            GeminiArgBuilder.Build("test", settingsDir: _tempDir, port: 9900);
            var content = File.ReadAllText(path);
            StringAssert.Contains("\"UNITY_MCP_PORT\": \"9900\"", content,
                "Stale port must be updated to current port");
            StringAssert.DoesNotContain("9501", content,
                "Old port must not remain in settings");
        }

        [Test]
        public void Build_SettingsJson_StalePort_PreservesOtherSettings()
        {
            var path = Path.Combine(_tempDir, "settings.json");
            var stale = "{\"security\":{\"auth\":\"oauth\"},\"mcpServers\":{\"unity-mcp\":{\"command\":\"python3\",\"args\":[],\"env\":{\"UNITY_MCP_PORT\":\"9501\"},\"trust\":true}}}";
            File.WriteAllText(path, stale);
            GeminiArgBuilder.Build("test", settingsDir: _tempDir, port: 9900);
            var content = File.ReadAllText(path);
            StringAssert.Contains("\"security\"", content,
                "Other settings must be preserved during port update");
            StringAssert.Contains("\"UNITY_MCP_PORT\": \"9900\"", content);
        }

        // ── BuildMcpBlock ─────────────────────────────────────────────────────

        [Test]
        public void BuildMcpBlock_ContainsMcpServers()
        {
            var block = GeminiArgBuilder.BuildMcpBlock();
            StringAssert.Contains("mcpServers", block);
        }

        [Test]
        public void BuildMcpBlock_ContainsUnityMcpKey()
        {
            var block = GeminiArgBuilder.BuildMcpBlock();
            StringAssert.Contains("unity-mcp", block);
        }
    }
}
