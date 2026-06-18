// TDD: ConsoleCapture — comma-separated level filter fix.
using System;
using NUnit.Framework;
using UnityEngine;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class ConsoleCaptureTests
    {
        [SetUp]
        public void SetUp() => ConsoleCapture.Clear();

        [TearDown]
        public void TearDown() => ConsoleCapture.Clear();

        [Test]
        public void GetLogs_CommaLevel_FiltersCorrectly()
        {
            ConsoleCapture.InjectForTest("err msg", LogType.Error);
            ConsoleCapture.InjectForTest("warn msg", LogType.Warning);
            ConsoleCapture.InjectForTest("log msg", LogType.Log);

            var result = ConsoleCapture.GetLogs(level: "Error,Warning");

            StringAssert.Contains("err msg", result);
            StringAssert.Contains("warn msg", result);
            StringAssert.DoesNotContain("log msg", result);
        }

        [Test]
        public void GetLogs_SingleLevel_FiltersCorrectly()
        {
            ConsoleCapture.InjectForTest("err msg", LogType.Error);
            ConsoleCapture.InjectForTest("warn msg", LogType.Warning);
            ConsoleCapture.InjectForTest("log msg", LogType.Log);

            var result = ConsoleCapture.GetLogs(level: "Error");

            StringAssert.Contains("err msg", result);
            StringAssert.DoesNotContain("warn msg", result);
            StringAssert.DoesNotContain("log msg", result);
        }

        [Test]
        public void GetLogs_InvalidLevel_ReturnsAll()
        {
            ConsoleCapture.InjectForTest("err msg", LogType.Error);
            ConsoleCapture.InjectForTest("log msg", LogType.Log);

            var result = ConsoleCapture.GetLogs(level: "invalid");

            StringAssert.Contains("err msg", result);
            StringAssert.Contains("log msg", result);
        }

        [Test]
        public void GetLogs_FirstParam_WithLevelFilter_FiltersCorrectly()
        {
            // Inject into init buffer (within 5s window after Clear)
            ConsoleCapture.InjectForTest("init err", LogType.Error);
            ConsoleCapture.InjectForTest("init log", LogType.Log);

            var result = ConsoleCapture.GetLogs(count: 10, level: "Error", first: 2);

            StringAssert.Contains("init err", result);
            StringAssert.DoesNotContain("init log", result);
        }

        [Test]
        public void GetLogs_NullLevel_ReturnsAll()
        {
            ConsoleCapture.InjectForTest("err msg", LogType.Error);
            ConsoleCapture.InjectForTest("log msg", LogType.Log);

            var result = ConsoleCapture.GetLogs(level: null);

            StringAssert.Contains("err msg", result);
            StringAssert.Contains("log msg", result);
        }

        [Test]
        public void GetLogs_Count_ReturnsLastN()
        {
            for (int i = 0; i < 5; i++)
                ConsoleCapture.InjectForTest($"msg-{i}", LogType.Log);

            var result = ConsoleCapture.GetLogs(count: 2);

            StringAssert.Contains("msg-3", result);
            StringAssert.Contains("msg-4", result);
            StringAssert.DoesNotContain("msg-0", result);
        }

        [Test]
        public void GetLogs_RingBuffer_ReturnsEntries()
        {
            // Entries 0–49 fill _initBuffer. Entry 50 triggers _initPhaseOpen=false and lands in ring.
            for (int i = 0; i < 50; i++)
                ConsoleCapture.InjectForTest($"init-{i}", LogType.Log);

            ConsoleCapture.InjectForTest("ring-error", LogType.Error);

            var result = ConsoleCapture.GetLogs(level: "Error");

            StringAssert.Contains("ring-error", result);
        }

        [Test]
        public void GetLogs_RingBuffer_PreservesChronologicalOrder()
        {
            for (int i = 0; i < 50; i++)
                ConsoleCapture.InjectForTest($"init-{i}", LogType.Log);

            ConsoleCapture.InjectForTest("ring-first", LogType.Error);
            ConsoleCapture.InjectForTest("ring-second", LogType.Error);

            var result = ConsoleCapture.GetLogs(level: "Error");

            Assert.Less(result.IndexOf("ring-first"), result.IndexOf("ring-second"));
        }

        [Test]
        public void GetErrorsSince_ReturnsErrorsAfterTimestamp()
        {
            ConsoleCapture.InjectForTest("before-error", LogType.Error);
            System.Threading.Thread.Sleep(20);
            var since = DateTime.Now;
            System.Threading.Thread.Sleep(20);
            ConsoleCapture.InjectForTest("after-error", LogType.Error);

            var result = ConsoleCapture.GetErrorsSince(since);

            StringAssert.Contains("after-error", result);
            StringAssert.DoesNotContain("before-error", result);
        }

        [Test]
        public void GetErrorsSince_ReturnsNull_WhenNoErrorsAfterTimestamp()
        {
            ConsoleCapture.InjectForTest("old-error", LogType.Error);
            var since = DateTime.Now.AddSeconds(1);

            var result = ConsoleCapture.GetErrorsSince(since);

            Assert.IsNull(result);
        }

        [Test]
        public void GetErrorsSince_IgnoresNonErrors()
        {
            var since = DateTime.Now.AddSeconds(-1);
            ConsoleCapture.InjectForTest("a warning", LogType.Warning);
            ConsoleCapture.InjectForTest("a log", LogType.Log);

            var result = ConsoleCapture.GetErrorsSince(since);

            Assert.IsNull(result);
        }

        [Test]
        public void GetLogs_CountMinusOne_ReturnsAll()
        {
            ConsoleCapture.InjectForTest("error", LogType.Error);
            ConsoleCapture.InjectForTest("warning", LogType.Warning);
            ConsoleCapture.InjectForTest("log", LogType.Log);

            var result = ConsoleCapture.GetLogs(count: -1);

            StringAssert.Contains("error", result);
            StringAssert.Contains("warning", result);
            StringAssert.Contains("log", result);
        }

        [Test]
        public void GetLogs_EmptyBuffer_ReturnsEmptyString()
        {
            var result = ConsoleCapture.GetLogs();

            Assert.AreEqual("", result);
        }
    }
}
