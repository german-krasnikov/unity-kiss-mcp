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
    }
}
