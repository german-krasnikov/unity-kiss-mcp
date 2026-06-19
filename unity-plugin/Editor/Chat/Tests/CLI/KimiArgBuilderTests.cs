// TDD tests for KimiArgBuilder.
// Pure unit tests — no real FS writes (mcpConfigDir injectable seam), no Unity API.
using System.IO;
using System.Linq;
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
        public void Build_NoMcpConfigFileFlag()
        {
            var (args, _) = Build();
            Assert.IsFalse(System.Array.IndexOf(args, "--mcp-config-file") >= 0,
                "--mcp-config-file must not be passed; kimi reads ~/.kimi-code/mcp.json automatically");
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
        public void Build_YoloPlan_NotAddedInPromptMode()
        {
            var (args, _) = Build(approvalMode: "yolo");
            Assert.IsFalse(System.Array.IndexOf(args, "--yolo") >= 0,
                "--yolo incompatible with -p mode");
            Assert.IsFalse(System.Array.IndexOf(args, "--plan") >= 0,
                "--plan incompatible with -p mode");
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
            Assert.AreEqual(content.Count(c => c == '{'), content.Count(c => c == '}'),
                "Brace count must be balanced");
        }

        [Test]
        public void Build_StalePort_PreservesExternalServers()
        {
            var path = Path.Combine(_tempDir, "mcp.json");
            var stale =
                "{\n" +
                "  \"mcpServers\": {\n" +
                "    \"blender\": { \"command\": \"blender-mcp\", \"args\": [] },\n" +
                "    \"unity-mcp\": { \"command\": \"python3\", \"args\": [\"-m\",\"unity_mcp.server\"], \"env\": { \"UNITY_MCP_PORT\": \"9501\" } }\n" +
                "  }\n" +
                "}\n";
            File.WriteAllText(path, stale);

            KimiArgBuilder.Build("test", mcpConfigDir: _tempDir, port: 9900);

            var content = File.ReadAllText(path);
            StringAssert.Contains("\"blender\"", content,
                "External blender-mcp server must be preserved after port update");
            StringAssert.Contains("blender-mcp", content);
            StringAssert.Contains("9900", content);
            StringAssert.DoesNotContain("9501", content);
            Assert.AreEqual(content.Count(c => c == '{'), content.Count(c => c == '}'),
                "Brace count must be balanced");
        }

        [Test]
        public void Build_NewFile_WithExternalServers_PreservedAfterUpdate()
        {
            var path = Path.Combine(_tempDir, "mcp.json");
            // Simulate a file that has external servers but no unity-mcp yet
            var existing =
                "{\n" +
                "  \"mcpServers\": {\n" +
                "    \"blender\": { \"command\": \"blender-mcp\", \"args\": [] }\n" +
                "  }\n" +
                "}\n";
            File.WriteAllText(path, existing);

            KimiArgBuilder.Build("test", mcpConfigDir: _tempDir, port: 9500);

            var content = File.ReadAllText(path);
            StringAssert.Contains("\"blender\"", content, "blender must survive injection of unity-mcp");
            StringAssert.Contains("unity-mcp", content);
        }

        // ── Models config ─────────────────────────────────────────────────────

        [Test]
        public void Build_WriteModelsConfig_CreatesConfigToml()
        {
            Build();
            var path = Path.Combine(_tempDir, "config.toml");
            Assert.IsTrue(File.Exists(path), "config.toml must be created");
            var content = File.ReadAllText(path);
            StringAssert.Contains("[models.\"kimi-for-coding\"]", content);
            StringAssert.Contains("[models.\"k2p6\"]", content);
            StringAssert.Contains("[models.\"k2p5\"]", content);
            StringAssert.Contains("provider = \"managed:kimi-code\"", content);
        }

        [Test]
        public void Build_WriteModelsConfig_SkipsExistingModel()
        {
            var path = Path.Combine(_tempDir, "config.toml");
            File.WriteAllText(path, "[models.\"k2p6\"]\nprovider = \"managed:kimi-code\"\n");

            KimiArgBuilder.WriteModelsConfig(_tempDir);

            var content = File.ReadAllText(path);
            // k2p6 must appear only once
            Assert.AreEqual(1, CountOccurrences(content, "[models.\"k2p6\"]"),
                "k2p6 must not be duplicated");
            // other models still added
            StringAssert.Contains("[models.\"kimi-for-coding\"]", content);
            StringAssert.Contains("[models.\"k2p5\"]", content);
        }

        private static int CountOccurrences(string text, string pattern)
        {
            int count = 0, idx = 0;
            while ((idx = text.IndexOf(pattern, idx, System.StringComparison.Ordinal)) >= 0)
            {
                count++;
                idx += pattern.Length;
            }
            return count;
        }
    }
}
