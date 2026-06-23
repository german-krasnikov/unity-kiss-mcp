// TDD tests for OpenCodeArgBuilder.
// Pure unit tests — injectable configDir seam, no real FS for argv tests.
using System.IO;
using System.Linq;
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
            var path = OpenCodeArgBuilder.WriteConfig(_tempDir, 9500);
            Assert.IsTrue(File.Exists(path));
        }

        [Test]
        public void WriteConfig_ContainsMcpKey()
        {
            var path = OpenCodeArgBuilder.WriteConfig(_tempDir, 9500);
            var content = File.ReadAllText(path);
            StringAssert.Contains("\"mcp\"", content);
        }

        [Test]
        public void WriteConfig_ContainsTypeLocal()
        {
            var path = OpenCodeArgBuilder.WriteConfig(_tempDir, 9500);
            var content = File.ReadAllText(path);
            StringAssert.Contains("\"type\"", content);
            StringAssert.Contains("\"local\"", content);
        }

        [Test]
        public void WriteConfig_ContainsUnityMcpKey()
        {
            var path = OpenCodeArgBuilder.WriteConfig(_tempDir, 9500);
            var content = File.ReadAllText(path);
            StringAssert.Contains("unity-mcp", content);
        }

        [Test]
        public void WriteConfig_ContainsCommandArray()
        {
            var path = OpenCodeArgBuilder.WriteConfig(_tempDir, 9500);
            var content = File.ReadAllText(path);
            // "command" must be an array (JSON array bracket)
            StringAssert.Contains("\"command\"", content);
            // Should contain python3 or python as array element
            StringAssert.Contains("python", content);
        }

        [Test]
        public void WriteConfig_ContainsEnvironmentKey_NotEnvKey()
        {
            var path = OpenCodeArgBuilder.WriteConfig(_tempDir, 9500);
            var content = File.ReadAllText(path);
            StringAssert.Contains("\"environment\"", content);
        }

        [Test]
        public void WriteConfig_ContainsPort()
        {
            var path = OpenCodeArgBuilder.WriteConfig(_tempDir, 9876);
            var content = File.ReadAllText(path);
            StringAssert.Contains("9876", content);
        }

        [Test]
        public void WriteConfig_Overwrites_ExistingFile()
        {
            // Write once to create the file with port-scoped name
            var path = OpenCodeArgBuilder.WriteConfig(_tempDir, 9500);
            File.WriteAllText(path, "old content");
            OpenCodeArgBuilder.WriteConfig(_tempDir, 9500);
            var content = File.ReadAllText(path);
            StringAssert.Contains("\"mcp\"", content);
            StringAssert.DoesNotContain("old content", content);
        }

        // ── BuildEnv dedup regression gate ────────────────────────────────────

        [Test]
        public void BuildEnv_DoesNotContainUnityMcpPort()
        {
            var env = OpenCodeArgBuilder.BuildEnv(_tempDir, 9500);
            Assert.IsFalse(env.ContainsKey("UNITY_MCP_PORT"),
                "UNITY_MCP_PORT must NOT be in BuildEnv — port is already in OPENCODE_CONFIG JSON");
        }

        [Test]
        public void BuildEnv_ContainsOnlyOpencodeConfig()
        {
            var env = OpenCodeArgBuilder.BuildEnv(_tempDir, 9500);
            Assert.AreEqual(1, env.Count,
                "BuildEnv must return exactly 1 key (OPENCODE_CONFIG); UNITY_MCP_PORT is redundant");
            Assert.IsTrue(env.ContainsKey("OPENCODE_CONFIG"));
        }

        // ── MergeGlobalOpenCodeConfig (REQ-1) ─────────────────────────────────

        [Test]
        public void WriteConfig_GlobalMissing_ProducesUnityOnly()
        {
            var nonexistentPath = Path.Combine(_tempDir, "nonexistent_config.json");
            var path = OpenCodeArgBuilder.WriteConfig(_tempDir, 9500, nonexistentPath);
            var content = File.ReadAllText(path);
            StringAssert.Contains("unity-mcp", content);
        }

        [Test]
        public void WriteConfig_GlobalEmpty_ProducesUnityOnly()
        {
            var userConfigPath = Path.Combine(_tempDir, "empty_config.json");
            File.WriteAllText(userConfigPath, "{}");
            var path = OpenCodeArgBuilder.WriteConfig(_tempDir, 9500, userConfigPath);
            var content = File.ReadAllText(path);
            StringAssert.Contains("unity-mcp", content);
        }

        [Test]
        public void WriteConfig_GlobalWithExternalServer_MergesIt()
        {
            var userConfigPath = Path.Combine(_tempDir, "user_config.json");
            File.WriteAllText(userConfigPath,
                "{\n  \"mcp\": {\n    \"blender\": { \"type\": \"local\", \"command\": [\"blender-mcp\"] }\n  }\n}\n");
            var path = OpenCodeArgBuilder.WriteConfig(_tempDir, 9500, userConfigPath);
            var content = File.ReadAllText(path);
            StringAssert.Contains("unity-mcp", content, "unity-mcp must be present");
            StringAssert.Contains("\"blender\"", content, "External blender server must be merged in");
            StringAssert.Contains("blender-mcp", content);
        }

        [Test]
        public void WriteConfig_GlobalWithUnityEntry_StripsIt()
        {
            var userConfigPath = Path.Combine(_tempDir, "user_config_unity.json");
            File.WriteAllText(userConfigPath,
                "{\n  \"mcp\": {\n" +
                "    \"unity-mcp\": { \"type\": \"local\", \"command\": [\"old-unity\"] },\n" +
                "    \"blender\": { \"type\": \"local\", \"command\": [\"blender-mcp\"] }\n" +
                "  }\n}\n");
            var path = OpenCodeArgBuilder.WriteConfig(_tempDir, 9500, userConfigPath);
            var content = File.ReadAllText(path);
            // Only one unity-mcp (the injected one, not the stripped global one)
            int count = 0;
            int idx = -1;
            while ((idx = content.IndexOf("\"unity-mcp\"", idx + 1, System.StringComparison.Ordinal)) >= 0)
                count++;
            Assert.AreEqual(1, count, "unity-mcp must appear exactly once (stripped from global, injected fresh)");
            StringAssert.Contains("\"blender\"", content, "blender must still be present");
        }

        [Test]
        public void WriteConfig_GlobalWithAllUnityVariants_StripsAll()
        {
            var userConfigPath = Path.Combine(_tempDir, "user_config_all_unity.json");
            File.WriteAllText(userConfigPath,
                "{\n  \"mcp\": {\n" +
                "    \"unity\": { \"type\": \"local\", \"command\": [\"u1\"] },\n" +
                "    \"unity-mcp\": { \"type\": \"local\", \"command\": [\"u2\"] },\n" +
                "    \"unity-kiss-mcp\": { \"type\": \"local\", \"command\": [\"u3\"] },\n" +
                "    \"blender\": { \"type\": \"local\", \"command\": [\"blender-mcp\"] }\n" +
                "  }\n}\n");
            var path = OpenCodeArgBuilder.WriteConfig(_tempDir, 9500, userConfigPath);
            var content = File.ReadAllText(path);
            StringAssert.Contains("\"blender\"", content, "blender must be preserved");
            // Verify global unity variant commands (u1/u2/u3) are NOT injected — these
            // are unique markers that cannot appear in the base config or Python path.
            StringAssert.DoesNotContain("\"u1\"", content, "bare unity command u1 must be stripped");
            StringAssert.DoesNotContain("\"u2\"", content, "unity-mcp global command u2 must be stripped");
            StringAssert.DoesNotContain("\"u3\"", content, "unity-kiss-mcp command u3 must be stripped");
        }

        [Test]
        public void WriteConfig_GlobalWithArrayValueEntry_DoesNotCorrupt()
        {
            // MAJOR regression guard: if an mcp-block entry has an array value (e.g. "settings":[1,2,3])
            // the parser must NOT scan past the '[' to a later '{', which would corrupt the output.
            var userConfigPath = Path.Combine(_tempDir, "user_config_array.json");
            File.WriteAllText(userConfigPath,
                "{\n  \"mcp\": {\n" +
                "    \"settings\": [1, 2, 3],\n" +
                "    \"blender\": { \"type\": \"local\", \"command\": [\"blender-mcp\"] }\n" +
                "  }\n}\n");
            var path = OpenCodeArgBuilder.WriteConfig(_tempDir, 9500, userConfigPath);
            var content = File.ReadAllText(path);
            // unity-mcp must be present (base config intact)
            StringAssert.Contains("unity-mcp", content, "unity-mcp must be in output");
            // blender (object entry) must be merged
            StringAssert.Contains("\"blender\"", content, "blender object entry must be merged");
            StringAssert.Contains("blender-mcp", content);
            // output must have balanced braces (not corrupted)
            Assert.AreEqual(content.Count(c => c == '{'), content.Count(c => c == '}'),
                "Braces must be balanced — array-value entry must not corrupt brace walk");
            // settings array must NOT appear (it's not an MCP server object, skip it)
            StringAssert.DoesNotContain("\"settings\"", content,
                "Non-object (array) entry must be skipped, not injected");
        }

        [Test]
        public void WriteConfig_GlobalCorrupt_ProducesUnityOnly()
        {
            var userConfigPath = Path.Combine(_tempDir, "corrupt_config.json");
            File.WriteAllText(userConfigPath, "not valid json {{{");
            var path = OpenCodeArgBuilder.WriteConfig(_tempDir, 9500, userConfigPath);
            var content = File.ReadAllText(path);
            StringAssert.Contains("unity-mcp", content, "Must produce valid unity-only config on corrupt input");
        }

        // ── Port-scoped filename tests (T9-T11) ───────────────────────────────

        // T9 — two ports → two separate files that coexist without clobbering
        [Test]
        public void WriteConfig_TwoPorts_FilesCoexist()
        {
            var p1 = OpenCodeArgBuilder.WriteConfig(_tempDir, 9500);
            var p2 = OpenCodeArgBuilder.WriteConfig(_tempDir, 9876);
            Assert.AreNotEqual(p1, p2);
            Assert.IsTrue(File.Exists(p1));
            Assert.IsTrue(File.Exists(p2));
            StringAssert.Contains("9500", File.ReadAllText(p1));
            StringAssert.Contains("9876", File.ReadAllText(p2));
        }

        // T10 — BuildEnv path must match WriteConfig path for the same port
        [Test]
        public void BuildEnv_PathMatchesWriteConfig_SamePort()
        {
            var writtenPath = OpenCodeArgBuilder.WriteConfig(_tempDir, 9500);
            var envPath     = OpenCodeArgBuilder.BuildEnv(_tempDir, 9500)["OPENCODE_CONFIG"];
            Assert.AreEqual(writtenPath, envPath);
        }

        // T11 — port>0 must not produce the legacy bare filename
        [Test]
        public void WriteConfig_PortGtZero_DoesNotProduceLegacyBareFilename()
        {
            var path = OpenCodeArgBuilder.WriteConfig(_tempDir, 9500);
            Assert.AreNotEqual("opencode-unity-mcp.json", Path.GetFileName(path));
        }
    }
}
