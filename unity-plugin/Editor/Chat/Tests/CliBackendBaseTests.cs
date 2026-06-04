// TDD tests for CliBackendBase — uses TestCliBackend double to exercise shared lifecycle.
using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    internal sealed class TestCliBackend : CliBackendBase
    {
        internal bool _persistent = true;
        internal Func<string, List<ChatEvent>, bool> ParseLineFunc;
        internal readonly Queue<string> LinesToDrain = new Queue<string>();
        internal bool   SimulateRunning;
        internal int    SpawnCallCount, WriteLineCallCount, CloseStdinCallCount;
        internal string LastWrittenLine;

        protected override string BinaryName          => "test";
        protected override bool   IsPersistentProcess => _persistent;

        protected override (string[] args, string[] stripEnvKeys) BuildArgs(string binaryPath, string resumeId)
            => (new[] { "arg1" }, new[] { "ENV_KEY" });

        protected override void ParseLine(string line, List<ChatEvent> sink)
            => ParseLineFunc?.Invoke(line, sink);

        protected override void DrainRawLines(List<string> buf)
        { while (LinesToDrain.Count > 0) buf.Add(LinesToDrain.Dequeue()); }

        protected override void SpawnNewProcess(string binary, string[] args, string[] strip)
            => SpawnCallCount++;

        protected override void WriteLineToProc(string line)
        { WriteLineCallCount++; LastWrittenLine = line; }

        protected override void CloseStdinOnProc() => CloseStdinCallCount++;

        public override bool IsRunning => SimulateRunning || base.IsRunning;
    }

    [TestFixture]
    public class CliBackendBaseTests
    {
        private TestCliBackend _b;

        [SetUp] public void SetUp()
        {
            _b = new TestCliBackend();
            ChatBinaryResolver.WhichOverride = _ => "/fake/test";
            ChatBinaryResolver.ResetCacheForTests();
        }

        [TearDown] public void TearDown()
        {
            ChatBinaryResolver.WhichOverride = null;
            ChatBinaryResolver.ResetCacheForTests();
        }

        // Helper: queue a line + install a non-null proc so DrainEvents runs
        private void QueueLine(string line, Func<string, List<ChatEvent>, bool> parser)
        {
            _b.ParseLineFunc = parser;
            _b.LinesToDrain.Enqueue(line);
            _b._proc = new ChatProcess();
        }

        [Test]
        public void DrainEvents_TextDelta_ForwardsToOutput()
        {
            QueueLine("x", (_, sink) => { sink.Add(ChatEvent.TextDelta("hello")); return true; });
            var out_ = new List<ChatEvent>();
            _b.DrainEvents(out_);
            Assert.AreEqual(1, out_.Count);
            Assert.AreEqual(ChatEventKind.TextDelta, out_[0].Kind);
            Assert.AreEqual("hello", out_[0].Text);
        }

        [Test]
        public void DrainEvents_TurnDone_CapturesSessionId()
        {
            QueueLine("x", (_, s) => { s.Add(ChatEvent.TurnDone("sess-42", 0f, 10, 5)); return true; });
            _b.DrainEvents(new List<ChatEvent>());
            Assert.AreEqual("sess-42", _b.SessionId);
        }

        [Test]
        public void DrainEvents_SessionInit_CapturesSessionId()
        {
            QueueLine("x", (_, s) => { s.Add(ChatEvent.SessionInit("init-sess")); return true; });
            _b.DrainEvents(new List<ChatEvent>());
            Assert.AreEqual("init-sess", _b.SessionId);
        }

        [Test]
        public void DrainEvents_ToolStart_EmitsIncompleteChipRecord()
        {
            QueueLine("x", (_, s) => { s.Add(ChatEvent.ToolStart("get_hierarchy", "", "id1")); return true; });
            var toolOut = new List<ToolCallRecord>();
            _b.DrainEvents(new List<ChatEvent>(), toolOut);
            Assert.AreEqual(1, toolOut.Count);
            Assert.AreEqual("get_hierarchy", toolOut[0].Name);
        }

        [Test]
        public void DrainEvents_ToolResult_EmitsToolCallRecord()
        {
            _b.ParseLineFunc = (line, s) =>
            {
                if (line == "start")  s.Add(ChatEvent.ToolStart("my_tool", "", "id2"));
                if (line == "args")   s.Add(ChatEvent.ToolArgsComplete());
                if (line == "result") s.Add(ChatEvent.ToolResult("id2", "output text", true));
                return true;
            };
            _b.LinesToDrain.Enqueue("start");
            _b.LinesToDrain.Enqueue("args");
            _b.LinesToDrain.Enqueue("result");
            _b._proc = new ChatProcess();

            var toolOut = new List<ToolCallRecord>();
            _b.DrainEvents(new List<ChatEvent>(), toolOut);

            var last = toolOut[toolOut.Count - 1];
            Assert.AreEqual("my_tool",     last.Name);
            Assert.AreEqual("output text", last.ResultText);
            Assert.IsTrue(last.IsOk);
        }

        [Test]
        public void DrainEvents_Error_ForwardsToOutput()
        {
            QueueLine("x", (_, s) => { s.Add(ChatEvent.Error("boom")); return true; });
            var out_ = new List<ChatEvent>();
            _b.DrainEvents(out_);
            Assert.AreEqual(1, out_.Count);
            Assert.AreEqual(ChatEventKind.Error, out_[0].Kind);
        }

        [Test]
        public void Stop_ResetsAccumulator()
        {
            _b._accumulator.Feed(ChatEvent.ToolStart("t", "", "id3"));
            _b.Stop();
            var rec = _b._accumulator.Feed(ChatEvent.ToolResult("id3", "res", true));
            Assert.AreEqual("?", rec.Value.Name, "Accumulator must be reset on Stop");
        }

        [Test]
        public void SendTurn_Persistent_WritesToStdin()
        {
            _b._persistent      = true;
            _b.SimulateRunning  = true;
            _b.SendTurn("turn-payload");
            Assert.AreEqual(1, _b.WriteLineCallCount);
            Assert.AreEqual("turn-payload", _b.LastWrittenLine);
        }

        [Test]
        public void SendTurn_SpawnPerTurn_DisposesAndRespawns()
        {
            _b._persistent = false;
            _b.SendTurn("first-turn");
            Assert.AreEqual(1, _b.SpawnCallCount);
            _b.SendTurn("second-turn");
            Assert.AreEqual(2, _b.SpawnCallCount);
        }

        [Test]
        public void SendTurn_SpawnPerTurn_CloseStdin()
        {
            _b._persistent = false;
            _b.SendTurn("some-prompt");
            Assert.AreEqual(1, _b.CloseStdinCallCount, "CloseStdin must be called after spawn");
        }

        [Test]
        public void Start_NullBinary_DoesNotThrow()
        {
            ChatBinaryResolver.WhichOverride = _ => null;
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("binary not found"));
            Assert.DoesNotThrow(() => _b.Start());
            Assert.AreEqual(0, _b.SpawnCallCount, "No spawn when binary not found");
        }
    }
}
