// Partial MCPChatWindow — send path: OnSend, AttachScreenshot, AppendChipContext, DispatchTurn.
// NBSP strip via NbspReservation.StripReservation (handles both FFFC+NBSP and bare FFFC).
// Extracted from MCPChatWindow.cs to keep it under 200 lines.
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor.Chat
{
    public partial class MCPChatWindow
    {
        private void OnSend()
        {
            if (!_activity.CanSend) return; // #6: no re-entrant send during active turn
            _autoFix.Disarm(); // user manually sending — cancel any pending auto-fix

            var store = BackendConfigStore.Load();

            // StripReservation removes FFFC+NBSP runs AND bare FFFC (regex matches zero NBSP).
            // No IsAvailable branch needed — proven by StripReservation_BareFFFC_Removed test.
            var rawText = _input.value ?? "";
            var text = NbspReservation.StripReservation(rawText).Trim();
            AppendChipContext(ref text, store);
            if (string.IsNullOrEmpty(text)) return;

            DispatchTurn(UserTurnBuilder.Build(text), text);
        }

        private void AttachScreenshot()
        {
            if (!_activity.CanSend) return; // #6: guard second vector — SS button also dispatches a turn
            var target = Selection.activeGameObject;
            if (target == null) { Debug.LogWarning("[MCP Chat] Select a GameObject first"); return; }
            var capturePath = MultiViewCapture.CaptureToFile(target);
            if (string.IsNullOrEmpty(capturePath)) { Debug.LogWarning("[MCP Chat] Screenshot failed"); return; }
            var bytes = File.ReadAllBytes(capturePath);
            var store = BackendConfigStore.Load();
            var rawSs = _input.value ?? "";
            var text  = NbspReservation.StripReservation(rawSs).Trim();
            AppendChipContext(ref text, store);
            DispatchTurn(UserTurnBuilder.Build(text, bytes), text, screenshotPath: capturePath);
        }

        // F10: merge strip chips + inline chips, emit typed bracket format honouring ChipConfig.
        // store is pre-loaded by the caller to avoid a double file-read per send.
        private void AppendChipContext(ref string text, BackendConfigStore store = null)
        {
            var chips = CollectChipData();

            // F5: merge inline chips — deduplicate by path.
            for (int i = 0; i < _chipTracker.Count; i++)
            {
                var cd = _chipTracker[i];
                if (!chips.Exists(x => x.Path == cd.Path)) chips.Add(cd);
            }

            // #3 Auto-include selection: prepend summary if not already a chip.
            var selGo   = Selection.activeGameObject;
            var chipSet = new HashSet<string>();
            foreach (var c in chips) chipSet.Add(c.Path);
            if (SelectionSummary.ShouldPrepend(selGo, chipSet))
                text = SelectionSummary.Summarize(selGo) + "\n" + text;

            if (chips.Count == 0) return;
            // F10: typed emission — [kind:ref] bracket format, per-kind depth from config.
            var cfg     = (store ?? BackendConfigStore.Load()).Chips;
            var context = ChipContextResolver.ResolveAllTyped(chips, cfg);
            if (!string.IsNullOrEmpty(context)) text += "\n" + context;
        }

        // Shared send sequence — OnSend and AttachScreenshot must not drift from each other.
        private void DispatchTurn(string turnJson, string displayText, string screenshotPath = null)
        {
            // #1/#4 Lock reloads for the duration of this turn.
            ReloadGuard.OnTurnStarted();
            // F6: open a named undo group for the duration of this turn.
            _undoTracker.OnTurnStart(displayText?.Length > 40
                ? displayText.Substring(0, 40)
                : displayText ?? "");
            // FIX A: cache before clearing input so SaveStateBeforeReload can read the sent text.
            _sentTextCache.Set(displayText);
            _transcript.AppendUserBubble(displayText, screenshotPath);
            _backend.SendTurn(turnJson);
            _input.value = ""; _input.cursorIndex = _input.selectIndex = 0;
            _objChipStrip.Clear();
            _chipTracker?.Clear();
            _chipOverlay?.Refresh();
            _heightCalc.Reset();
            ResetInputAreaHeight();
            if (_activity.Send()) OnActivityChanged();
        }
    }
}
