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
        public void Build_DisallowsAskUserQuestion_ImmediatelyAfterFlag()
        {
            // AskUserQuestion auto-fails in headless stream-json (~500ms, no stdin wait)
            // so the model must be forced to ask via prose instead.
            var (args, _) = ClaudeArgBuilder.Build(
                "/usr/local/bin/claude", "/tmp/mcp.json", "plan", null);

            var idx = System.Array.IndexOf(args, "--disallowedTools");
            Assert.Greater(idx, -1, "--disallowedTools flag must be present");
            Assert.AreEqual("AskUserQuestion", args[idx + 1]);
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

        [Test]
        public void Build_WithAllowedTools_AskUserQuestionStillDisallowed()
        {
            var (args, _) = ClaudeArgBuilder.Build(
                "/bin/claude", "/cfg.json", "plan", null, null,
                new[] { "get_hierarchy" });

            var idx = System.Array.IndexOf(args, "--disallowedTools");
            Assert.Greater(idx, -1, "--disallowedTools must still be present");
            Assert.AreEqual("AskUserQuestion", args[idx + 1]);
        }
    }
}
