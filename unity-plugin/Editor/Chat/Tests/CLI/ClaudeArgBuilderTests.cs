using System.Linq;
using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class ClaudeArgBuilderTests
    {
        [Test]
        public void Build_NoResume_ContainsCoreFlags()
        {
            var (args, _) = ClaudeArgBuilder.Build(
                "/usr/local/bin/claude",
                "/tmp/mcp.json",
                "plan",
                null);

            Assert.Contains("-p",                         args);
            Assert.Contains("--output-format",            args);
            Assert.Contains("stream-json",                args);
            Assert.Contains("--verbose",                  args);
            Assert.Contains("--include-partial-messages", args);
            Assert.Contains("--input-format",             args);
            Assert.Contains("--mcp-config",               args);
            Assert.Contains("/tmp/mcp.json",              args);
            Assert.Contains("--permission-mode",          args);
            Assert.Contains("--strict-mcp-config",        args);
            Assert.Contains("plan",                       args);
        }

        [Test]
        public void Build_AllowsUnityMcpServer_SoHeadlessToolsDontBlock()
        {
            var (args, _) = ClaudeArgBuilder.Build(
                "/usr/local/bin/claude", "/tmp/mcp.json", "plan", null);

            var idx = System.Array.IndexOf(args, "--allowedTools");
            Assert.Greater(idx, -1, "headless claude -p has no permission prompt; unity tools must be pre-allowed");
            Assert.AreEqual("mcp__unity", args[idx + 1]);
        }

        [Test]
        public void Build_WithResume_AppendsResumeFlag()
        {
            var (args, _) = ClaudeArgBuilder.Build(
                "/usr/local/bin/claude",
                "/tmp/mcp.json",
                "acceptEdits",
                "sess-123");

            Assert.Contains("--resume", args);
            Assert.Contains("sess-123", args);
        }

        [Test]
        public void Build_NoResume_NoResumeFlag()
        {
            var (args, _) = ClaudeArgBuilder.Build(
                "/usr/local/bin/claude",
                "/tmp/mcp.json",
                "plan",
                null);

            Assert.IsFalse(args.Contains("--resume"));
        }

        [Test]
        public void Build_StripEnvKeys_ContainsAnthropicApiKey()
        {
            var (_, strip) = ClaudeArgBuilder.Build(
                "/usr/local/bin/claude",
                "/tmp/mcp.json",
                "plan",
                null);

            Assert.Contains("ANTHROPIC_API_KEY", strip);
        }

        [Test]
        public void Build_McpConfigPath_PlacedAfterFlag()
        {
            var (args, _) = ClaudeArgBuilder.Build(
                "/bin/claude",
                "/my/config.json",
                "plan",
                null);

            var idx = System.Array.IndexOf(args, "--mcp-config");
            Assert.Greater(idx, -1);
            Assert.AreEqual("/my/config.json", args[idx + 1]);
        }

        [Test]
        public void Build_PermissionMode_PlacedAfterFlag()
        {
            var (args, _) = ClaudeArgBuilder.Build(
                "/bin/claude", "/cfg.json", "acceptEdits", null);

            var idx = System.Array.IndexOf(args, "--permission-mode");
            Assert.Greater(idx, -1);
            Assert.AreEqual("acceptEdits", args[idx + 1]);
        }

        [Test]
        public void Build_PermissionPromptTool_Present()
        {
            var (args, _) = ClaudeArgBuilder.Build(
                "/usr/local/bin/claude", "/tmp/mcp.json", "plan", null);

            Assert.IsTrue(args.Contains("--permission-prompt-tool"),
                "--permission-prompt-tool must be present");
        }

        [Test]
        public void Build_PermissionPromptTool_ValueMatchesServerKey()
        {
            var (args, _) = ClaudeArgBuilder.Build(
                "/usr/local/bin/claude", "/tmp/mcp.json", "plan", null);

            var idx = System.Array.IndexOf(args, "--permission-prompt-tool");
            Assert.Greater(idx, -1);
            Assert.AreEqual(PermissionConfig.MCP_BLANKET + "__permission_prompt", args[idx + 1]);
        }

        [Test]
        public void Build_DisallowedTools_NotPresent()
        {
            // AskUserQuestion is auto-denied in -p mode; --disallowedTools hides it from Claude
            // and prevents it from trying alternatives like mcp__unity__ask_user.
            var (args, _) = ClaudeArgBuilder.Build(
                "/usr/local/bin/claude", "/tmp/mcp.json", "plan", null);

            Assert.IsFalse(args.Contains("--disallowedTools"),
                "--disallowedTools must be absent; AskUserQuestion is already auto-denied in -p mode");
        }

        // ── F1: agent flag ─────────────────────────────────────────────────────

        [Test]
        public void Build_WithAgent_ContainsAgentFlag()
        {
            var (args, _) = ClaudeArgBuilder.Build(
                "/usr/local/bin/claude", "/tmp/mcp.json", "acceptEdits", null, "code-reviewer");

            var idx = System.Array.IndexOf(args, "--agent");
            Assert.Greater(idx, -1, "--agent flag must be present");
            Assert.AreEqual("code-reviewer", args[idx + 1]);
        }

        [Test]
        public void Build_WithAgent_AgentImmediatelyAfterFlag()
        {
            var (args, _) = ClaudeArgBuilder.Build(
                "/usr/local/bin/claude", "/tmp/mcp.json", "acceptEdits", null, "doc-keeper");

            var idx = System.Array.IndexOf(args, "--agent");
            Assert.AreEqual("doc-keeper", args[idx + 1]);
        }

        [Test]
        public void Build_NullAgent_NoAgentFlag()
        {
            var (args, _) = ClaudeArgBuilder.Build(
                "/usr/local/bin/claude", "/tmp/mcp.json", "plan", null, null);

            Assert.IsFalse(System.Array.IndexOf(args, "--agent") >= 0,
                "--agent must not appear when agentName is null");
        }

        [Test]
        public void Build_EmptyAgent_NoAgentFlag()
        {
            var (args, _) = ClaudeArgBuilder.Build(
                "/usr/local/bin/claude", "/tmp/mcp.json", "plan", null, "");

            Assert.IsFalse(System.Array.IndexOf(args, "--agent") >= 0,
                "--agent must not appear when agentName is empty");
        }

        // ── F4: allowedMcpTools param ─────────────────────────────────────────

        [Test]
        public void Build_NullAllowedTools_KeepsBlanketPrefix()
        {
            var (args, _) = ClaudeArgBuilder.Build(
                "/bin/claude", "/cfg.json", "plan", null, null, null);

            var idx = System.Array.IndexOf(args, "--allowedTools");
            Assert.Greater(idx, -1);
            Assert.AreEqual("mcp__unity", args[idx + 1]);
        }

        [Test]
        public void Build_WithAllowedTools_ReplacesBlanketWithEnumeration()
        {
            var (args, _) = ClaudeArgBuilder.Build(
                "/bin/claude", "/cfg.json", "plan", null, null,
                new[] { "get_hierarchy", "batch" });

            var idx = System.Array.IndexOf(args, "--allowedTools");
            Assert.Greater(idx, -1);
            Assert.AreNotEqual("mcp__unity", args[idx + 1],
                "blanket must not appear when allowedMcpTools is provided");
        }

        [Test]
        public void Build_WithAllowedTools_CommaSeparatedFormat()
        {
            var (args, _) = ClaudeArgBuilder.Build(
                "/bin/claude", "/cfg.json", "plan", null, null,
                new[] { "get_hierarchy", "batch" });

            var idx = System.Array.IndexOf(args, "--allowedTools");
            var val = args[idx + 1];
            Assert.IsTrue(val.Contains(","), "multiple tools must be comma-separated");
        }

        [Test]
        public void Build_WithAllowedTools_EachToolHasMcpPrefix()
        {
            var (args, _) = ClaudeArgBuilder.Build(
                "/bin/claude", "/cfg.json", "plan", null, null,
                new[] { "get_hierarchy", "batch" });

            var idx = System.Array.IndexOf(args, "--allowedTools");
            var parts = args[idx + 1].Split(',');
            foreach (var part in parts)
                StringAssert.StartsWith(PermissionConfig.MCP_TOOL_PREFIX, part.Trim());
        }

        // ── FIX 1: per-tool prefix must be mcp__unity__ (matches server key "unity") ──

        [Test]
        public void Build_WithAllowedTools_PrefixMatchesLiveServerKey()
        {
            // The mcp.json server key is "unity", so Claude names tools mcp__unity__<tool>.
            // Enumerated ids must use mcp__unity__ NOT mcp__unity-mcp__.
            var (args, _) = ClaudeArgBuilder.Build(
                "/bin/claude", "/cfg.json", "plan", null, null,
                new[] { "get_hierarchy" });

            var idx = System.Array.IndexOf(args, "--allowedTools");
            Assert.Greater(idx, -1);
            Assert.AreEqual("mcp__unity__get_hierarchy", args[idx + 1],
                "Tool id must match live server key 'unity' from mcp.json");
        }

        [Test]
        public void MCP_TOOL_PREFIX_StartsWithBlanketPlusDblUnderscore()
        {
            // Per-tool prefix must be derived from the blanket (mcp__unity) + "__"
            // so the two can never drift relative to each other.
            const string blanket = "mcp__unity";
            StringAssert.StartsWith(blanket + "__", PermissionConfig.MCP_TOOL_PREFIX,
                "MCP_TOOL_PREFIX must equal the blanket server name + '__'");
        }

        [Test]
        public void Build_EmptyAllowedTools_NoBlanketNoEnumeration()
        {
            var (args, _) = ClaudeArgBuilder.Build(
                "/bin/claude", "/cfg.json", "plan", null, null,
                new string[0]);

            // --allowedTools should not appear at all (nothing to allow)
            Assert.IsFalse(args.Contains("--allowedTools"),
                "--allowedTools must be absent when allowedMcpTools is empty");
        }

        // ── strict-mcp-config ─────────────────────────────────────────────────

        [Test]
        public void Build_IncludesStrictMcpConfig()
        {
            var (args, _) = ClaudeArgBuilder.Build(
                "/usr/local/bin/claude", "/tmp/mcp.json", "plan", null);

            Assert.Contains("--strict-mcp-config", args);
        }

        [Test]
        public void Build_StrictMcpConfigAfterMcpConfigFlag()
        {
            var (args, _) = ClaudeArgBuilder.Build(
                "/usr/local/bin/claude", "/tmp/mcp.json", "plan", null);

            var mcpIdx    = System.Array.IndexOf(args, "--mcp-config");
            var strictIdx = System.Array.IndexOf(args, "--strict-mcp-config");
            Assert.Greater(mcpIdx, -1,    "--mcp-config must be present");
            var pathIdx = mcpIdx + 1;
            Assert.Greater(strictIdx, pathIdx, "--strict-mcp-config must come after the config path value");
        }

        // ── F9: model + extraArgs ─────────────────────────────────────────────

        [Test]
        public void ClaudeArgBuilder_WithModel_AddsModelFlag()
        {
            var (args, _) = ClaudeArgBuilder.Build(
                "/bin/claude", "/cfg.json", "plan", null,
                model: "claude-opus-4");

            var idx = System.Array.IndexOf(args, "--model");
            Assert.Greater(idx, -1, "--model flag must be present");
            Assert.AreEqual("claude-opus-4", args[idx + 1]);
        }

        [Test]
        public void ClaudeArgBuilder_EmptyModel_NoModelFlag()
        {
            var (args, _) = ClaudeArgBuilder.Build(
                "/bin/claude", "/cfg.json", "plan", null,
                model: "");

            Assert.IsFalse(System.Array.IndexOf(args, "--model") >= 0,
                "--model must not appear when model is empty");
        }

        [Test]
        public void ClaudeArgBuilder_WithExtraArgs_AppendsRaw()
        {
            var (args, _) = ClaudeArgBuilder.Build(
                "/bin/claude", "/cfg.json", "plan", null,
                extraArgs: "--debug --timeout 5");

            CollectionAssert.Contains(args, "--debug");
            CollectionAssert.Contains(args, "--timeout");
            CollectionAssert.Contains(args, "5");
        }

        [Test]
        public void ClaudeArgBuilder_ExtraArgsEmptyTokensDropped()
        {
            var (args, _) = ClaudeArgBuilder.Build(
                "/bin/claude", "/cfg.json", "plan", null,
                extraArgs: "  --debug  ");

            // Only "--debug" appended, no empty strings
            int emptyCount = System.Array.FindAll(args, s => s == "").Length;
            Assert.AreEqual(0, emptyCount);
        }
    }
}
