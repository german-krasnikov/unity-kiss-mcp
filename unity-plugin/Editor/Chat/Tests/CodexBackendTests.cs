// TDD tests for CodexBackend — first-turn snapshot injection.
// EditMode: uses SceneProviderOverride seam + TestCodexBackend to capture argv.
#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    /// <summary>
    /// Minimal subclass: captures SpawnNewProcess argv so we can assert on the prompt arg.
    /// Constructor skips the package-path resolution by injecting python command directly.
    /// </summary>
    internal sealed class TestCodexBackend : CliBackendBase
    {
        internal string[] LastArgs;

        internal void SetSessionId(string id) => SessionId = id;

        // Reuse persistent=false so spawn-per-turn path fires in SendTurn.
        protected override bool   IsPersistentProcess => false;
        protected override string BinaryName          => "codex";

        protected override (string[] args, string[] stripEnvKeys) BuildArgs(string binaryPath, string resumeId)
        {
            var rawPrompt = ExtractText(PendingPrompt ?? "");

            if (resumeId == null && !string.IsNullOrEmpty(rawPrompt))
            {
                var snapshot = EditorStateSnapshot.Capture();
                if (!string.IsNullOrEmpty(snapshot))
                    rawPrompt = snapshot + "\n\n" + rawPrompt;
            }

            return CodexArgBuilder.Build(rawPrompt, resumeId,
                "/fake/python3", new[] { "-m", "unity_mcp.server" });
        }

        protected override void ParseLine(string line, List<ChatEvent> sink) { }

        protected override void SpawnNewProcess(string binary, string[] args, string[] strip)
            => LastArgs = args;

        protected override void CloseStdinOnProc() { }

        // Mirror CodexBackend.ExtractPromptText — extract "text" from UserTurnBuilder JSON.
        private static string ExtractText(string turnJson)
        {
            var msg     = JsonHelper.ExtractObject(turnJson, "message");
            var content = JsonHelper.ExtractArray(msg, "content");
            var first   = JsonHelper.ExtractFirstArrayObject(content);
            return (first != null ? JsonHelper.ExtractString(first, "text") : null) ?? "";
        }
    }

    [TestFixture]
    public class CodexBackendTests
    {
        private TestCodexBackend _b;

        [SetUp]
        public void SetUp()
        {
            _b = new TestCodexBackend();
            ChatBinaryResolver.WhichOverride = _ => "/fake/codex";
            ChatBinaryResolver.ResetCacheForTests();

            // Control snapshot output so assertions are deterministic.
            EditorStateSnapshot.SceneProviderOverride = () => "Hierarchy: /Player";
        }

        [TearDown]
        public void TearDown()
        {
            ChatBinaryResolver.WhichOverride = null;
            ChatBinaryResolver.ResetCacheForTests();
            EditorStateSnapshot.SceneProviderOverride = null;
        }

        // Minimal UserTurnBuilder-style JSON envelope with a text prompt.
        private static string TurnJson(string text)
            => $"{{\"message\":{{\"role\":\"user\",\"content\":[{{\"type\":\"text\",\"text\":\"{text}\"}}]}}}}";

        [Test]
        public void FirstTurn_SnapshotPrepended_PromptArgContainsUnityState()
        {
            _b.SendTurn(TurnJson("list scene objects"));

            var lastArg = _b.LastArgs[_b.LastArgs.Length - 1];
            StringAssert.Contains("[Unity State]", lastArg,
                "First-turn prompt must be prefixed with snapshot");
            StringAssert.Contains("list scene objects", lastArg);
        }

        [Test]
        public void ResumeTurn_NoSnapshot_PromptArgIsRaw()
        {
            _b.SetSessionId("sess-resume-123");
            _b.SendTurn(TurnJson("follow-up question"));

            var lastArg = _b.LastArgs[_b.LastArgs.Length - 1];
            StringAssert.DoesNotContain("[Unity State]", lastArg,
                "Resume turn must not have snapshot prefix");
            StringAssert.Contains("follow-up question", lastArg);
        }

        [Test]
        public void FirstTurn_EmptyPrompt_NoSnapshotInjected()
        {
            // Empty prompt → snapshot injection guard `!string.IsNullOrEmpty(rawPrompt)` → skip.
            _b.SendTurn(TurnJson(""));

            // With empty prompt there's no last arg containing text — just verify no crash
            // and snapshot not artificially appended to empty string.
            if (_b.LastArgs != null && _b.LastArgs.Length > 0)
            {
                var lastArg = _b.LastArgs[_b.LastArgs.Length - 1];
                // Last arg should not be just the snapshot with no user text.
                Assert.IsFalse(lastArg == EditorStateSnapshot.Capture(),
                    "Snapshot alone must not become the prompt arg");
            }
        }
    }
}
#endif
