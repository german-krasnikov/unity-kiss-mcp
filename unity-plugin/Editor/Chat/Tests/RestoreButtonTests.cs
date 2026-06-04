// TDD — Tests for RestoreButton.RefreshEnabled last-only disable rule (F6 MAJOR 2).
// Chat.Tests asmdef has defineConstraint UNITY_MCP_CHAT and references UnityMCP.Editor.Chat.
#if UNITY_MCP_CHAT
using NUnit.Framework;
using UnityEngine.UIElements;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    /// <summary>
    /// Verifies that once a new turn starts, the OLD turn's Restore button becomes disabled.
    /// This is the most important correctness rule for F6 (architecture §risk #6).
    /// </summary>
    [TestFixture]
    public class RestoreButtonTests
    {
        // --- 16. Button enabled when generation matches and turn is restorable ----
        [Test]
        public void RefreshEnabled_SameGeneration_EnablesButton()
        {
            var tracker = new TurnUndoTracker();
            tracker.OnTurnStart("Turn_A");
            tracker.OnTurnEnd();
            int genA = tracker.CurrentGeneration;

            var btn = new Button();
            RestoreButton.RefreshEnabled(btn, tracker, genA);

            Assert.IsTrue(btn.enabledSelf, "Button should be enabled when generation matches");
        }

        // --- 17. Button disables when a new turn starts (old capturedGeneration is stale) ---
        [Test]
        public void RefreshEnabled_NewTurnStarted_DisablesOldButton()
        {
            var tracker = new TurnUndoTracker();
            tracker.OnTurnStart("Turn_A");
            tracker.OnTurnEnd();
            int genA = tracker.CurrentGeneration; // generation captured by old button

            // New turn starts — generation advances
            tracker.OnTurnStart("Turn_B");

            var btn = new Button();
            RestoreButton.RefreshEnabled(btn, tracker, genA);

            Assert.IsFalse(btn.enabledSelf,
                "Old button must disable when a new turn has started (last-only rule)");
        }
    }
}
#endif
