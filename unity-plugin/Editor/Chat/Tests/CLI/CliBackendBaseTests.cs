// TDD tests for CliBackendBase — uses TestCliBackend double to exercise shared lifecycle.
using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    public sealed class TestCliBackend : CliBackendBase
    {
        public bool _persistent = true;
        public bool _sendInit   = false;
        public Func<string, List<ChatEvent>, bool> ParseLineFunc;
        public readonly Queue<string> LinesToDrain = new Queue<string>();
        public bool   SimulateRunning;
        public int    SpawnCallCount, WriteLineCallCount, CloseStdinCallCount;
        public string LastWrittenLine;
        public string LastResumeId = "UNSET"; // sentinel; null = was explicitly passed null

        protected override string BinaryName              => "test";
        protected override bool   IsPersistentProcess     => _persistent;
        protected override bool   SendInitializeHandshake => _sendInit;

        protected override (string[] args, string[] stripEnvKeys) BuildArgs(string binaryPath, string resumeId)
        {
            LastResumeId = resumeId; // capture for assertions
            return (new[] { "arg1" }, new[] { "ENV_KEY" });
        }

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
            string captured = null;
            _b.LogError = m => captured = m;
            Assert.DoesNotThrow(() => _b.Start());
            StringAssert.Contains("binary not found", captured);
            Assert.AreEqual(0, _b.SpawnCallCount, "No spawn when binary not found");
        }

        [Test]
        public void Start_AlreadyRunning_DoesNotSpawnAgain()
        {
            // Simulate a running process so IsRunning returns true.
            _b.SimulateRunning = true;
            _b.Start();
            Assert.AreEqual(0, _b.SpawnCallCount, "Must not spawn when already running");
        }

        [Test]
        public void Start_AlreadyRunning_CalledTwice_SingleSpawn()
        {
            // First Start: not running → spawns once.
            _b.Start();
            Assert.AreEqual(1, _b.SpawnCallCount);

            // Second Start: now simulate running → guard must skip.
            _b.SimulateRunning = true;
            _b.Start();
            Assert.AreEqual(1, _b.SpawnCallCount, "Second Start must be a no-op when already running");
        }

        // CH1.test.7: SendTurn persistent-process, not running → auto-starts then writes
        [Test]
        public void SendTurn_Persistent_NotRunning_AutoStartsThenWrites()
        {
            _b._persistent     = true;
            _b._sendInit       = true;
            _b.SimulateRunning = false; // not running — must auto-start

            _b.SendTurn("my-payload");

            Assert.AreEqual(1, _b.SpawnCallCount, "must spawn when not running");
            // initialize handshake (1) + turn payload (1) = 2 writes
            Assert.AreEqual(2, _b.WriteLineCallCount, "must write initialize + payload");
            Assert.AreEqual("my-payload", _b.LastWrittenLine);
        }

        // Sprint 1C: persistent backend sends initialize handshake on Start
        [Test]
        public void Start_Persistent_SendsInitializeHandshake()
        {
            _b._persistent     = true;
            _b._sendInit       = true;
            _b.SimulateRunning = false;

            _b.Start();

            Assert.AreEqual(1, _b.WriteLineCallCount, "initialize must be written on Start");
            StringAssert.Contains("\"subtype\":\"initialize\"", _b.LastWrittenLine);
        }

        [Test]
        public void Start_SpawnPerTurn_NoInitializeHandshake()
        {
            _b._persistent = false;

            _b.Start();

            Assert.AreEqual(0, _b.WriteLineCallCount, "spawn-per-turn must NOT send initialize");
        }

        // X5.cross.5: DrainEvents ToolCallAccumulator pipeline — start+args+result produces complete record
        [Test]
        public void DrainEvents_FullToolPipeline_ArgsAndResultInRecord()
        {
            _b.ParseLineFunc = (line, s) =>
            {
                if (line == "start")  s.Add(ChatEvent.ToolStart("read_file", "", "id_r"));
                if (line == "args")   s.Add(ChatEvent.ToolArgsComplete());
                if (line == "result") s.Add(ChatEvent.ToolResult("id_r", "file contents", true));
                return true;
            };
            _b.LinesToDrain.Enqueue("start");
            _b.LinesToDrain.Enqueue("args");
            _b.LinesToDrain.Enqueue("result");
            _b._proc = new ChatProcess();

            var toolOut = new List<ToolCallRecord>();
            _b.DrainEvents(new List<ChatEvent>(), toolOut);

            Assert.AreEqual(3, toolOut.Count, "must emit 3 tool records (start, args-complete, result)");
            var final = toolOut[toolOut.Count - 1];
            Assert.AreEqual("read_file",      final.Name);
            Assert.AreEqual("file contents",  final.ResultText);
            Assert.IsTrue(final.IsOk,         "IsOk must be true for successful result");
            Assert.IsTrue(final.HasResult,     "HasResult must be true after result event");
        }

        // DRY is_error guard in base class — all parsers protected automatically
        [Test]
        public void DrainEvents_IsErrorEnvelope_EmitsError()
        {
            var line = "{\"type\":\"result\",\"is_error\":true,\"error\":\"Exit code 1\"}";
            bool parseLineCalled = false;
            _b.ParseLineFunc = (_, s) => { parseLineCalled = true; return true; };
            _b.LinesToDrain.Enqueue(line);
            _b._proc = new ChatProcess();

            var out_ = new List<ChatEvent>();
            _b.DrainEvents(out_);

            Assert.AreEqual(1, out_.Count);
            Assert.AreEqual(ChatEventKind.Error, out_[0].Kind);
            StringAssert.Contains("Exit code 1", out_[0].Text);
            Assert.IsFalse(parseLineCalled, "ParseLine must be skipped for is_error envelope");
        }

        [Test]
        public void DrainEvents_NormalLine_DelegatesToParseLine()
        {
            bool parseLineCalled = false;
            _b.ParseLineFunc = (_, s) => { parseLineCalled = true; s.Add(ChatEvent.TextDelta("hi")); return true; };
            _b.LinesToDrain.Enqueue("{\"type\":\"assistant\",\"text\":\"hi\"}");
            _b._proc = new ChatProcess();

            var out_ = new List<ChatEvent>();
            _b.DrainEvents(out_);

            Assert.IsTrue(parseLineCalled, "ParseLine must be called for non-error lines");
            Assert.AreEqual(1, out_.Count);
        }
    }
}
