// Amber "Restore" button injected into transcript after each turn (F6).
// Chat-only (Chat asmdef, defineConstraint UNITY_MCP_CHAT).
using System;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat
{
    public static class RestoreButton
    {
        /// <summary>
        /// Creates an amber "Restore" button tied to <paramref name="tracker"/>.
        /// Only the LAST turn's button stays enabled — captured generation check
        /// disables old buttons the moment a new turn starts (architecture §risk #6).
        /// </summary>
        public static VisualElement Create(TurnUndoTracker tracker, Action onRestored = null)
        {
            int capturedGeneration = tracker.CurrentGeneration;

            Button btn = null;
            btn = new Button(() =>
            {
                tracker.RestoreLastTurn();
                btn.SetEnabled(false);
                btn.text = "Restored";
                onRestored?.Invoke();
            });

            btn.text    = "Restore";
            btn.tooltip = "Revert this turn's scene changes";
            btn.AddToClassList("chat-btn");
            btn.AddToClassList("chat-btn--restore");

            // Refresh enabled state on geometry changes (covers repaint-equivalent).
            btn.RegisterCallback<GeometryChangedEvent>(_ => RefreshEnabled(btn, tracker, capturedGeneration));
            // Also refresh when the panel ticks (schedule-based refresh).
            btn.schedule.Execute(() => RefreshEnabled(btn, tracker, capturedGeneration)).Every(200);

            RefreshEnabled(btn, tracker, capturedGeneration);
            return btn;
        }

        /// <summary>
        /// Recomputes and applies the button's enabled state.
        /// Exposed as internal static so tests can assert the last-only disable rule
        /// without requiring a live UIElements panel.
        /// </summary>
        internal static void RefreshEnabled(Button btn, TurnUndoTracker tracker, int capturedGeneration)
        {
            var shouldEnable =
                tracker.HasRestorableGroup &&
                tracker.CurrentGeneration == capturedGeneration;
            btn.SetEnabled(shouldEnable);
        }
    }
}
