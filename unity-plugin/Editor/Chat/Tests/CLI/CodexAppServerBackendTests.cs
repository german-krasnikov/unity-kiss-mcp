// CH1.test.5 + CH1.test.6: CodexAppServerBackend unit tests.
// Tests SpawnNewProcess sends initialize immediately, and ExtractPromptText parsing.
using System;
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

            // Start() calls SpawnNewProcess → sends initialize → bumps _nextId.
            // Spawn may fail in headless (no real process) but _nextId must be incremented first.
            Exception spawnException = null;
            try { backend.Start(); }
            catch (Exception ex) { spawnException = ex; }

            var idAfter = (int)typeof(CodexAppServerBackend)
                .GetField("_nextId", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(backend);

            // _nextId must have been incremented by exactly 1 (the initialize message).
            // If this fails AND spawnException != null, it means spawn threw before increment.
            Assert.AreEqual(idBefore + 1, idAfter,
                $"_nextId must be incremented by exactly 1; spawnException={spawnException?.Message}");
            backend.Stop();
        }

        // ── BuildArgs: disables static unity/unity-mcp entries, registers unity_chat ─────

        [Test]
        public void BuildArgs_DisablesStaticEntries_AndRegistersUnityChatServer()
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

            // 2 disable + 3 registration + 2 env = 7 -c flags (no model)
            Assert.AreEqual(7, cValues.Count, $"BuildArgs without model must emit exactly 7 -c flags, got: {string.Join(", ", cValues)}");
            Assert.IsTrue(cValues.Exists(v => v == "mcp_servers.unity.enabled=false"), "must disable global unity entry");
            Assert.IsTrue(cValues.Exists(v => v == "mcp_servers.unity-mcp.enabled=false"), "must disable project unity-mcp entry");
            Assert.IsTrue(cValues.Exists(v => v.StartsWith("mcp_servers.unity_chat.command=")));
            Assert.IsTrue(cValues.Exists(v => v.StartsWith("mcp_servers.unity_chat.args=")));
            Assert.IsTrue(cValues.Exists(v => v.StartsWith("mcp_servers.unity_chat.startup_timeout_sec=")));
            Assert.IsTrue(cValues.Exists(v => v.Contains("unity_chat.env.UNITY_MCP_PORT=")));
            Assert.IsTrue(cValues.Exists(v => v.Contains("unity_chat.env.UNITY_MCP_CHAT=")));
            backend.Stop();
        }

        // ── BuildArgs with model must emit model via -c, NOT --model ─────────

        [Test]
        public void BuildArgs_WithModel_EmitsModelAsCFlag()
        {
            var backend = new CodexAppServerBackend(model: "o3");
            var method  = typeof(CodexAppServerBackend)
                .GetMethod("BuildArgs", BindingFlags.NonPublic | BindingFlags.Instance);

            var result = ((string[] args, string[] stripEnvKeys))method.Invoke(
                backend, new object[] { "/fake/codex", null });
            var args = result.args;

            var cValues = new List<string>();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-c") cValues.Add(args[i + 1]);

            // 7 base flags + 1 model = 8 -c flags total
            Assert.AreEqual(8, cValues.Count, $"BuildArgs with model must emit 8 -c flags, got: {string.Join(", ", cValues)}");
            Assert.IsTrue(cValues.Exists(v => v == "model=\"o3\""),
                $"Expected -c model=\"o3\" but got: {string.Join(", ", cValues)}");
            CollectionAssert.DoesNotContain(args, "--model", "app-server does not accept --model");
            backend.Stop();
        }
    }
}
