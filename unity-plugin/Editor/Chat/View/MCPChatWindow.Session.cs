// Partial MCPChatWindow — session management (P6: New Session / Clear).
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat
{
    public partial class MCPChatWindow
    {
        /// <summary>
        /// Reset everything for a fresh session. Called from the session menu after confirm.
        /// </summary>
        internal void NewSession()
        {
            // 0. Release reload lock if a turn was in flight.
            ReloadGuard.OnTurnFinished();

            // 1. Kill old backend + build a fresh one (current config + snapshot, SessionId=null → no --resume).
            _backend?.Stop();   // dispose old process FIRST (may be mid-turn) — CreateBackend does not dispose
            CreateBackend();

            // 2. Clear transcript (finalizes any in-flight bubble first).
            _transcript?.Clear();

            // 3. Clear input field + inline chips.
            if (_chipField != null) { _chipField.ClearChips(); _chipField.Text = ""; }

            // 5. Clear reload-survival state (prevents resurrection on domain reload).
            ReloadGuard.ClearPendingState();

            // 6. Clear sent-text cache.
            _sentTextCache.Set("");

            // 7. Reset input area height.
            _heightCalc.Reset();
            ResetInputAreaHeight();

            // 8. Clear session-scoped tool approvals (Always-allowed persist; session-only reset).
            _sessionAllowlist.ClearSession();

            // 8b. Disarm auto-fix (no stale compile-error retries into new session).
            _autoFix.Disarm();
            ResetTurnFlags(); // P0-2: DRY reset (was 2 inline, _needsRefresh was missing)

            // 9. Reset activity state to Idle (unlocks send button).
            if (_activity.Phase != ActivityPhase.Idle)
            {
                _activity.Done();
                OnActivityChanged();
            }

            // 10. Reset token counters.
            ResetTokenCounters();

            // 11. Invalidate undo tracker (old session groups are meaningless).
            _undoTracker.Invalidate();
        }

        /// <summary>
        /// Build the session menu button for the footer bar.
        /// Direct click → confirm dialog → NewSession (F25: removed submenu layer).
        /// </summary>
        internal Button BuildSessionMenuButton()
        {
            var btn = new Button(() =>
            {
                if (EditorUtility.DisplayDialog(
                    "New Session",
                    "Clear the transcript and start a fresh session?\n\nThis kills the current backend process and cannot be undone.",
                    "Clear", "Cancel"))
                {
                    NewSession();
                }
            }) { text = "☰" };
            btn.AddToClassList("chat-btn");
            btn.tooltip = "New session / Clear";
            return btn;
        }
    }
}
