// CH1.test.5 + CH1.test.6: CodexAppServerBackend unit tests.
// Tests SpawnNewProcess sends initialize immediately, and ExtractPromptText parsing.
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class CodexAppServerBackendTests
    {
        [SetUp]
        public void SetUp()
        {
            ChatBinaryResolver.WhichOverride = _ => "/fake/codex";
            ChatBinaryResolver.ResetCacheForTests();
        }

        [TearDown]
        public void TearDown()
        {
            ChatBinaryResolver.WhichOverride = null;
            ChatBinaryResolver.ResetCacheForTests();
        }

        // ── CH1.test.6: ExtractPromptText ──────────────────────────────────────

        [Test]
        public void ExtractPromptText_ValidEnvelope_ReturnsText()
        {
            const string envelope = "{\"message\":{\"content\":[{\"type\":\"text\",\"text\":\"hello world\"}]}}";
            var result = CodexAppServerBackend.ExtractPromptText(envelope);
            Assert.AreEqual("hello world", result);
        }

        [Test]
        public void ExtractPromptText_EmptyContent_ReturnsEmpty()
        {
            const string envelope = "{\"message\":{\"content\":[]}}";
            var result = CodexAppServerBackend.ExtractPromptText(envelope);
            Assert.AreEqual("", result);
        }

        [Test]
        public void ExtractPromptText_NoMessage_ReturnsEmpty()
        {
            var result = CodexAppServerBackend.ExtractPromptText("{}");
            Assert.AreEqual("", result);
        }

        [Test]
        public void ExtractPromptText_NullInput_ReturnsEmpty()
        {
            var result = CodexAppServerBackend.ExtractPromptText(null);
            Assert.AreEqual("", result);
        }

        [Test]
        public void ExtractPromptText_MultipleContentItems_ReturnsFirstText()
        {
            const string envelope =
                "{\"message\":{\"content\":[{\"type\":\"text\",\"text\":\"first\"},{\"type\":\"text\",\"text\":\"second\"}]}}";
            var result = CodexAppServerBackend.ExtractPromptText(envelope);
            Assert.AreEqual("first", result);
        }

        // ── CH1.test.5: SpawnNewProcess increments _nextId for initialize fire-and-forget ─────

        [Test]
        public void SpawnNewProcess_NextIdIncremented_AfterStart()
        {
            // SpawnNewProcess calls base.WriteLineToProc with the initialize message,
            // which increments _nextId. Verify via reflection that _nextId >= 1 after Start().
            var backend = new CodexAppServerBackend();

            var idBefore = (int)typeof(CodexAppServerBackend)
                .GetField("_nextId", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(backend);

            // Start() calls SpawnNewProcess → sends initialize → bumps _nextId
            // ChatBinaryResolver will return "/fake/codex" from our override.
            // _proc.Spawn will fail silently (no real process) but _nextId is incremented first.
            try { backend.Start(); } catch { /* process spawn may fail in headless — that's ok */ }

            var idAfter = (int)typeof(CodexAppServerBackend)
                .GetField("_nextId", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(backend);

            Assert.Greater(idAfter, idBefore,
                "_nextId must be incremented by SpawnNewProcess (fire-and-forget initialize call)");
            backend.Stop();
        }

        // ── BuildArgs must emit 3 -c flags for MCP config ─────────────────────

        [Test]
        public void BuildArgs_HasThreeMcpConfigFlags()
        {
            var backend = new CodexAppServerBackend();
            var method  = typeof(CodexAppServerBackend)
                .GetMethod("BuildArgs", BindingFlags.NonPublic | BindingFlags.Instance);

            var result = ((string[] args, string[] stripEnvKeys))method.Invoke(
                backend, new object[] { "/fake/codex", null });
            var args = result.args;

            var cValues = new List<string>();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-c") cValues.Add(args[i + 1]);

            Assert.AreEqual(3, cValues.Count, "BuildArgs must emit exactly 3 -c flags");
            Assert.IsTrue(cValues.Exists(v => v.StartsWith("mcp_servers.unity.command=")));
            Assert.IsTrue(cValues.Exists(v => v.StartsWith("mcp_servers.unity.args=")));
            Assert.IsTrue(cValues.Exists(v => v.StartsWith("mcp_servers.unity.startup_timeout_sec=")));
            backend.Stop();
        }
    }
}
