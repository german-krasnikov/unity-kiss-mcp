// TDD tests for #7: ChatSettingsHook.AddDefine / RemoveDefine pure string helpers.
using NUnit.Framework;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class ChatSettingsHookTests
    {
        private const string Define = "UNITY_MCP_CHAT";

        // AddDefine: absent → adds it
        [Test]
        public void AddDefine_WhenAbsent_AddsSymbol()
        {
            var result = ChatSettingsHook.AddDefine("FOO;BAR", Define);
            StringAssert.Contains(Define, result);
        }

        // AddDefine: idempotent — already present → no duplicate
        [Test]
        public void AddDefine_WhenPresent_IsIdempotent()
        {
            var input  = $"FOO;{Define};BAR";
            var result = ChatSettingsHook.AddDefine(input, Define);
            // Count occurrences — must be exactly 1
            int count = 0;
            foreach (var s in result.Split(';'))
                if (s.Trim() == Define) count++;
            Assert.AreEqual(1, count, "Symbol must appear exactly once");
        }

        // RemoveDefine: present → removes it
        [Test]
        public void RemoveDefine_WhenPresent_RemovesSymbol()
        {
            var result = ChatSettingsHook.RemoveDefine($"FOO;{Define};BAR", Define);
            StringAssert.DoesNotContain(Define, result);
            StringAssert.Contains("FOO", result);
            StringAssert.Contains("BAR", result);
        }

        // RemoveDefine: absent → no-op, no exception
        [Test]
        public void RemoveDefine_WhenAbsent_IsNoOp()
        {
            var input  = "FOO;BAR";
            var result = ChatSettingsHook.RemoveDefine(input, Define);
            StringAssert.Contains("FOO", result);
            StringAssert.Contains("BAR", result);
        }

        // RemoveDefine: no partial-substring match — removing FOO must not affect FOOBAR
        [Test]
        public void RemoveDefine_DoesNotRemovePrefixedSymbol()
        {
            var result = ChatSettingsHook.RemoveDefine($"FOOBAR;{Define}", "FOO");
            StringAssert.Contains("FOOBAR", result);
        }

        // AddDefine on empty string produces just the symbol
        [Test]
        public void AddDefine_EmptyDefines_ProducesJustSymbol()
        {
            var result = ChatSettingsHook.AddDefine("", Define);
            Assert.AreEqual(Define, result);
        }

        // RemoveDefine on empty string → empty (no exception)
        [Test]
        public void RemoveDefine_EmptyDefines_ReturnsEmpty()
        {
            var result = ChatSettingsHook.RemoveDefine("", Define);
            Assert.AreEqual("", result);
        }
    }
}
