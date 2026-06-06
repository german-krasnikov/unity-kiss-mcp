// TDD — F19: Tool detail labels preserve full text (no C#-side truncation).
using NUnit.Framework;
using UnityEngine.UIElements;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class F19ToolDetailTests
    {
        private static VisualElement MakeChip() =>
            new VisualElement { name = "tool-chip" };

        [Test]
        public void AttachOrUpdate_ArgsLabel_FullTextPreserved()
        {
            var chip = MakeChip();
            var rec  = new ToolCallRecord("get_hierarchy", "1", "{\"path\":\"/Root\"}");
            ToolDetailBuilder.AttachOrUpdate(chip, rec);

            var label = chip.Q<Label>(className: "tool-detail-args");
            Assert.IsNotNull(label);
            StringAssert.Contains("path", label.text);
            StringAssert.Contains("/Root", label.text);
        }

        [Test]
        public void AttachOrUpdate_LongArgsJson_FullyPreservedInLabel()
        {
            var chip    = MakeChip();
            string args = "{\"command\":\"" + new string('x', 500) + "\",\"value\":42}";
            var rec     = new ToolCallRecord("set_property", "2", args);
            ToolDetailBuilder.AttachOrUpdate(chip, rec);

            var label = chip.Q<Label>(className: "tool-detail-args");
            Assert.IsNotNull(label);
            // All 500 'x' chars must survive formatting (they're inside a string literal)
            StringAssert.Contains(new string('x', 500), label.text);
        }

        [Test]
        public void AttachOrUpdate_LongResultText_FullyPreservedInLabel()
        {
            var chip       = MakeChip();
            string longRes = "OK: " + new string('y', 800);
            var rec        = new ToolCallRecord("run_tests", "3", "{}", longRes, true);
            ToolDetailBuilder.AttachOrUpdate(chip, rec);

            var label = chip.Q<Label>(className: "tool-detail-result");
            Assert.IsNotNull(label);
            Assert.AreEqual(longRes, label.text);
        }
    }
}
