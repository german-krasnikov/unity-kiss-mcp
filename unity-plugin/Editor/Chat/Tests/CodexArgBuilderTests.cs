// TDD tests for CodexArgBuilder.
// Pure unit tests — no I/O, no Unity API.
using System;
using System.Linq;
using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class CodexArgBuilderTests
    {
        private static readonly string[]   DefaultPythonArgs = { "-m", "unity_mcp.server" };
        private const           string     DefaultPythonCmd  = "/path/to/python3";
        private const           string     SomePrompt        = "list scene objects";
        private const           string     SessionId         = "019e9353-8c51-7143-89cb-e1fa68b4bb08";

        private static string[] Build(string prompt = SomePrompt, string resume = null)
        {
            var (args, _) = CodexArgBuilder.Build(prompt, resume, DefaultPythonCmd, DefaultPythonArgs);
            return args;
        }

        // ── First-turn argv ───────────────────────────────────────────────────

        [Test]
        public void FirstTurn_StartsWithExecJson()
        {
            var args = Build();
            Assert.AreEqual("exec",   args[0]);
            Assert.AreEqual("--json", args[1]);
        }

        [Test]
        public void FirstTurn_HasSandboxAndSkipGit()
        {
            var args = Build();
            CollectionAssert.Contains(args, "-s");
            CollectionAssert.Contains(args, "danger-full-access");
            CollectionAssert.Contains(args, "--skip-git-repo-check");
        }

        [Test]
        public void FirstTurn_HasCwd()
        {
            var args = Build();
            CollectionAssert.Contains(args, "-C");
            // Value after -C must be a non-empty string
            var idx = Array.IndexOf(args, "-C");
            Assert.Greater(idx + 1, 0);
            Assert.IsFalse(string.IsNullOrEmpty(args[idx + 1]));
        }

        [Test]
        public void FirstTurn_McpConfigFlags_Present()
        {
            var args    = Build();
            var cValues = new System.Collections.Generic.List<string>();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-c") cValues.Add(args[i + 1]);

            Assert.AreEqual(3, cValues.Count, "Must have exactly 3 -c flags");
            Assert.IsTrue(cValues.Any(v => v.StartsWith("mcp_servers.unity.command=")));
            Assert.IsTrue(cValues.Any(v => v.StartsWith("mcp_servers.unity.args=")));
            Assert.IsTrue(cValues.Any(v => v.StartsWith("mcp_servers.unity.startup_timeout_sec=")));
        }

        [Test]
        public void FirstTurn_PromptIsLastArg()
        {
            var args = Build(SomePrompt);
            Assert.AreEqual(SomePrompt, args[args.Length - 1]);
        }

        // ── Resume argv ───────────────────────────────────────────────────────

        [Test]
        public void Resume_StartsWithExecResume()
        {
            var args = Build(resume: SessionId);
            Assert.AreEqual("exec",    args[0]);
            Assert.AreEqual("resume",  args[1]);
            Assert.AreEqual(SessionId, args[2]);
        }

        [Test]
        public void Resume_HasBypassFlag()
        {
            var args = Build(resume: SessionId);
            CollectionAssert.Contains(args, "--dangerously-bypass-approvals-and-sandbox");
        }

        [Test]
        public void Resume_NoCwdNoSandboxShort()
        {
            var args = Build(resume: SessionId);
            CollectionAssert.DoesNotContain(args, "-C");
            CollectionAssert.DoesNotContain(args, "-s");
        }

        [Test]
        public void Resume_PromptIsLastArg()
        {
            var args = Build(SomePrompt, SessionId);
            Assert.AreEqual(SomePrompt, args[args.Length - 1]);
        }

        [Test]
        public void Resume_McpConfigFlags_StillPresent()
        {
            var args = Build(resume: SessionId);
            var cValues = new System.Collections.Generic.List<string>();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-c") cValues.Add(args[i + 1]);

            Assert.AreEqual(3, cValues.Count);
        }

        // ── Env keys ──────────────────────────────────────────────────────────

        [Test]
        public void StripEnvKeys_ContainsOpenAiKey()
        {
            var (_, strip) = CodexArgBuilder.Build(SomePrompt, null, DefaultPythonCmd, DefaultPythonArgs);
            CollectionAssert.Contains(strip, "OPENAI_API_KEY");
        }

        // ── Null/empty prompt guard ───────────────────────────────────────────

        [Test]
        public void NullPrompt_NoTrailingNullOrEmpty()
        {
            var args = Build(null);
            // Last arg must be the startup_timeout_sec TOML value — no blank prompt appended.
            Assert.IsFalse(string.IsNullOrEmpty(args[args.Length - 1]));
            StringAssert.Contains("startup_timeout_sec", args[args.Length - 1]);
        }

        // ── TOML escaping ─────────────────────────────────────────────────────

        [Test]
        public void TomlEscaping_QuotesStrings()
        {
            var cValues = new System.Collections.Generic.List<string>();
            var args    = Build();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-c") cValues.Add(args[i + 1]);

            var commandVal = cValues.First(v => v.StartsWith("mcp_servers.unity.command="));
            // After "command=" the value must be double-quoted (TOML basic string)
            var afterEq = commandVal.Substring(commandVal.IndexOf('=') + 1);
            Assert.IsTrue(afterEq.StartsWith("\""), $"command value should be quoted: {afterEq}");
            Assert.IsTrue(afterEq.EndsWith("\""), $"command value should end with quote: {afterEq}");
        }
    }
}
