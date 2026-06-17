// TDD tests for KimiArgBuilder.
// Pure unit tests — no real FS writes (mcpConfigDir injectable seam), no Unity API.
using System.IO;
using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class KimiArgBuilderTests
    {
        private string _tempDir;

        [SetUp]
        public void SetUp()
        {
            _tempDir = Path.Combine(Path.GetTempPath(),
                "KimiArgBuilderTests_" + System.Guid.NewGuid().ToString("N").Substring(0, 8));
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
            string extraArgs    = null)
        {
            return KimiArgBuilder.Build(prompt, model, approvalMode, extraArgs, _tempDir);
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
        public void Build_ContainsMcpConfigFileFlag()
        {
            var (args, _) = Build();
            Assert.Contains("--mcp-config-file", args);
            int idx = System.Array.IndexOf(args, "--mcp-config-file");
            StringAssert.EndsWith("mcp.json", args[idx + 1]);
        }

        [Test]
        public void Build_McpConfigFile_PointsToInjectedDir()
        {
            var (args, _) = Build();
            int idx = System.Array.IndexOf(args, "--mcp-config-file");
            StringAssert.StartsWith(_tempDir, args[idx + 1]);
        }

        // ── StripEnvKeys ──────────────────────────────────────────────────────

        [Test]
        public void Build_StripEnvKeys_IsEmpty()
        {
            var (_, strip) = Build();
            Assert.AreEqual(0, strip.Length, "Kimi uses OAuth — nothing to strip");
        }

        // ── Model ─────────────────────────────────────────────────────────────

        [Test]
        public void Build_WithModel_AddsModelFlag()
        {
            var (args, _) = Build(model: "kimi-k2.7-code");
            int idx = System.Array.IndexOf(args, "--model");
            Assert.Greater(idx, -1, "--model must be present");
            Assert.AreEqual("kimi-k2.7-code", args[idx + 1]);
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
        public void Build_PlanMode_AddsPlanFlag()
        {
            var (args, _) = Build(approvalMode: "plan");
            Assert.Contains("--plan", args);
            Assert.IsFalse(System.Array.IndexOf(args, "--yolo") >= 0,
                "--yolo must not appear in plan mode");
        }

        [Test]
        public void Build_DefaultMode_NoYoloNoPlanFlag()
        {
            var (args, _) = Build(approvalMode: "");
            Assert.IsFalse(System.Array.IndexOf(args, "--yolo") >= 0);
            Assert.IsFalse(System.Array.IndexOf(args, "--plan") >= 0);
        }

        [Test]
        public void Build_NullApprovalMode_NoYoloNoPlanFlag()
        {
            var (args, _) = Build(approvalMode: null);
            Assert.IsFalse(System.Array.IndexOf(args, "--yolo") >= 0);
            Assert.IsFalse(System.Array.IndexOf(args, "--plan") >= 0);
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

        // ── MCP config file ───────────────────────────────────────────────────

        [Test]
        public void Build_WritesMcpJson()
        {
            Build();
            var path = Path.Combine(_tempDir, "mcp.json");
            Assert.IsTrue(File.Exists(path), "mcp.json must be written");
        }

        [Test]
        public void Build_McpJson_ContainsUnityMcp()
        {
            Build();
            var content = File.ReadAllText(Path.Combine(_tempDir, "mcp.json"));
            StringAssert.Contains("unity-mcp", content);
        }

        [Test]
        public void Build_McpJson_ContainsPort()
        {
            KimiArgBuilder.Build("test", mcpConfigDir: _tempDir, port: 9999);
            var content = File.ReadAllText(Path.Combine(_tempDir, "mcp.json"));
            StringAssert.Contains("9999", content);
        }

        [Test]
        public void Build_SamePort_NotRewritten()
        {
            var path = Path.Combine(_tempDir, "mcp.json");
            // Write a file that already has correct port
            var existing =
                "{\n  \"mcpServers\": {\n    \"unity-mcp\": {\n" +
                "      \"command\": \"python3\",\n      \"args\": [\"-m\",\"unity_mcp.server\"],\n" +
                "      \"env\": { \"UNITY_MCP_PORT\": \"9500\" }\n    }\n  }\n}\n";
            File.WriteAllText(path, existing);
            var mtime = File.GetLastWriteTimeUtc(path);
            System.Threading.Thread.Sleep(50);
            KimiArgBuilder.Build("test", mcpConfigDir: _tempDir, port: 9500);
            Assert.AreEqual(mtime, File.GetLastWriteTimeUtc(path),
                "mcp.json must not be rewritten when port matches");
        }

        [Test]
        public void Build_StalePort_GetsUpdated()
        {
            var path = Path.Combine(_tempDir, "mcp.json");
            var stale =
                "{\n  \"mcpServers\": {\n    \"unity-mcp\": {\n" +
                "      \"command\": \"python3\",\n      \"args\": [\"-m\",\"unity_mcp.server\"],\n" +
                "      \"env\": { \"UNITY_MCP_PORT\": \"9501\" }\n    }\n  }\n}\n";
            File.WriteAllText(path, stale);
            KimiArgBuilder.Build("test", mcpConfigDir: _tempDir, port: 9900);
            var content = File.ReadAllText(path);
            StringAssert.Contains("9900", content);
            StringAssert.DoesNotContain("9501", content);
        }
    }
}
