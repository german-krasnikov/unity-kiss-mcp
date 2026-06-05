// Partial MCPChatWindow — send path: OnSend, AttachScreenshot, AppendChipContext, DispatchTurn.
// Text is clean by construction (InlineChipField — no FFFC/NBSP stripping needed).
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

            // Text is clean by construction — no FFFC/NBSP stripping needed.
            var text = (_chipField?.Text ?? _input?.value ?? "").Trim();
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
            var text  = (_chipField?.Text ?? _input?.value ?? "").Trim();
            AppendChipContext(ref text, store);
            DispatchTurn(UserTurnBuilder.Build(text, bytes), text, screenshotPath: capturePath);
        }

        // F10: merge strip chips + inline chips, emit typed bracket format honouring ChipConfig.
        // store is pre-loaded by the caller to avoid a double file-read per send.
        private void AppendChipContext(ref string text, BackendConfigStore store = null)
        {
            // Auto-attach removed by design (F12 P3/P5): only explicit chips are sent.
            var chips = CollectChipData();

            // Merge inline chips from InlineChipField — deduplicate by path.
            if (_chipField?.Model != null)
            {
                foreach (var cd in _chipField.Model.Chips)
                {
                    if (!chips.Exists(x => x.Path == cd.Path)) chips.Add(cd);
                }
            }

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
            if (_chipField != null) { _chipField.ClearChips(); _chipField.Text = ""; }
            else if (_input != null) { _input.value = ""; _input.cursorIndex = _input.selectIndex = 0; }
            _objChipStrip.Clear();
            _heightCalc.Reset();
            ResetInputAreaHeight();
            if (_activity.Send()) OnActivityChanged();
        }
    }
}
