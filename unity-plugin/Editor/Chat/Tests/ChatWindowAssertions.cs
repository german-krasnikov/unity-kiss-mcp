// Reusable static assertion helpers for VE tree queries.
// Uses CSS class selectors only — no resolvedStyle dependency.
using NUnit.Framework;
using System.Collections.Generic;
using System.Text;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat.Tests
{
    internal static class ChatWindowAssertions
    {
        // ── Input area ────────────────────────────────────────────────────────

        internal static void AssertChipCount(InlineChipField field, int expected)
        {
            var pillRow = field[0];
            var pills = pillRow.Query(className: "inline-chip-pill").ToList();
            Assert.AreEqual(expected, pills.Count,
                $"Expected {expected} chip pill(s) in pillRow, found {pills.Count}");
        }

        internal static void AssertPillRowVisible(InlineChipField field)
            => Assert.AreEqual(DisplayStyle.Flex, field[0].style.display.value, "PillRow should be visible");

        internal static void AssertPillRowHidden(InlineChipField field)
            => Assert.AreEqual(DisplayStyle.None, field[0].style.display.value, "PillRow should be hidden");

        internal static void AssertInputText(InlineChipField field, string expected)
            => Assert.AreEqual(expected, field.Text, $"Expected input text \"{expected}\", got \"{field.Text}\"");

        // ── Transcript ────────────────────────────────────────────────────────

        internal static void AssertUserBubbleCount(VisualElement container, int expected)
        {
            var bubbles = container.Query(className: "msg-bubble--user").ToList();
            Assert.AreEqual(expected, bubbles.Count,
                $"Expected {expected} user bubble(s), found {bubbles.Count}");
        }

        internal static VisualElement GetUserBubble(VisualElement container, int index)
        {
            var bubbles = container.Query(className: "msg-bubble--user").ToList();
            Assert.IsTrue(index >= 0 && index < bubbles.Count,
                $"Requested bubble index {index} but only {bubbles.Count} bubble(s) exist");
            return bubbles[index];
        }

        internal static void AssertBubbleHasChipStrip(VisualElement bubble, int chipCount)
        {
            var strip = bubble.Q(className: "user-chip-strip");
            Assert.IsNotNull(strip, "User bubble must contain a .user-chip-strip element");
            var pills = strip.Query(className: "inline-chip-pill").ToList();
            Assert.AreEqual(chipCount, pills.Count,
                $"Expected {chipCount} pill(s) in chip strip, found {pills.Count}");
        }

        internal static void AssertBubbleHasNoChipStrip(VisualElement bubble)
        {
            var strip = bubble.Q(className: "user-chip-strip");
            Assert.IsNull(strip, "User bubble must not contain a .user-chip-strip element");
        }

        internal static void AssertBubbleText(VisualElement bubble, string expectedSubstring)
        {
            var msgText = bubble.Q(className: "msg-text");
            Assert.IsNotNull(msgText, "User bubble must contain a .msg-text element");
            if (msgText is Label lbl)
            {
                StringAssert.Contains(expectedSubstring, lbl.text);
                return;
            }
            var labels = msgText.Query<Label>().ToList();
            var sb = new StringBuilder();
            foreach (var l in labels) sb.Append(l.text);
            StringAssert.Contains(expectedSubstring, sb.ToString());
        }

        internal static void AssertPillContent(VisualElement pill, string kindKey, string displayName)
        {
            var kindLbl = pill.Q<Label>(className: "inline-chip-kind");
            Assert.IsNotNull(kindLbl, "Pill must have an .inline-chip-kind label");
            Assert.AreEqual(kindKey + ":", kindLbl.text);
            var nameLbl = pill.Q<Label>(className: "inline-chip-label");
            Assert.IsNotNull(nameLbl, "Pill must have an .inline-chip-label label");
            Assert.AreEqual(displayName, nameLbl.text);
        }
    }
}
