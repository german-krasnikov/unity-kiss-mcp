// TDD: ConsoleCapture — comma-separated level filter fix.
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
    }
}
